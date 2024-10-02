// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PChecker.StateMachines.Events;
using PChecker.IO.Logging;
using PChecker.PRuntime;

namespace PChecker.StateMachines.Logging
{
    /// <summary>
    /// Manages the installed <see cref="TextWriter"/> and all registered
    /// <see cref="IControlledRuntimeLog"/> objects.
    /// </summary>
    public sealed class LogWriter
    {
        /// <summary>
        /// The set of registered log writers.
        /// </summary>
        private readonly HashSet<IControlledRuntimeLog> Logs;

        /// <summary>
        /// Used to log messages.
        /// </summary>
        internal TextWriter Logger { get; private set; }

        /// <summary>
        /// Used to log json messages.
        /// </summary>
        public JsonWriter JsonLogger { get; private set; }

        /// <summary>
        /// Is checker strategy statistical.
        /// </summary>
        private bool isStatisticalStrategy;

        /// <summary>
        /// Initializes a new instance of the <see cref="LogWriter"/> class.
        /// </summary>
        internal LogWriter(CheckerConfiguration checkerConfiguration)
        {
            Logs = new HashSet<IControlledRuntimeLog>();

            if (checkerConfiguration.IsVerbose)
            {
                GetOrCreateTextLog();
            }
            else
            {
                Logger = TextWriter.Null;
            }

            isStatisticalStrategy = checkerConfiguration.SchedulingStrategy is "statistical";
        }

