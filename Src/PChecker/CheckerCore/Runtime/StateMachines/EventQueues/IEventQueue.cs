// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using PChecker.Runtime.Events;

namespace PChecker.Runtime.StateMachines.EventQueues
{
    /// <summary>
    /// Interface of a queue of events.
    /// </summary>
    internal interface IEventQueue : IDisposable
    {
        /// <summary>
        /// The size of the queue.
        /// </summary>
        int Size { get; }

        /// <summary>
        /// Checks if an event has been raised.
        /// </summary>
        bool IsEventRaised { get; }

        /// <summary>
        /// Enqueues the specified event and its optional metadata.
        /// </summary>
        EnqueueStatus Enqueue(Event e, EventInfo info);

        /// <summary>
        /// Dequeues the next event, if there is one available.
        /// </summary>
        (DequeueStatus status, Event e, EventInfo info) Dequeue();

        /// <summary>
        /// Enqueues the specified raised event.
        /// </summary>
        void RaiseEvent(Event e);

        /// <summary>
        /// Waits to receive an event of the specified type that satisfies an optional predicate.
        /// </summary>
        Task<Event> ReceiveEventAsync(Type eventType, Func<Event, bool> predicate = null);

        /// <summary>
        /// Waits to receive an event of the specified types.
        /// </summary>
        Task<Event> ReceiveEventAsync(params Type[] eventTypes);

        /// <summary>
        /// Waits to receive an event of the specified types that satisfy the specified predicates.
        /// </summary>
        Task<Event> ReceiveEventAsync(params Tuple<Type, Func<Event, bool>>[] events);

        /// <summary>
        /// Closes the queue, which stops any further event enqueues.
        /// </summary>
        void Close();
    }
}