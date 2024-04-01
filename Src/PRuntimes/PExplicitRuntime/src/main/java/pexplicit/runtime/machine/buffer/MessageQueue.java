package pexplicit.runtime.machine.buffer;

import lombok.Getter;
import pexplicit.runtime.machine.PMachine;
import pexplicit.runtime.machine.events.PMessage;
import pexplicit.utils.exceptions.NotImplementedException;

import java.io.Serializable;
import java.util.ArrayList;
import java.util.List;

/**
 * Represents an event queue
 */
public abstract class MessageQueue implements Serializable {

    private final PMachine owner;
    @Getter
    protected List<PMessage> elements;
    private PMessage peek;

    /**
     * Constructor
     *
     * @param owner Owner of the queue
     */
    public MessageQueue(PMachine owner) {
        this.owner = owner;
        this.elements = new ArrayList<>();
        resetPeek();
    }

    /**
     * Reset the queue peek
     */
    public void resetPeek() {
        peek = null;
    }

    /**
     * Get the number of elements in the queue
     *
     * @return Size of the queue
     */
    public int size() {
        return elements.size();
    }

    /**
     * Check whether or not the queue is empty
     *
     * @return true if queue is empty, else false
     */
    public boolean isEmpty() {
        return elements.isEmpty();
    }

    /**
     * Get the peek message in the queue
     *
     * @return Peek message in the queue
     */
    public PMessage peek() {
        return peekOrDequeueHelper(false);
    }

    /**
     * TODO
     * Get (or dequeue) the next message in the queue
     *
     * @param dequeue Whether or not to dequeue the message from the queue
     * @return The next message in the queue, or null if queue is empty
     */
    private PMessage peekOrDequeueHelper(boolean dequeue) {
        throw new NotImplementedException();
    }

    /**
     * TODO
     *
     * @param e
     */
    public void add(PMessage e) {
        throw new NotImplementedException();
    }

    /**
     * TODO
     *
     * @return
     */
    public PMessage remove() {
        throw new NotImplementedException();
    }

    /**
     * Set the queue elements based on the input messages
     *
     * @param messages Input messages
     */
    public void setElements(List<PMessage> messages) {
        this.elements = messages;
        resetPeek();
    }

    @Override
    public String toString() {
        return String.format("MessageQueue{elements=%s}", elements);
    }
}