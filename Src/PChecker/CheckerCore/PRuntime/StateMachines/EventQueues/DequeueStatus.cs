// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace PChecker.StateMachines.EventQueues
{
    /// <summary>
    /// The status returned as the result of a dequeue operation.
    /// </summary>
    internal enum DequeueStatus
    {
        /// <summary>
        /// An event was successfully dequeued.
        /// </summary>
        Success = 0,

        /// <summary>
        /// The raised event was dequeued.
        /// </summary>
        Raised,

        /// <summary>
        /// The default event was dequeued.
        /// </summary>
        Default,

        /// <summary>
        /// No event available to dequeue.
        /// </summary>
        NotAvailable,

        /// <summary>
        /// No event available to dequeue at the current time step.
        /// </summary>
        Delayed
    }
}