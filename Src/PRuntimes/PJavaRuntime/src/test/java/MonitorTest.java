import prt.events.PEvent;
import org.junit.jupiter.api.DisplayName;
import org.junit.jupiter.api.Test;
import prt.*;
import prt.exceptions.NonTotalStateMapException;
import prt.exceptions.PAssertionFailureException;

import java.util.ArrayList;
import java.util.List;
import java.util.Optional;

import static org.junit.jupiter.api.Assertions.*;

public class MonitorTest {

    private enum SingleState { INIT_STATE }
    private enum BiState { INIT_STATE, OTHER_STATE }

    private enum ABCState { A_STATE, B_STATE, C_STATE }

    /**
     * This monitor has no default state; an exception should be raised when .ready() is called.
     */
    class NoDefaultStateMonitor extends Monitor {
        public NoDefaultStateMonitor() {
            super();
            addState(new State.Builder<>(SingleState.INIT_STATE).build());
        }

        public void reInitializeMonitor() {}
        
        public List<Class<? extends PEvent<?>>> getEventTypes() { return List.of(); }
    }

    /**
     * This monitor has two default states; an exception will be thrown in the second addState().
     */
    class MultipleDefaultStateMonitors extends Monitor {
        public MultipleDefaultStateMonitors() {
            super();
            addState(new State.Builder<>(BiState.INIT_STATE).isInitialState(true).build());
            addState(new State.Builder<>(BiState.OTHER_STATE).isInitialState(true).build());
        }

        public void reInitializeMonitor() {}

        public List<Class<? extends PEvent<?>>> getEventTypes() { return List.of(); }
    }

    /**
     * This monitor should have two default states but only one is implemented.
     */
    class NonTotalStateMapMonitor extends Monitor {
        public NonTotalStateMapMonitor() {
            super();
            addState(new State.Builder<>(BiState.INIT_STATE).isInitialState(true).build());
        }

        public void reInitializeMonitor() {}

        public List<Class<? extends PEvent<?>>> getEventTypes() { return List.of(); }
    }
    /**
     * This monitor has two states with the same key; an exception will be thrown in the second addState().
     */
    class NonUniqueStateKeyMonitor extends Monitor {
        public NonUniqueStateKeyMonitor() {
            super();
            addState(new State.Builder<>(BiState.INIT_STATE).isInitialState(true).build());
            addState(new State.Builder<>(BiState.INIT_STATE).isInitialState(true).build());
        }

        public void reInitializeMonitor() {}

        public List<Class<? extends PEvent<?>>> getEventTypes() { return List.of(); }
    }

    /**
     * This prt.Monitor has one piece of ghost state: a counter that can be incremented by
     * processing events.
     */
    class CounterMonitor extends Monitor {

        class AddEvent extends PEvent<Integer> {
            private int payload;
            public AddEvent(int payload) {
                this.payload = payload;
            }
            public Integer getPayload() { return payload; }
        }

        public int count;

        public CounterMonitor() {
            super();
            count = 0;

            addState(new State.Builder<>(SingleState.INIT_STATE)
                    .isInitialState(true)
                    .withEvent(AddEvent.class, i -> count += i)
                    .build());
        }

        public void reInitializeMonitor() {}

        public List<Class<? extends PEvent<?>>> getEventTypes() { return List.of(); }
    }

    class ChainedEntryHandlerMonitor extends Monitor {
        public List<ABCState> stateAcc; // We'll use this to track what states we've transitioned through.
        public ChainedEntryHandlerMonitor() {
            super();

            stateAcc = new ArrayList<>();

            addState(new State.Builder<>(ABCState.A_STATE)
                    .isInitialState(true)
                    .withEntry(() -> gotoState(ABCState.B_STATE))
                    .withExit(() -> stateAcc.add(ABCState.A_STATE))
                    .build());
            addState(new State.Builder<>(ABCState.B_STATE)
                    .withEntry(() -> gotoState(ABCState.C_STATE))
                    .withExit(() -> stateAcc.add(ABCState.B_STATE))
                    .build());
            addState(new State.Builder<>(ABCState.C_STATE)
                    .withEntry(() -> stateAcc.add(ABCState.C_STATE))
                    .build());
        }

        public void reInitializeMonitor() {}

        public List<Class<? extends PEvent<?>>> getEventTypes() { return List.of(); }
    }


