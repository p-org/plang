using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PChecker.Runtime.Events;
using PChecker.Runtime.StateMachines.Managers;


namespace PChecker.Runtime.StateMachines.EventQueues;

/// <summary>
/// Implements a bag of events that is used during testing.
/// </summary>
internal sealed class EventBag : IEventQueue
{
    /// <summary>
    /// Manages the state machine that owns this queue.
    /// </summary>
    private readonly IStateMachineManager StateMachineManager;

    /// <summary>
    /// The state machine that owns this queue.
    /// </summary>
    private readonly StateMachine StateMachine;

    /// <summary>
    /// The internal queue that contains events with their metadata.
    /// </summary>
    private readonly List<(Event e, EventInfo info)> Bag;

    /// <summary>
    /// The raised event and its metadata, or null if no event has been raised.
    /// </summary>
    private (Event e, EventInfo info) RaisedEvent;

    /// <summary>
    /// Map from the types of events that the owner of the queue is waiting to receive
    /// to an optional predicate. If an event of one of these types is enqueued, then
    /// if there is no predicate, or if there is a predicate and evaluates to true, then
    /// the event is received, else the event is deferred.
    /// </summary>
    private Dictionary<Type, Func<Event, bool>> EventWaitTypes;

    /// <summary>
    /// Task completion source that contains the event obtained using an explicit receive.
    /// </summary>
    private TaskCompletionSource<Event> ReceiveCompletionSource;

    /// <summary>
    /// Checks if the queue is accepting new events.
    /// </summary>
    private bool IsClosed;

    /// <summary>
    /// The size of the queue.
    /// </summary>
    public int Size => Bag.Count;

    /// <summary>
    /// Checks if an event has been raised.
    /// </summary>
    public bool IsEventRaised => RaisedEvent != default;
    
    /// <summary>
    /// Initializes a new instance of the <see cref="EventBag"/> class.
    /// </summary>
    internal EventBag(IStateMachineManager stateMachineManager, StateMachine stateMachine)
    {
        StateMachineManager = stateMachineManager;
        StateMachine = stateMachine;
        Bag = new List<(Event, EventInfo)>();
        EventWaitTypes = new Dictionary<Type, Func<Event, bool>>();
        IsClosed = false;
    }
    
    /// <inheritdoc/>
    public EnqueueStatus Enqueue(Event e, EventInfo info)
    {
        if (IsClosed)
        {
            return EnqueueStatus.Dropped;
        }

        if (EventWaitTypes.TryGetValue(e.GetType(), out var predicate) &&
            (predicate is null || predicate(e)))
        {
            EventWaitTypes.Clear();
            StateMachineManager.OnReceiveEvent(e, info);
            ReceiveCompletionSource.SetResult(e);
            return EnqueueStatus.EventHandlerRunning;
        }

        StateMachineManager.OnEnqueueEvent(e, info);
        Bag.Add((e, info));

        if (!StateMachineManager.IsEventHandlerRunning)
        {
            if (TryDequeueEvent(true).e is null)
            {
                return EnqueueStatus.NextEventUnavailable;
            }
            else
            {
                StateMachineManager.IsEventHandlerRunning = true;
                return EnqueueStatus.EventHandlerNotRunning;
            }
        }

        return EnqueueStatus.EventHandlerRunning;
    }
    
    /// <inheritdoc/>
        public (DequeueStatus status, Event e, EventInfo info) Dequeue()
        {
            // Try to get the raised event, if there is one. Raised events
            // have priority over the events in the inbox.
            if (RaisedEvent != default)
            {
                if (StateMachineManager.IsEventIgnored(RaisedEvent.e, RaisedEvent.info))
                {
                    // TODO: should the user be able to raise an ignored event?
                    // The raised event is ignored in the current state.
                    RaisedEvent = default;
                }
                else
                {
                    var raisedEvent = RaisedEvent;
                    RaisedEvent = default;
                    return (DequeueStatus.Raised, raisedEvent.e, raisedEvent.info);
                }
            }

            var hasDefaultHandler = StateMachineManager.IsDefaultHandlerAvailable();
            if (hasDefaultHandler)
            {
                StateMachine.Runtime.NotifyDefaultEventHandlerCheck(StateMachine);
            }

            // Try to dequeue the next event, if there is one.
            var (e, info) = TryDequeueEvent();
            if (e != null)
            {
                // Found next event that can be dequeued.
                return (DequeueStatus.Success, e, info);
            }

            // No event can be dequeued, so check if there is a default event handler.
            if (!hasDefaultHandler)
            {
                // There is no default event handler installed, so do not return an event.
                StateMachineManager.IsEventHandlerRunning = false;
                return (DequeueStatus.NotAvailable, null, null);
            }

            // TODO: check op-id of default event.
            // A default event handler exists.
            var stateName = StateMachine.CurrentState.GetType().Name;
            var eventOrigin = new EventOriginInfo(StateMachine.Id, StateMachine.GetType().FullName, stateName);
            return (DequeueStatus.Default, DefaultEvent.Instance, new EventInfo(DefaultEvent.Instance, eventOrigin, StateMachine.VectorTime));
        }
        
