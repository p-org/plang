// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.Serialization;

namespace PChecker.StateMachines.Events
{
    /// <summary>
    /// Contains an <see cref="Event"/>, and its associated metadata.
    /// </summary>
    [DataContract]
    internal class EventInfo
    {
        /// <summary>
        /// Event name.
        /// </summary>
        [DataMember]
        internal string EventName { get; private set; }

        /// <summary>
        /// Information regarding the event origin.
        /// </summary>
        [DataMember]
        internal EventOriginInfo OriginInfo { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventInfo"/> class.
        /// </summary>
        internal EventInfo(Event e)
        {
            EventName = e.GetType().FullName;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="EventInfo"/> class.
        /// </summary>
        internal EventInfo(Event e, EventOriginInfo originInfo)
            : this(e)
        {
            OriginInfo = originInfo;
        }
        
        /// <summary>
        /// Asserts that there must not be more than N instances of the
        /// event type in the inbox queue of the receiver.
        /// </summary>
        internal int Assert { get; set; }
    }
}