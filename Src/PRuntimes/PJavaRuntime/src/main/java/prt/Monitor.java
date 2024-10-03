package prt;

import java.util.*;
import java.util.function.Consumer;

import prt.events.PEvent;
import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;
import org.apache.logging.log4j.Marker;
import org.apache.logging.log4j.MarkerManager;
import prt.exceptions.*;
import java.io.Serializable;

/**
 * A prt.Monitor encapsulates a state machine.
 *
 */
public abstract class Monitor<StateKey extends Enum<StateKey>> implements Consumer<PEvent<?>>, Serializable {
    private static final Logger logger = LogManager.getLogger(Monitor.class);
    private static final Marker PROCESSING_MARKER = MarkerManager.getMarker("EVENT_PROCESSING");
    private static final Marker TRANSITIONING_MARKER = MarkerManager.getMarker("STATE_TRANSITIONING");

    private StateKey startStateKey;
    private StateKey currentStateKey;

    private transient EnumMap<StateKey, State<StateKey>> states; // All registered states
    private StateKey[] stateUniverse;                  // all possible states

    /**
     * If the prt.Monitor is running, new states must not be able to be added.
     * If the monitor is not running, events must not be able to be processed and states can't be transitioned.
     */
    private boolean isRunning;

    /**
     * Adds a new prt.State to the state machine.
     *
     * @param s The state.
     */
    protected void addState(State<StateKey> s) {
        Objects.requireNonNull(s);
        if (isRunning) {
            throw new RuntimeException("prt.Monitor is already running; no new states may be added.");
        }

        registerState(s);

        if (s.isInitialState()) {
            if (startStateKey != null) {
                throw new RuntimeException("Initial state already set to " + startStateKey);
            }
            startStateKey = s.getKey();
        }
    }

    protected void registerState(State<StateKey> s) {
        if (states == null) {
            states = new EnumMap<>((Class<StateKey>) s.getKey().getClass());
            stateUniverse = s.getKey().getDeclaringClass().getEnumConstants();
        }

        if (states.containsKey(s.getKey())) {
            throw new RuntimeException("prt.State already present");
        }
        states.put(s.getKey(), s);
    }

    public StateKey getCurrentState() {
        if (!isRunning) {
            throw new RuntimeException("prt.Monitor is not running (did you call ready()?)");
        }

        return currentStateKey;
    }

    /**
     * Throws a runtime exception if the given boolean is false.
     * @param cond The predicate to assert on.
     * @param msg The message to deliver if the predicate is false.
     */
    public void tryAssert(boolean cond, String msg)
    {
        if (!cond) throw new PAssertionFailureException(msg);
    }

    /**
     * Interrupts the current event handler and processes the given event in the current state
     * @param ev The event to process.
     * @throws RaiseEventException to context-switch back into the runtime.
     */
    @SuppressWarnings(value = "unchecked")
    public <P> void tryRaiseEvent(PEvent<P> ev) throws RaiseEventException
    {
        throw new RaiseEventException((PEvent<Object>) ev);
    }

    /**
     * Transitions the prt.Monitor to a new state, without including a payload.
     *
     * @param k the key of the state to transition to.
     *
     * @throws RuntimeException if `k` is not a state in the state machine.
     */
    public void gotoState(StateKey k) throws TransitionException {
        Objects.requireNonNull(k);

        if (!states.containsKey(k)) {
            throw new RuntimeException("prt.State not present");
        }
        throw new TransitionException(states.get(k));
    }

    /**
     * Transitions the prt.Monitor to a new state, delivering the given event afterwards.
     *
     * @param k the key of the state to transition to.
     * @param payload The payload to hand to the state entry handler.
     *
     * @throws RuntimeException if `k` is not a state in the state machine.
     */
    public <P> void gotoState(StateKey k, P payload) throws TransitionException {
        Objects.requireNonNull(k);
        Objects.requireNonNull(payload);

        if (!states.containsKey(k)) {
            throw new RuntimeException("prt.State not present");
        }
        throw new TransitionException(states.get(k), payload);
    }