    /// <summary>
    /// Dequeues the next event and its metadata, if there is one available, else returns null.
    /// </summary>
    private (Event e, EventInfo info) TryDequeueEvent(bool checkOnly = false)
    {
        (Event e, EventInfo info) availableEvent = default;
        List<(Event, EventInfo)> deferredEvents = new List<(Event, EventInfo)>();

        // Iterates through the events and metadata in the inbox.
        while (availableEvent == default)
        {
            if (Bag.Count == 0)
            {
                break;
            }
            var index = StateMachine.RandomInteger(Bag.Count);
            availableEvent = Bag[index];

            if (StateMachineManager.IsEventIgnored(availableEvent.e, availableEvent.info))
            {
                if (!checkOnly)
                {
                    // Removes an ignored event.
                    Bag.Remove(availableEvent);
                    availableEvent = default;
                }

                continue;
            }

            // Skips a deferred event.
            if (StateMachineManager.IsEventDeferred(availableEvent.Item1, availableEvent.Item2))
            {
                Bag.Remove(availableEvent);
                deferredEvents.Add(availableEvent);
                availableEvent = default;
            }
        }
        
        Bag.AddRange(deferredEvents);
        if (!checkOnly)
        {
            Bag.Remove(availableEvent);
        }

        return availableEvent;
    }
    
    /// <inheritdoc/>
    public void RaiseEvent(Event e)
    {
        var stateName = StateMachine.CurrentState.GetType().Name;
        var eventOrigin = new EventOriginInfo(StateMachine.Id, StateMachine.GetType().FullName, stateName);
        var info = new EventInfo(e, eventOrigin, StateMachine.VectorTime);
        RaisedEvent = (e, info);
        StateMachineManager.OnRaiseEvent(e, info);
    }

    /// <inheritdoc/>
    public Task<Event> ReceiveEventAsync(Type eventType, Func<Event, bool> predicate = null)
    {
        var eventWaitTypes = new Dictionary<Type, Func<Event, bool>>
        {
            { eventType, predicate }
        };

        return ReceiveEventAsync(eventWaitTypes);
    }

    /// <inheritdoc/>
    public Task<Event> ReceiveEventAsync(params Type[] eventTypes)
    {
        var eventWaitTypes = new Dictionary<Type, Func<Event, bool>>();
        foreach (var type in eventTypes)
        {
            eventWaitTypes.Add(type, null);
        }

        return ReceiveEventAsync(eventWaitTypes);
    }
    
    /// <inheritdoc/>
    public Task<Event> ReceiveEventAsync(params Tuple<Type, Func<Event, bool>>[] events)
    {
        var eventWaitTypes = new Dictionary<Type, Func<Event, bool>>();
        foreach (var e in events)
        {
            eventWaitTypes.Add(e.Item1, e.Item2);
        }

        return ReceiveEventAsync(eventWaitTypes);
    }
    
    /// <summary>
    /// Waits for an event to be enqueued.
    /// </summary>
    private Task<Event> ReceiveEventAsync(Dictionary<Type, Func<Event, bool>> eventWaitTypes)
    {
        StateMachine.Runtime.NotifyReceiveCalled(StateMachine);

        (Event e, EventInfo info) receivedEvent = default;
        int index = 0;
        while (index < Bag.Count)
        {
            receivedEvent = Bag[index];
            // Dequeue the first event that the caller waits to receive, if there is one in the queue.
            if (eventWaitTypes.TryGetValue(receivedEvent.e.GetType(), out var predicate) &&
                (predicate is null || predicate(receivedEvent.e)))
            {
                Bag.Remove(receivedEvent);
                break;
            }

            index++;
        }

        if (receivedEvent == default)
        {
            ReceiveCompletionSource = new TaskCompletionSource<Event>();
            EventWaitTypes = eventWaitTypes;
            StateMachineManager.OnWaitEvent(EventWaitTypes.Keys);
            return ReceiveCompletionSource.Task;
        }

        StateMachineManager.OnReceiveEventWithoutWaiting(receivedEvent.e, receivedEvent.info);
        return Task.FromResult(receivedEvent.e);
    }
    
    /// <inheritdoc/>
    public void Close()
    {
        IsClosed = true;
    }
    
    /// <summary>
    /// Disposes the queue resources.
    /// </summary>
    private void Dispose(bool disposing)
    {
        if (!disposing)
        {
            return;
        }

        foreach (var (e, info) in Bag)
        {
            StateMachineManager.OnDropEvent(e, info);
        }

        Bag.Clear();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    
}