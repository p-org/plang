using System;
using System.Linq;
using PChecker.Actors;
using PChecker.Actors.Events;
using PChecker.Actors.Logging;
using PChecker.PRuntime.Exceptions;

namespace PChecker.PRuntime
{
    /// <summary>
    ///     Formatter for the runtime log.
    /// </summary>
    public class PLogFormatter : ActorRuntimeLogTextFormatter
    {
        public PLogFormatter() : base()
        {
        }

        private string GetShortName(string name)
        {
            return name.Split('.').Last();
        }

        private string GetEventNameWithPayload(Event e)
        {
            if (e.GetType().Name.Contains("GotoStateEvent"))
            {
                return e.GetType().Name;
            }
            var pe = (PEvent)(e);
            var payload = pe.Payload == null ? "null" : pe.Payload.ToEscapedString();
            var msg = pe.Payload == null ? "" : $" with payload ({payload})";
            return $"{GetShortName(e.GetType().Name)}{msg}";
        }

        public override void OnStateTransition(ActorId id, string stateName, bool isEntry)
        {
            if (stateName.Contains("__InitState__") || id.Name.Contains("GodMachine"))
            {
                return;
            }

            base.OnStateTransition(id, GetShortName(stateName), isEntry);
        }

        public override void OnDefaultEventHandler(ActorId id, string stateName)
        {
            base.OnDefaultEventHandler(id, GetShortName(stateName));
        }

        public override void OnWaitEvent(ActorId id, string stateName, params Type[] eventTypes)
        {
            base.OnWaitEvent(id, GetShortName(stateName), eventTypes);
        }

        public override void OnWaitEvent(ActorId id, string stateName, Type eventType)
        {
            base.OnWaitEvent(id, GetShortName(stateName), eventType);
        }

        public override void OnMonitorStateTransition(string monitorType, string stateName, bool isEntry, bool? isInHotState)
        {
            if (stateName.Contains("__InitState__"))
            {
                return;
            }

            base.OnMonitorStateTransition(monitorType: GetShortName(monitorType), stateName: GetShortName(stateName), isEntry: isEntry, isInHotState: isInHotState);
        }

        public override void OnCreateActor(ActorId id, string creatorName, string creatorType)
        {
            if (id.Name.Contains("GodMachine") || creatorName.Contains("GodMachine"))
            {
                return;
            }

            base.OnCreateActor(id, GetShortName(creatorName), creatorType);
        }

        public override void OnMonitorProcessEvent(string monitorType, string stateName, string senderName, string senderType,
            string senderStateName, Event e)
        {
            var text = $"<MonitorLog> {GetShortName(monitorType)} is processing event '{GetEventNameWithPayload(e)}' in state '{stateName}'.";
            Logger.WriteLine(text);
        }

        public override void OnDequeueEvent(ActorId id, string stateName, Event e)
        {
            if (stateName.Contains("__InitState__") || id.Name.Contains("GodMachine"))
            {
                return;
            }

            stateName = GetShortName(stateName);
            var eventName = GetEventNameWithPayload(e);
            string text = null;
            if (stateName is null)
            {
                text = $"<DequeueLog> '{id}' dequeued event '{eventName}'.";
            }
            else
            {
                text = $"<DequeueLog> '{id}' dequeued event '{eventName}' in state '{stateName}'.";
            }

            Logger.WriteLine(text);
        }

        public override void OnRaiseEvent(ActorId id, string stateName, Event e)
        {
            stateName = GetShortName(stateName);
            var eventName = GetEventNameWithPayload(e);
            if (stateName.Contains("__InitState__") || id.Name.Contains("GodMachine") || eventName.Contains("GotoStateEvent"))
            {
                return;
            }

            string text = null;
            if (stateName is null)
            {
                text = $"<RaiseLog> '{id}' raised event '{eventName}'.";
            }
            else
            {
                text = $"<RaiseLog> '{id}' raised event '{eventName}' in state '{stateName}'.";
            }

            Logger.WriteLine(text);
        }

        public override void OnEnqueueEvent(ActorId id, Event e) {   }

        public override void OnReceiveEvent(ActorId id, string stateName, Event e, bool wasBlocked)
        {
            stateName = GetShortName(stateName);
            var eventName = GetEventNameWithPayload(e);
            string text = null;
            var unblocked = wasBlocked ? " and unblocked" : string.Empty;
            if (stateName is null)
            {
                text = $"<ReceiveLog> '{id}' dequeued event '{eventName}'{unblocked}.";
            }
            else
            {
                text = $"<ReceiveLog> '{id}' dequeued event '{eventName}'{unblocked} in state '{stateName}'.";
            }

            Logger.WriteLine(text);
        }

        public override void OnMonitorRaiseEvent(string monitorType, string stateName, Event e)
        {
            stateName = GetShortName(stateName);
            var eventName = GetEventNameWithPayload(e);
            var text = $"<MonitorLog> Monitor '{GetShortName(monitorType)}' raised event '{eventName}' in state '{stateName}'.";
            Logger.WriteLine(text);
        }

        public override void OnSendEvent(ActorId targetActorId, string senderName, string senderType, string senderStateName, Event e, Guid opGroupId, bool isTargetHalted)
        {
            senderStateName = GetShortName(senderStateName);
            var eventName = GetEventNameWithPayload(e);
            var opGroupIdMsg = opGroupId != Guid.Empty ? $" (operation group '{opGroupId}')" : string.Empty;
            var isHalted = isTargetHalted ? $" which has halted" : string.Empty;
            var sender = !string.IsNullOrEmpty(senderName) ? $"'{senderName}' in state '{senderStateName}'" : $"The runtime";
            var text = $"<SendLog> {sender} sent event '{eventName}' to '{targetActorId}'{isHalted}{opGroupIdMsg}.";
            Logger.WriteLine(text);
        }

        public override void OnGotoState(ActorId id, string currStateName, string newStateName)
        {
            if (currStateName.Contains("__InitState__") || id.Name.Contains("GodMachine"))
            {
                return;
            }

            base.OnGotoState(id, GetShortName(currStateName), GetShortName(newStateName));
        }

        public override void OnExecuteAction(ActorId id, string handlingStateName, string currentStateName, string actionName)
        {
        }

        public override void OnMonitorExecuteAction(string monitorType, string stateName, string actionName)
        {
        }

        public override void OnExceptionHandled(ActorId id, string stateName, string actionName, Exception ex)
        {
            if (ex is PNonStandardReturnException)
            {
                return;
            }
            base.OnExceptionHandled(id: id, stateName: GetShortName(stateName), actionName: actionName, ex: ex);
        }

        public override void OnExceptionThrown(ActorId id, string stateName, string actionName, Exception ex)
        {
            if (ex is PNonStandardReturnException)
            {
                return;
            }
            base.OnExceptionThrown(id: id, stateName: GetShortName(stateName), actionName: actionName, ex: ex);
        }

        public override void OnCreateMonitor(string monitorType)
        {
            base.OnCreateMonitor(GetShortName(monitorType));
        }

        public override void OnHandleRaisedEvent(ActorId id, string stateName, Event e)
        {
        }

        public override void OnRandom(object result, string callerName, string callerType)
        {
        }
    }
}