    class GotoStateWithPayloadsMonitor extends Monitor {
        public List<Object> eventsProcessed; // We'll use this to track what events we've processed

        public GotoStateWithPayloadsMonitor() {
            super();

            eventsProcessed = new ArrayList<>();

            addState(new State.Builder<>(ABCState.A_STATE)
                    .isInitialState(true)
                    .withEntry(() -> {
                        gotoState(ABCState.B_STATE, "Hello from prt.State A");
                    })
                    .build());
            addState(new State.Builder<>(ABCState.B_STATE)
                    .withEntry(s -> {
                        eventsProcessed.add(s);
                        gotoState(ABCState.C_STATE, "Hello from prt.State B");
                    })
                    .build());
            addState(new State.Builder<>(ABCState.C_STATE)
                    .withEntry(s -> eventsProcessed.add(s))
                    .build());
        }

        public void reInitializeMonitor() {}

        public List<Class<? extends PEvent<?>>> getEventTypes() { return List.of(); }
    }


    class GotoStateWithPayloadsMonitorIncludingInitialEntryHandler extends Monitor {
        public List<Object> eventsProcessed; // We'll use this to track what events we've processed

        public GotoStateWithPayloadsMonitorIncludingInitialEntryHandler() {
            super();

            eventsProcessed = new ArrayList<>();

            addState(new State.Builder<>(ABCState.A_STATE)
                    .isInitialState(true)
                    .withEntry((String s) -> {
                        eventsProcessed.add(s);
                        gotoState(ABCState.B_STATE, "Hello from prt.State A");
                    })
                    .build());
            addState(new State.Builder<>(ABCState.B_STATE)
                    .withEntry((String s) -> {
                        eventsProcessed.add(s);
                        gotoState(ABCState.C_STATE, "Hello from prt.State B");
                    })
                    .build());
            addState(new State.Builder<>(ABCState.C_STATE)
                    .withEntry((String s) -> eventsProcessed.add(s))
                    .build());
        }

        public void reInitializeMonitor() {}

        public List<Class<? extends PEvent<?>>> getEventTypes() { return List.of(CounterMonitor.AddEvent.class); }
    }


    class GotoStateWithIllTypedPayloadsMonitor extends Monitor {
        public List<String> eventsProcessed; // We'll use this to track what events we've processed

        public GotoStateWithIllTypedPayloadsMonitor() {
            super();

            eventsProcessed = new ArrayList<>();

            addState(new State.Builder<>(ABCState.A_STATE)
                    .isInitialState(true)
                    .withEntry(() -> gotoState(ABCState.B_STATE, Integer.valueOf(42))) // Here we pass an Integer to the interrupt handler...
                    .build());
            addState(new State.Builder<>(ABCState.B_STATE)
                    .withEntry((String s) -> eventsProcessed.add(s)) //...but here we enforce that it must be a string!
                    .build());
            addState(new State.Builder<>(ABCState.C_STATE)
                    .build());
        }

        public void reInitializeMonitor() {}

        public List<Class<? extends PEvent<?>>> getEventTypes() { return List.of(); }
    }

    /**
     * This monitor immediately asserts.
     */
    class ImmediateAssertionMonitor extends Monitor {
        public ImmediateAssertionMonitor() {
            super();
            addState(new State.Builder<>(SingleState.INIT_STATE)
                    .isInitialState(true)
                    .withEntry(() -> tryAssert(1 > 2, "Math works"))
                    .build());
        }

        public void reInitializeMonitor() {}

        public List<Class<? extends PEvent<?>>> getEventTypes() { return List.of(); }
    }

    class RaiseEventMonitor extends Monitor {
        public class testEvent extends PEvent<Void> {
            public Void getPayload() { return null; }
        }
        public class noopEvent extends PEvent<Void> {
            public Void getPayload() { return null; }
        }

        public void reInitializeMonitor() {}

        public List<Class<? extends PEvent<?>>> getEventTypes() { return List.of(testEvent.class, noopEvent.class); }

        public RaiseEventMonitor() {
            super();
            addState(new State.Builder<>(SingleState.INIT_STATE)
                    .isInitialState(true)
                    .withEvent(testEvent.class, __ -> {
                        tryRaiseEvent(new noopEvent());
                        throw new RuntimeException("tryRaiseEvent must terminate executing the current event");
                    })
                    .withEvent(noopEvent.class, __ -> {})
                    .build());
        }
    }

