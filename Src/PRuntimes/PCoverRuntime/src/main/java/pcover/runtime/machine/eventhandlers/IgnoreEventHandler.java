package pcover.runtime.machine.eventhandlers;

import pcover.values.PValue;
import pcover.values.PEvent;
import pcover.runtime.machine.Machine;

/**
 * Represents the ignore event handler
 */
public class IgnoreEventHandler extends EventHandler {

    /**
     * Constructor
     * @param event Event
     */
    public IgnoreEventHandler(PEvent event) {
        super(event);
    }

    /**
     * @inheritDoc
     */
    @Override
    public void handleEvent(Machine target, PValue<?> payload) {
        // Ignore
    }
}