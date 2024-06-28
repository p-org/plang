package pexplicit.runtime.machine.events;

import lombok.Getter;
import pexplicit.runtime.machine.PMachine;
import pexplicit.runtime.machine.State;
import pexplicit.utils.serialize.SerializableBiFunction;
import pexplicit.utils.serialize.SerializableRunnable;
import pexplicit.values.PEvent;
import pexplicit.values.PMessage;

import java.util.HashSet;
import java.util.Set;

@Getter
public class PContinuation {
    private final Set<String> caseEvents;
    private final SerializableBiFunction<PMachine, PMessage> handleFun;
    private final SerializableRunnable clearFun;

    public PContinuation(SerializableBiFunction<PMachine, PMessage> handleFun, SerializableRunnable clearFun, String... ev) {
        this.handleFun = handleFun;
        this.clearFun = clearFun;
        this.caseEvents = new HashSet<>(Set.of(ev));
    }

    public boolean isDeferred(PEvent event) {
        return !event.isHaltMachineEvent() && !caseEvents.contains(event.toString());
    }

    /**
     * Run after a machine executes this continuation
     * First, if machine is unblocked, run any pending state exit function
     * Second, if still unblocked, run any pending new state entry function
     * Third, if still unblocked, clear all continuation variables
     *
     * @param machine Machine that just executed this continuation
     */
    public void runAfter(PMachine machine) {
        // When a continuation is processed/unblocks, the machine might unblock or may get blocked on
        // a different continuation.

        // If machine unblocks (i.e., no pending continuation), then we run any pending state exit function first.
        if (!machine.isBlocked()) {
            State blockedExitState = machine.getBlockedStateExit();
            if (blockedExitState != null) {
                assert (machine.getCurrentState() == blockedExitState);
                machine.exitCurrentState();
            }
        } else {
            // blocked on a different continuation encountered when processing this continuation, do nothing
            return;
        }

        // At this point, the machine might remain unblocked or may get blocked on a different continuation
        // encountered when executing the pending state exit function.

        // In all cases, at this point there should be no pending state exit function at this point.
        assert (machine.getBlockedStateExit() == null);

        // If machine unblocks (i.e., no pending continuation), then we run any pending state new state entry function.
        if (!machine.isBlocked()) {
            State blockedEntryState = machine.getBlockedNewStateEntry();
            if (blockedEntryState != null) {
                machine.enterNewState(blockedEntryState, machine.getBlockedNewStateEntryPayload());
            }
        } else {
            // blocked on a different continuation encountered when processing pending state exit function, do nothing
            return;
        }

        // At this point, the machine might remain unblocked or may get blocked on a different continuation
        // encountered in the pending new state entry function.

        // In all cases, there should be no pending exit or new state entry functions at this point.
        assert (machine.getBlockedStateExit() == null);
        assert (machine.getBlockedNewStateEntry() == null);

        // If machine unblocks (i.e., no pending continuation), cleanup continuation variables since machine is
        // completely unblocked
        if (!machine.isBlocked()) {
            for (PContinuation c : machine.getContinuationMap().values()) {
                c.getClearFun().run();
            }
        } else {
            // blocked on a different continuation encountered when processing pending new state entry function, do nothing
        }
    }

}