    @Test
    @DisplayName("Monitors' state maps must be total")
    public void testTotalMonitorMap() {
        NonTotalStateMapMonitor m = new NonTotalStateMapMonitor();
        assertThrows(NonTotalStateMapException.class, () -> m.ready(), "State map is not total");
    }

    @Test
    @DisplayName("Monitors require unique states")
    public void testNonUniqueStateKeyConstruction() {
        Throwable e;
        e = assertThrows(RuntimeException.class, () -> new NonUniqueStateKeyMonitor().ready());
        assertTrue(e.getMessage().contains("prt.State already present"));
    }

    @Test
    @DisplayName("Monitors must be ready()ied before events can be processed")
    public void testNonReadyMonitors() {
        CounterMonitor m = new CounterMonitor();
        Throwable e = assertThrows(RuntimeException.class, () -> m.accept(m.new AddEvent(42)));
        assertTrue(e.getMessage().contains("not running"));
    }

    @Test
    @DisplayName("prt.Monitor can process ghost state-mutating events")
    public void testStateMutationOnEvent() {
        CounterMonitor m = new CounterMonitor();
        m.ready();

        assertEquals(m.count, 0);
        m.accept(m.new AddEvent(1));
        m.accept(m.new AddEvent(2));
        m.accept(m.new AddEvent(3));
        assertEquals(m.count, 6);
    }

    @Test
    @DisplayName("Chained gotos in entry handlers work")
    public void testChainedEntryHandlers() {
        ChainedEntryHandlerMonitor m = new ChainedEntryHandlerMonitor();
        m.ready();

        assertTrue(m.stateAcc.equals(List.of(ABCState.A_STATE, ABCState.B_STATE, ABCState.C_STATE)));
    }

    @Test
    @DisplayName("Payloads can be passed to entry handlers")
    public void testChainedEntryHandlersWithPayloads() {
        GotoStateWithPayloadsMonitor m = new GotoStateWithPayloadsMonitor();
        m.ready();

        assertTrue(m.eventsProcessed.equals(List.of(Optional.of("Hello from prt.State A"),
                Optional.of("Hello from prt.State B"))));
    }

    @Test
    @DisplayName("ready() cannot be called twice")
    public void testCantCallReadyTwice() {
        GotoStateWithPayloadsMonitor m = new GotoStateWithPayloadsMonitor();
        m.ready();
        assertThrows(RuntimeException.class, () -> m.ready(), "prt.Monitor is already running.");
    }

    @Test
    @DisplayName("Event handlers consuuming arguments in ready() must consume them!")
    public void testInitialEntryHandlerMustHaveAnArg() {
        GotoStateWithPayloadsMonitorIncludingInitialEntryHandler m =
                new GotoStateWithPayloadsMonitorIncludingInitialEntryHandler();
        assertThrows(NullPointerException.class, () -> m.ready());
    }

    @Test
    @DisplayName("Ill-typed payload handlers throw")
    public void testChainedEntryHandlersWithIllTypedPayloads() {
        GotoStateWithIllTypedPayloadsMonitor m = new GotoStateWithIllTypedPayloadsMonitor();

        assertThrows(ClassCastException.class, () -> m.ready());
    }

    @Test
    @DisplayName("Ill-typed ready() argument causes throw")
    public void testIllTypedReadyCallThrows() {
        GotoStateWithPayloadsMonitorIncludingInitialEntryHandler m =
                new GotoStateWithPayloadsMonitorIncludingInitialEntryHandler();
        assertThrows(ClassCastException.class, () -> m.ready(Integer.valueOf(42)));
    }

    @Test
    @DisplayName("Assertion failures throw")
    public void testAssertionFailureDoesThrow() {
        ImmediateAssertionMonitor m = new ImmediateAssertionMonitor();
        Throwable e = assertThrows(PAssertionFailureException.class,
                () -> m.ready(), "Assertion failed: Math works");
    }

    @Test
    @DisplayName("tryRaiseEvent interrupts control flow")
    public void testTryRaiseEvent() {
        RaiseEventMonitor m = new RaiseEventMonitor();
        m.ready();

        m.accept(m.new testEvent());
    }
}