    /**
     * Synchronously processes one Event.Payload message.
     *
     * @param p the pEvent.
     * @throws UnhandledEventException if the pEvent's type has no associated handler.
     */
    @SuppressWarnings(value = "unchecked")
    public void accept(PEvent<?> p) throws UnhandledEventException {
        Objects.requireNonNull(p);

        if (!isRunning) {
            throw new RuntimeException("prt.Monitor is not running (did you call ready()?)");
        }

        //logger.info(PROCESSING_MARKER, new StringMapMessage().with("event", p));

        // XXX: We can technically avoid this downcast, but to fulfill the interface for Consumer<T>
        // this method cannot accept a type parameter, so this can't be a TransitionableConsumer<P>.
        State<StateKey> currentState = states.get(currentStateKey);
        Optional<State.TransitionableConsumer<Object>> oc = currentState.getHandler(p.getClass());
        if (oc.isEmpty()) {
            logger.atFatal().log(currentState + " missing event handler for " + p.getClass().getSimpleName());
            throw new UnhandledEventException(currentState, p.getClass());
        }

        invokeWithTrampoline(oc.get(), p.getPayload());
    }

    /**
     * Transitions to `s` by invoking the current state's exit handler and the new state's
     * entry handler, and updating internal bookkeeping.
     * @param s The new state.
     */
    private <P> void handleTransition(State<StateKey> s, P payload) {
        if (!isRunning) {
            throw new RuntimeException("prt.Monitor is not running (did you call ready()?)");
        }

        //logger.info(TRANSITIONING_MARKER, new StringMapMessage().with("state", s));

        State<StateKey> currentState = states.get(currentStateKey);
        currentState.getOnExit().ifPresent(Runnable::run);
        currentState = s;
        currentStateKey = s.getKey();

        currentState.getOnEntry().ifPresent(handler -> {
            invokeWithTrampoline(handler, payload);
        });
    }

    /**
     * Invokes a given Consumer, handling all its checked exceptions.
     * @param handler The TransitionableConsumer to be invoked.
     * @param o The argument to handler.
     * @param <P> The type to be consumed by the handler.
     */
    private <P> void invokeWithTrampoline(State.TransitionableConsumer<P> handler, P o)
    {
        try {
            // Run the event handler, knowing that it might cause:
            handler.accept(o);
        } catch (TransitionException e) {
            // ...A state transition: if it does, run the exit handler, context-switch, and run
            // the new state's entry handler.
            handleTransition(e.getTargetState(), e.getPayload());
        } catch (RaiseEventException e) {
            // ...An event to be raised.  If it does, process the event in the current state.
            accept(e.getEvent());
        }
    }

    /**
     * Marks the prt.Monitor as ready to run and consume events.  The initial state's entry handler, which
     * must be a handler of zero parameters, will be invoked.
     */
    public void ready() {
        readyImpl(null);
    }

    /**
     * Marks the Monitor as ready to run and consume events.  The initial state's entry handler, which must
     * be a handler that consumes a payload of type P, will be invoked with the given argument.
     * @param payload The argument to the initial state's entry handler.
     */
    public <P> void ready(P payload) {
        readyImpl(payload);
    }

    private <P> void readyImpl(P payload) {
        if (isRunning) {
            throw new RuntimeException("prt.Monitor is already running.");
        }

        for (StateKey k : stateUniverse) {
            if (!states.containsKey(k)) {
                throw new NonTotalStateMapException(k);
            }
        }

        isRunning = true;

        currentStateKey = startStateKey;
        State<StateKey> currentState = states.get(currentStateKey);

        currentState.getOnEntry().ifPresent(handler -> {
            invokeWithTrampoline(handler, payload);
        });
    }

    /**
     * Instantiates a new prt.Monitor; users should provide domain-specific functionality in a subclass.
     */
    protected Monitor() {
        startStateKey = null;
        isRunning = false;

        states = null; // We need a concrete class to instantiate an EnumMap; do this lazily on the first addState() call.
        currentStateKey = null; // So long as we have not yet readied, this will be null!
    }

    public abstract List<Class<? extends PEvent<?>>> getEventTypes();

    public abstract void reInitializeMonitor();

}