        /// <summary>
        /// Logs that the specified state machine has been created.
        /// </summary>
        /// <param name="id">The id of the state machine that has been created.</param>
        /// <param name="creatorName">The name of the creator, or null.</param>
        /// <param name="creatorType">The type of the creator, or null.</param>
        public void LogCreateStateMachine(StateMachineId id, string creatorName, string creatorType)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnCreateStateMachine(id, creatorName, creatorType);
                }
            }
        }

        /// <summary>
        /// Logs that the specified state machine executes an action.
        /// </summary>
        /// <param name="id">The id of the state machine executing the action.</param>
        /// <param name="handlingStateName">The state that declared this action (can be different from currentStateName in the case of PushStates.</param>
        /// <param name="currentStateName">The state name, if the state machine is a state machine and a state exists, else null.</param>
        /// <param name="actionName">The name of the action being executed.</param>
        public void LogExecuteAction(StateMachineId id, string handlingStateName, string currentStateName, string actionName)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnExecuteAction(id, handlingStateName, currentStateName, actionName);
                }
            }
        }

        /// <summary>
        /// Logs that the specified event is sent to a target state machine.
        /// </summary>
        /// <param name="targetStateMachineId">The id of the target state machine.</param>
        /// <param name="senderName">The name of the sender, if any.</param>
        /// <param name="senderType">The type of the sender, if any.</param>
        /// <param name="senderState">The state name, if the sender is a state machine, else null.</param>
        /// <param name="e">The event being sent.</param>
        /// <param name="opGroupId">The id used to identify the send operation.</param>
        /// <param name="isTargetHalted">Is the target state machine halted.</param>
        public void LogSendEvent(StateMachineId targetStateMachineId, string senderName, string senderType, string senderState,
            Event e, bool isTargetHalted)
        {
            if (!isStatisticalStrategy)
            {
                IControlledRuntimeLog timeLogger = null;
                foreach (var log in Logs)
                {
                    if (log.GetType().ToString().Equals("Plang.CSharpRuntime.PTimeLogger"))
                    {
                        timeLogger = log;
                        break;
                    }
                }

                if (timeLogger is not null)
                {
                    Logs.Remove(timeLogger);
                }
            }

            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnSendEvent(targetStateMachineId, senderName, senderType, senderState, e, isTargetHalted);
                }
            }
        }

        /// <summary>
        /// Logs that the specified state machine raises an event.
        /// </summary>
        /// <param name="id">The id of the state machine raising the event.</param>
        /// <param name="stateName">The state name, if the state machine is a state machine and a state exists, else null.</param>
        /// <param name="e">The event being raised.</param>
        public void LogRaiseEvent(StateMachineId id, string stateName, Event e)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnRaiseEvent(id, stateName, e);
                }
            }
        }

        /// <summary>
        /// Logs that the specified event is about to be enqueued to an state machine.
        /// </summary>
        /// <param name="id">The id of the state machine that the event is being enqueued to.</param>
        /// <param name="e">The event being enqueued.</param>
        public void LogEnqueueEvent(StateMachineId id, Event e)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnEnqueueEvent(id, e);
                }
            }
        }

        /// <summary>
        /// Logs that the specified event is dequeued by an state machine.
        /// </summary>
        /// <param name="id">The id of the state machine that the event is being dequeued by.</param>
        /// <param name="stateName">The state name, if the state machine is a state machine and a state exists, else null.</param>
        /// <param name="e">The event being dequeued.</param>
        public void LogDequeueEvent(StateMachineId id, string stateName, Event e)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnDequeueEvent(id, stateName, e);
                }
            }
        }

        /// <summary>
        /// Logs that the specified event is received by an state machine.
        /// </summary>
        /// <param name="id">The id of the state machine that received the event.</param>
        /// <param name="stateName">The state name, if the state machine is a state machine and a state exists, else null.</param>
        /// <param name="e">The event being received.</param>
        /// <param name="wasBlocked">The state machine was waiting for one or more specific events,
        /// and <paramref name="e"/> was one of them.</param>
        public void LogReceiveEvent(StateMachineId id, string stateName, Event e, bool wasBlocked)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnReceiveEvent(id, stateName, e, wasBlocked);
                }
            }
        }

        /// <summary>
        /// Logs that the specified state machine waits to receive an event of a specified type.
        /// </summary>
        /// <param name="id">The id of the state machine that is entering the wait state.</param>
        /// <param name="stateName">The state name, if the state machine is a state machine and a state exists, else null.</param>
        /// <param name="eventType">The type of the event being waited for.</param>
        public void LogWaitEvent(StateMachineId id, string stateName, Type eventType)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnWaitEvent(id, stateName, eventType);
                }
            }
        }

        /// <summary>
        /// Logs that the specified state machine waits to receive an event of one of the specified types.
        /// </summary>
        /// <param name="id">The id of the state machine that is entering the wait state.</param>
        /// <param name="stateName">The state name, if the state machine is a state machine and a state exists, else null.</param>
        /// <param name="eventTypes">The types of the events being waited for, if any.</param>
        public void LogWaitEvent(StateMachineId id, string stateName, params Type[] eventTypes)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnWaitEvent(id, stateName, eventTypes);
                }
            }
        }

        /// <summary>
        /// Logs that the specified random result has been obtained.
        /// </summary>
        /// <param name="result">The random result (may be bool or int).</param>
        /// <param name="callerName">The name of the caller, if any.</param>
        /// <param name="callerType">The type of the caller, if any.</param>
        public void LogRandom(object result, string callerName, string callerType)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnRandom(result, callerName, callerType);
                }
            }
        }

        /// <summary>
        /// Logs that the specified state machine enters or exits a state.
        /// </summary>
        /// <param name="id">The id of the state machine entering or exiting the state.</param>
        /// <param name="stateName">The name of the state being entered or exited.</param>
        /// <param name="isEntry">If true, this is called for a state entry; otherwise, exit.</param>
        public void LogStateTransition(StateMachineId id, string stateName, bool isEntry)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnStateTransition(id, stateName, isEntry);
                }
            }
        }

        /// <summary>
        /// Logs that the specified state machine performs a goto state transition.
        /// </summary>
        /// <param name="id">The id of the state machine.</param>
        /// <param name="currentStateName">The name of the current state.</param>
        /// <param name="newStateName">The target state of the transition.</param>
        public void LogGotoState(StateMachineId id, string currentStateName, string newStateName)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnGotoState(id, currentStateName, newStateName);
                }
            }
        }

        /// <summary>
        /// Logs that the specified state machine has halted.
        /// </summary>
        /// <param name="id">The id of the state machine that has been halted.</param>
        /// <param name="inboxSize">Approximate size of the inbox.</param>
        public void LogHalt(StateMachineId id, int inboxSize)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnHalt(id, inboxSize);
                }
            }
        }

        /// <summary>
        /// Logs that the specified state machine is idle (there is nothing to dequeue) and the default
        /// event handler is about to be executed.
        /// </summary>
        /// <param name="id">The id of the state machine that the state will execute in.</param>
        /// <param name="stateName">The state name, if the state machine is a state machine and a state exists, else null.</param>
        public void LogDefaultEventHandler(StateMachineId id, string stateName)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnDefaultEventHandler(id, stateName);
                }
            }
        }

        /// <summary>
        /// Logs that the specified state machine handled a raised event.
        /// </summary>
        /// <param name="id">The id of the state machine handling the event.</param>
        /// <param name="stateName">The name of the current state.</param>
        /// <param name="e">The event being handled.</param>
        public void LogHandleRaisedEvent(StateMachineId id, string stateName, Event e)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnHandleRaisedEvent(id, stateName, e);
                }
            }
        }

        /// <summary>
        /// Logs that the specified event cannot be handled in the current state, its exit
        /// handler is executed and then the state is popped and any previous "current state"
        /// is reentered. This handler is called when that pop has been done.
        /// </summary>
        /// <param name="id">The id of the state machine that the pop executed in.</param>
        /// <param name="stateName">The state name, if the state machine is a state machine and a state exists, else null.</param>
        /// <param name="e">The event that cannot be handled.</param>
        public void LogPopStateUnhandledEvent(StateMachineId id, string stateName, Event e)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnPopStateUnhandledEvent(id, stateName, e);
                }
            }
        }

        /// <summary>
        /// Logs that the specified state machine throws an exception.
        /// </summary>
        /// <param name="id">The id of the state machine that threw the exception.</param>
        /// <param name="stateName">The state name, if the state machine is a state machine and a state exists, else null.</param>
        /// <param name="actionName">The name of the action being executed.</param>
        /// <param name="ex">The exception.</param>
        public void LogExceptionThrown(StateMachineId id, string stateName, string actionName, Exception ex)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnExceptionThrown(id, stateName, actionName, ex);
                }
            }
        }

        /// <summary>
        /// Logs that the specified OnException method is used to handle a thrown exception.
        /// </summary>
        /// <param name="id">The id of the state machine that threw the exception.</param>
        /// <param name="stateName">The state name, if the state machine is a state machine and a state exists, else null.</param>
        /// <param name="actionName">The name of the action being executed.</param>
        /// <param name="ex">The exception.</param>
        public void LogExceptionHandled(StateMachineId id, string stateName, string actionName, Exception ex)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnExceptionHandled(id, stateName, actionName, ex);
                }
            }
        }

        /// <summary>
        /// Logs that the specified monitor has been created.
        /// </summary>
        /// <param name="monitorType">The name of the type of the monitor that has been created.</param>
        public void LogCreateMonitor(string monitorType)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnCreateMonitor(monitorType);
                }
            }
        }

        /// <summary>
        /// Logs that the specified monitor executes an action.
        /// </summary>
        /// <param name="monitorType">Name of type of the monitor that is executing the action.</param>
        /// <param name="stateName">The name of the state in which the action is being executed.</param>
        /// <param name="actionName">The name of the action being executed.</param>
        public void LogMonitorExecuteAction(string monitorType, string stateName, string actionName)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnMonitorExecuteAction(monitorType, stateName, actionName);
                }
            }
        }

        /// <summary>
        /// Logs that the specified monitor is about to process an event.
        /// </summary>
        /// <param name="monitorType">Name of type of the monitor that will process the event.</param>
        /// <param name="stateName">The name of the state in which the event is being raised.</param>
        /// <param name="senderName">The name of the sender, if any.</param>
        /// <param name="senderType">The type of the sender, if any.</param>
        /// <param name="senderStateName">The name of the state the sender is in.</param>
        /// <param name="e">The event being processed.</param>
        public void LogMonitorProcessEvent(string monitorType, string stateName, string senderName,
            string senderType, string senderStateName, Event e)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnMonitorProcessEvent(monitorType, stateName, senderName, senderType, senderStateName, e);
                }
            }
        }

        /// <summary>
        /// Logs that the specified monitor raised an event.
        /// </summary>
        /// <param name="monitorType">Name of type of the monitor raising the event.</param>
        /// <param name="stateName">The name of the state in which the event is being raised.</param>
        /// <param name="e">The event being raised.</param>
        public void LogMonitorRaiseEvent(string monitorType, string stateName, Event e)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnMonitorRaiseEvent(monitorType, stateName, e);
                }
            }
        }

        /// <summary>
        /// Logs that the specified monitor enters or exits a state.
        /// </summary>
        /// <param name="monitorType">The name of the type of the monitor entering or exiting the state</param>
        /// <param name="stateName">The name of the state being entered or exited; if <paramref name="isInHotState"/>
        /// is not null, then the temperature is appended to the statename in brackets, e.g. "stateName[hot]".</param>
        /// <param name="isEntry">If true, this is called for a state entry; otherwise, exit.</param>
        /// <param name="isInHotState">If true, the monitor is in a hot state; if false, the monitor is in a cold state;
        /// else no liveness state is available.</param>
        public void LogMonitorStateTransition(string monitorType, string stateName, bool isEntry, bool? isInHotState)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnMonitorStateTransition(monitorType, stateName, isEntry, isInHotState);
                }
            }
        }

        /// <summary>
        /// Logs that the specified monitor has found an error.
        /// </summary>
        /// <param name="monitorType">The name of the type of the monitor.</param>
        /// <param name="stateName">The name of the current state.</param>
        /// <param name="isInHotState">If true, the monitor is in a hot state; if false, the monitor is in a cold state;
        /// else no liveness state is available.</param>
        public void LogMonitorError(string monitorType, string stateName, bool? isInHotState)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnMonitorError(monitorType, stateName, isInHotState);
                }
            }
        }

        /// <summary>
        /// Logs that the specified assertion failure has occurred.
        /// </summary>
        /// <param name="error">The text of the error.</param>
        public void LogAssertionFailure(string error)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnAssertionFailure(error);
                }
            }
        }

        /// <summary>
        /// Logs the specified scheduling strategy description.
        /// </summary>
        /// <param name="strategyName">The name of the strategy that was used.</param>
        /// <param name="description">More information about the scheduling strategy.</param>
        public void LogStrategyDescription(string strategyName, string description)
        {
            if (Logs.Count > 0)
            {
                foreach (var log in Logs)
                {
                    log.OnStrategyDescription(strategyName, description);
                }
            }
        }

        /// <summary>
        /// Use this method to notify all logs that the test schedule is complete.
        /// </summary>
        internal void LogCompletion()
        {
            foreach (var log in Logs)
            {
                log.OnCompleted();
            }
        }

        /// <summary>
        /// Returns all registered logs of type <typeparamref name="TStateMachineRuntimeLog"/>,
        /// if there are any.
        /// </summary>
        public IEnumerable<TStateMachineRuntimeLog> GetLogsOfType<TStateMachineRuntimeLog>()
            where TStateMachineRuntimeLog : IControlledRuntimeLog =>
            Logs.OfType<TStateMachineRuntimeLog>();

        /// <summary>
        /// Use this method to override the default <see cref="TextWriter"/> for logging messages.
        /// </summary>
        internal TextWriter SetLogger(TextWriter logger)
        {
            var prevLogger = Logger;
            if (logger == null)
            {
                Logger = TextWriter.Null;

                var textLog = GetLogsOfType<PCheckerLogTextFormatter>().FirstOrDefault();
                if (textLog != null)
                {
                    textLog.Logger = Logger;
                }
            }
            else
            {
                Logger = logger;

                // This overrides the original IsVerbose flag and creates a text logger anyway!
                var textLog = GetOrCreateTextLog();
                textLog.Logger = Logger;
            }

            return prevLogger;
        }

        /// <summary>
        /// Sets the JsonLogger to an instance created on runtime.
        /// </summary>
        /// <param name="jsonLogger">The jsonLogger instance created from runtime before running tests.</param>
        internal void SetJsonLogger(JsonWriter jsonLogger) => JsonLogger = jsonLogger;

        private PCheckerLogTextFormatter GetOrCreateTextLog()
        {
            var textLog = GetLogsOfType<PCheckerLogTextFormatter>().FirstOrDefault();
            if (textLog == null)
            {
                if (Logger == null)
                {
                    Logger = new ConsoleLogger();
                }

                textLog = new PCheckerLogTextFormatter
                {
                    Logger = Logger
                };

                Logs.Add(textLog);
            }

            return textLog;
        }

        /// <summary>
        /// Use this method to register an <see cref="IControlledRuntimeLog"/>.
        /// </summary>
        internal void RegisterLog(IControlledRuntimeLog log)
        {
            if (log == null)
            {
                throw new InvalidOperationException("Cannot register a null log.");
            }

            // Make sure we only have one text logger
            if (log is PCheckerLogTextFormatter a)
            {
                var textLog = GetLogsOfType<PCheckerLogTextFormatter>().FirstOrDefault();
                if (textLog != null)
                {
                    Logs.Remove(textLog);
                }

                if (Logger != null)
                {
                    a.Logger = Logger;
                }
            }

            // If log is or of subclass PCheckerLogJsonFormatter (i.e. when log is PCheckerLogJsonFormatter),
            // update the Writer reference to the JsonLogger instance defined within LogWriter.cs
            if (log is PCheckerLogJsonFormatter tempJsonFormatter)
            {
                tempJsonFormatter.Writer = JsonLogger;
            }

            Logs.Add(log);
        }

        /// <summary>
        /// Use this method to unregister a previously registered <see cref="IControlledRuntimeLog"/>.
        /// </summary>
        internal void RemoveLog(IControlledRuntimeLog log)
        {
            if (log != null)
            {
                Logs.Remove(log);
            }
        }
    }
}