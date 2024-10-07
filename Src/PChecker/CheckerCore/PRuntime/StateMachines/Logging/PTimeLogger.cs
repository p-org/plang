// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using PChecker.StateMachines.Events;

namespace PChecker.StateMachines.Logging
{
    /// <summary>
    /// This class implements StateMachineRuntimeTimeLogCsvFormatter and generates log output in a CSV format with time
    /// and payload information included.
    /// </summary>
    public class PTimeLogger : ControlledRuntimeTimeLogCsvFormatter
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="PTimeLogger"/> class.
        /// </summary>
        public PTimeLogger() : base()
        {
        }


        /// <inheritdoc />
        public override void OnCreateStateMachine(StateMachineId id, string creatorName, string creatorType)
        {
        }

        /// <inheritdoc />
        public override void OnExecuteAction(StateMachineId id, string handlingStateName, string currentStateName, string actionName)
        {
        }

        /// <inheritdoc />
        public override void OnSendEvent(StateMachineId targetStateMachineId, string senderName, string senderType, string senderStateName, Event e,
         bool isTargetHalted)
        {

        }

        /// <inheritdoc />
        public override void OnRaiseEvent(StateMachineId id, string stateName, Event e)
        {
        }

        /// <inheritdoc />
        public override void OnEnqueueEvent(StateMachineId id, Event e)
        {
            var pe = (Event)e;
            var payload = pe.Payload == null ? "null" : pe.Payload.ToEscapedString().Replace(" ", "").Replace(",", "|");
            InMemoryLogger.WriteLine(e.EnqueueTime.GetTime() + ", enqueue," + e + "," + payload + "," + id);
        }

        /// <inheritdoc />
        public override void OnDequeueEvent(StateMachineId id, string stateName, Event e)
        {
            var pe = (Event)e;
            var payload = pe.Payload == null ? "null" : pe.Payload.ToEscapedString().Replace(" ", "").Replace(",", "|");
            InMemoryLogger.WriteLine(e.DequeueTime.GetTime() + ",Dequeue," + e + "," + payload + "," + id + "," + stateName + ",null");
        }

        /// <inheritdoc />
        public override void OnReceiveEvent(StateMachineId id, string stateName, Event e, bool wasBlocked)
        {
        }

        /// <inheritdoc />
        public override void OnWaitEvent(StateMachineId id, string stateName, Type eventType)
        {
        }

        /// <inheritdoc />
        public override void OnWaitEvent(StateMachineId id, string stateName, params Type[] eventTypes)
        {
        }

        /// <inheritdoc />
        public override void OnStateTransition(StateMachineId id, string stateName, bool isEntry)
        {
        }

        /// <inheritdoc />
        public override void OnGotoState(StateMachineId id, string currentStateName, string newStateName)
        {
        }

        /// <inheritdoc />
        public override void OnPushState(StateMachineId id, string currentStateName, string newStateName)
        {
        }

        /// <inheritdoc />
        public override void OnPopState(StateMachineId id, string currentStateName, string restoredStateName)
        {
        }

        /// <inheritdoc />
        public override void OnDefaultEventHandler(StateMachineId id, string stateName)
        {
        }

        /// <inheritdoc />
        public override void OnHalt(StateMachineId id, int inboxSize)
        {
        }

        /// <inheritdoc />
        public override void OnHandleRaisedEvent(StateMachineId id, string stateName, Event e)
        {
        }

        /// <inheritdoc />
        public override void OnPopStateUnhandledEvent(StateMachineId id, string stateName, Event e)
        {
        }

        /// <inheritdoc />
        public override void OnExceptionThrown(StateMachineId id, string stateName, string actionName, Exception ex)
        {
        }

        /// <inheritdoc />
        public override void OnExceptionHandled(StateMachineId id, string stateName, string actionName, Exception ex)
        {
        }

        /// <inheritdoc />
        public override void OnCreateMonitor(string monitorType)
        {
        }

        /// <inheritdoc />
        public override void OnMonitorExecuteAction(string monitorType, string stateName, string actionName)
        {
        }

        /// <inheritdoc />
        public override void OnMonitorProcessEvent(string monitorType, string stateName, string senderName, string senderType,
            string senderStateName, Event e)
        {
        }

        /// <inheritdoc />
        public override void OnMonitorRaiseEvent(string monitorType, string stateName, Event e)
        {
        }

        /// <inheritdoc />
        public override void OnMonitorStateTransition(string monitorType, string stateName, bool isEntry, bool? isInHotState)
        {
        }

        /// <inheritdoc />
        public override void OnMonitorError(string monitorType, string stateName, bool? isInHotState)
        {
        }

        /// <inheritdoc />
        public override void OnRandom(object result, string callerName, string callerType)
        {
        }

        /// <inheritdoc />
        public override void OnAssertionFailure(string error)
        {
        }

        /// <inheritdoc />
        public override void OnStrategyDescription(string strategyName, string description)
        {
        }
    }
}