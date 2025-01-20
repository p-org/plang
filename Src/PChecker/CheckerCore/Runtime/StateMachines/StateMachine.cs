// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using PChecker.Exceptions;
using PChecker.IO.Debugging;
using PChecker.Runtime.Events;
using PChecker.Runtime.Exceptions;
using PChecker.Runtime.Logging;
using PChecker.Runtime.StateMachines.EventQueues;
using PChecker.Runtime.StateMachines.Exceptions;
using PChecker.Runtime.StateMachines.Handlers;
using PChecker.Runtime.StateMachines.Managers;
using PChecker.Runtime.StateMachines.StateTransitions;
using PChecker.Runtime.Values;
using PChecker.SystematicTesting;
using EventInfo = PChecker.Runtime.Events.EventInfo;


namespace PChecker.Runtime.StateMachines
{
    /// <summary>
    /// Type that implements a state machine with states, state transitions and event handlers.
    /// </summary>
    public abstract class StateMachine
    {

        /// <summary>
        /// The runtime that executes this state machine.
        /// </summary>
        internal ControlledRuntime Runtime { get; private set; }

        /// <summary>
        /// Unique id that identifies this state machine.
        /// </summary>
        protected internal StateMachineId Id { get; private set; }

        /// <summary>
        /// Manages the state machine.
        /// </summary>
        internal IStateMachineManager Manager { get; private set; }

        /// <summary>
        /// The inbox of the state machine. Incoming events are enqueued here.
        /// Events are dequeued to be processed.
        /// </summary>
        private protected IEventQueue Inbox;
        
        /// <summary>
        /// Event inbox type.
        /// </summary>
        public string InboxType;
        
        /// <summary>
        /// Keeps track of state machine's current vector time.
        /// </summary>
        public VectorTime VectorTime;
        
        /// <summary>
        /// Cache of state machine types to a map of action names to action declarations.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, Dictionary<string, MethodInfo>> ActionCache =
            new ConcurrentDictionary<Type, Dictionary<string, MethodInfo>>();

        /// <summary>
        /// A set of lockable objects used to protect static initialization of the ActionCache while
        /// also enabling multithreaded initialization of different StateMachine types.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, object> ActionCacheLocks =
            new ConcurrentDictionary<Type, object>();
        
        /// <summary>
        /// Cache of state machine types to a set of all possible states types.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, HashSet<Type>> StateTypeCache =
            new ConcurrentDictionary<Type, HashSet<Type>>();

        /// <summary>
        /// Cache of state machine types to a set of all available state instances.
        /// </summary>
        private static readonly ConcurrentDictionary<Type, HashSet<State>> StateInstanceCache =
            new ConcurrentDictionary<Type, HashSet<State>>();

        /// <summary>
        /// A map from event type to EventHandlerDeclaration for those EventHandlerDeclarations that
        /// are not inheritable on the state stack.
        /// </summary>
        private Dictionary<Type, EventHandlerDeclaration> EventHandlerMap;

        /// <summary>
        /// This is just so we don't have to allocate an empty map more than once.
        /// </summary>
        private static readonly Dictionary<Type, EventHandlerDeclaration> EmptyEventHandlerMap = new Dictionary<Type, EventHandlerDeclaration>();

        /// <summary>
        /// Map from action names to cached action delegates for all states in this state machine.
        /// </summary>
        private readonly Dictionary<string, CachedDelegate> StateMachineActionMap;
        
        /// <summary>
        /// A cached array that contains a single event type.
        /// </summary>
        private static readonly Type[] SingleEventTypeArray = new Type[] { typeof(Event) };
        
        /// <summary>
        /// The current status of the state machine. It is marked volatile as
        /// the runtime can read it concurrently.
        /// </summary>
        private protected volatile Status CurrentStatus;
        
        /// <summary>
        /// Gets the name of the current state, if there is one.
        /// </summary>
        internal string CurrentStateName { get; private protected set; }
        
        /// <summary>
        /// Checks if the state machine is halted.
        /// </summary>
        internal bool IsHalted => CurrentStatus is Status.Halted;
        
        /// <summary>
        /// Checks if a default handler is available.
        /// </summary>
        internal bool IsDefaultHandlerAvailable { get; private set; }

        /// <summary>
        /// Newly created Transition that hasn't been returned from InvokeActionAsync yet.
        /// </summary>
        private Transition PendingTransition;

        /// <summary>
        /// Gets the <see cref="Type"/> of the current state.
        /// </summary>
        protected internal State CurrentState { get; private set; }
        
        /// <summary>
        /// The installed runtime logger.
        /// </summary>
        protected TextWriter Logger => Runtime.Logger;

        /// <summary>
        /// The installed runtime json logger.
        /// </summary>
        protected JsonWriter JsonLogger => Runtime.JsonLogger;
        
        protected IPValue gotoPayload;
        
        public List<string> creates = new List<string>();
        public string interfaceName;
        public List<string> receives = new List<string>();
        public PMachineValue self;
        public List<string> sends = new List<string>();
        
        protected virtual Event GetConstructorEvent(IPValue value)
        {
            throw new NotImplementedException();
        }
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="creator"></param>
        /// <param name="payload"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public PMachineValue CreateInterface<T>(StateMachine creator, IPValue payload = null)
            where T : PMachineValue
        {
            var createdInterface = PModule.linkMap[creator.interfaceName][typeof(T).Name];
            Assert(creates.Contains(createdInterface),
                $"Machine {GetType().Name} cannot create interface {createdInterface}, not in its creates set");
            var createMachine = PModule.interfaceDefinitionMap[createdInterface];
            var machineId = CreateStateMachine(createMachine, createdInterface.Substring(2),
                GetConstructorEvent(payload));
            return new PMachineValue(machineId, PInterfaces.GetPermissions(createdInterface));
        }
        

        /// <summary>
        /// 
        /// </summary>
        /// <param name="param"></param>
        /// <returns></returns>
        /// <exception cref="PInternalException"></exception>
        public IPValue TryRandom(IPValue param)
        {
            switch (param)
            {
                case PInt maxValue:
                {
                    Assert(maxValue <= 10000, $"choose expects a parameter with at most 10000 choices, got {maxValue} choices instead.");
                    return (PInt)RandomInteger(maxValue);
                }

                case PSeq seq:
                {
                    Assert(seq.Any(), "Trying to choose from an empty sequence!");
                    Assert(seq.Count <= 10000, $"choose expects a parameter with at most 10000 choices, got {seq.Count} choices instead.");
                    return seq[RandomInteger(seq.Count)];
                }
                case PSet set:
                {
                    Assert(set.Any(), "Trying to choose from an empty set!");
                    Assert(set.Count <= 10000, $"choose expects a parameter with at most 10000 choices, got {set.Count} choices instead.");
                    return set.ElementAt(RandomInteger(set.Count));
                }
                case PMap map:
                {
                    Assert(map.Any(), "Trying to choose from an empty map!");
                    Assert(map.Keys.Count <= 10000, $"choose expects a parameter with at most 10000 choices, got {map.Keys.Count} choices instead.");
                    return map.Keys.ElementAt(RandomInteger(map.Keys.Count));
                }
                default:
                    throw new PInternalException("This is an unexpected (internal) P exception. Please report to the P Developers");
            }
        }

        public void LogLine(string message)
        {
            Logger.WriteLine($"<PrintLog> {message}");

            // Log message to JSON output
            JsonLogger.AddLogType(JsonWriter.LogType.Print);
            JsonLogger.AddLog(message);
            JsonLogger.AddToLogs(updateVcMap: false);
        }

        public void Log(string message)
        {
            Logger.Write($"{message}");
        }

        public void Announce(Event ev, object payload = null)
        {
            Assert(ev != null, "Machine cannot announce a null event");
            if (ev is PHalt)
            {
                ev = HaltEvent.Instance;
            }

            var oneArgConstructor = ev.GetType().GetConstructors().First(x => x.GetParameters().Length > 0);
            var @event = (Event)oneArgConstructor.Invoke(new[] { payload });
            var pText = payload == null ? "" : $" with payload {((IPValue)payload).ToEscapedString()}";

            Logger.WriteLine($"<AnnounceLog> '{Id}' announced event '{ev.GetType().Name}'{pText}.");

            // Log message to JSON output
            JsonLogger.AddLogType(JsonWriter.LogType.Announce);
            JsonLogger.LogDetails.Id = $"{Id}";
            JsonLogger.LogDetails.Event = ev.GetType().Name;
            if (payload != null)
            {
                JsonLogger.LogDetails.Payload = ((IPValue)payload).ToDict();
            }
            JsonLogger.AddLog($"{Id} announced event {ev.GetType().Name}{pText}.");
            JsonLogger.AddToLogs(updateVcMap: true);

            AnnounceInternal(@event);
        }

        private void AnnounceInternal(Event ev)
        {
            Assert(ev != null, "cannot send a null event");
            if (!PModule.monitorMap.ContainsKey(interfaceName))
            {
                return;
            }

            foreach (var monitor in PModule.monitorMap[interfaceName])
            {
                if (PModule.monitorObserves[monitor.Name].Contains(ev.GetType().Name))
                {
                    Monitor(monitor, ev);
                }
            }
        }
         
        /// <summary>
        /// Initializes a new instance of the <see cref="StateMachine"/> class.
        /// </summary>
        protected StateMachine()
            : base()
        {
            CurrentStatus = Status.Active;
            CurrentStateName = default;
            IsDefaultHandlerAvailable = false;
            EventHandlerMap = EmptyEventHandlerMap;
            StateMachineActionMap = new Dictionary<string, CachedDelegate>();
        }
        
        /// <summary>
        /// Configures the state machine.
        /// </summary>
        internal void Configure(ControlledRuntime runtime, StateMachineId id, IStateMachineManager manager, IEventQueue inbox)
        {
            Runtime = runtime;
            Id = id;
            Manager = manager;
            Inbox = inbox;
            VectorTime = new VectorTime(Id);
        }
        
        /// <summary>
        /// Returns a nondeterministic boolean choice, that can be
        /// controlled during analysis or testing.
        /// </summary>
        /// <returns>The controlled nondeterministic choice.</returns>
        public bool RandomBoolean() => Runtime.GetNondeterministicBooleanChoice(2, Id.Name, Id.Type);

        /// <summary>
        /// Returns a nondeterministic boolean choice, that can be
        /// controlled during analysis or testing. The value is used
        /// to generate a number in the range [0..maxValue), where 0
        /// triggers true.
        /// </summary>
        /// <param name="maxValue">The max value.</param>
        /// <returns>The controlled nondeterministic choice.</returns>
        public bool RandomBoolean(int maxValue) =>
            Runtime.GetNondeterministicBooleanChoice(maxValue, Id.Name, Id.Type);

        /// <summary>
        /// Returns a nondeterministic integer, that can be controlled during
        /// analysis or testing. The value is used to generate an integer in
        /// the range [0..maxValue).
        /// </summary>
        /// <param name="maxValue">The max value.</param>
        /// <returns>The controlled nondeterministic integer.</returns>
        public int RandomInteger(int maxValue) =>
            Runtime.GetNondeterministicIntegerChoice(maxValue, Id.Name, Id.Type);
        
        public int RandomInteger(int minValue, int maxValue)
        {
            return minValue + RandomInteger(maxValue - minValue);
        }

        /// <summary>
        /// Invokes the specified monitor with the specified <see cref="Event"/>.
        /// </summary>
        /// <typeparam name="T">Type of the monitor.</typeparam>
        /// <param name="e">Event to send to the monitor.</param>
        protected void Monitor<T>(Event e) => Monitor(typeof(T), e);

        /// <summary>
        /// Invokes the specified monitor with the specified event.
        /// </summary>
        /// <param name="type">Type of the monitor.</param>
        /// <param name="e">The event to send.</param>
        protected void Monitor(Type type, Event e)
        {
            Assert(e != null, "{0} is sending a null event.", Id);
            Runtime.Monitor(type, e, Id.Name, Id.Type, CurrentStateName);
        }
        
        /// <summary>
        /// Checks if the assertion holds, and if not, throws an <see cref="AssertionFailureException"/> exception.
        /// </summary>
        public void Assert(bool predicate) => Runtime.Assert(predicate);

        /// <summary>
        /// Checks if the assertion holds, and if not, throws an <see cref="AssertionFailureException"/> exception.
        /// </summary>
        public void Assert(bool predicate, string s, object arg0) =>
            Runtime.Assert(predicate, s, arg0);

        /// <summary>
        /// Checks if the assertion holds, and if not, throws an <see cref="AssertionFailureException"/> exception.
        /// </summary>
        public void Assert(bool predicate, string s, object arg0, object arg1) =>
            Runtime.Assert(predicate, s, arg0, arg1);

        /// <summary>
        /// Checks if the assertion holds, and if not, throws an <see cref="AssertionFailureException"/> exception.
        /// </summary>
        public void Assert(bool predicate, string s, object arg0, object arg1, object arg2) =>
            Runtime.Assert(predicate, s, arg0, arg1, arg2);

        /// <summary>
        /// Checks if the assertion holds, and if not, throws an <see cref="AssertionFailureException"/> exception.
        /// </summary>
        public void Assert(bool predicate, string s, params object[] args) =>
            Runtime.Assert(predicate, s, args);
        
        /// <summary>
        /// Asynchronous callback that is invoked when the state machine is initialized with an optional event.
        /// </summary>
        /// <param name="initialEvent">Optional event used for initialization.</param>
        /// <returns>Task that represents the asynchronous operation.</returns>
        protected virtual Task OnInitializeAsync(Event initialEvent) => Task.CompletedTask;

        /// <summary>
        /// Asynchronous callback that is invoked when the state machine successfully dequeues
        /// an event from its inbox. This method is not called when the dequeue happens
        /// via a receive statement.
        /// </summary>
        /// <param name="e">The event that was dequeued.</param>
        protected virtual Task OnEventDequeuedAsync(Event e) => Task.CompletedTask;
        
        /// <summary>
        /// Asynchronous callback that is invoked when the state machine finishes handling a dequeued
        /// event, unless the handler of the dequeued event caused the state machine to halt (either
        /// normally or due to an exception). The state machine will either become idle or dequeue
        /// the next event from its inbox.
        /// </summary>
        /// <param name="e">The event that was handled.</param>
        protected virtual Task OnEventHandledAsync(Event e) => Task.CompletedTask;

        /// <summary>
        /// Asynchronous callback that is invoked when the state machine receives an event that
        /// it is not prepared to handle. The callback is invoked first, after which the
        /// state machine will necessarily throw an <see cref="UnhandledEventException"/>
        /// </summary>
        /// <param name="e">The event that was unhandled.</param>
        /// <param name="state">The state when the event was dequeued.</param>
        protected Task OnEventUnhandledAsync(Event e, string state) => Task.CompletedTask;

        /// <summary>
        /// Asynchronous callback that is invoked when the state machine handles an exception.
        /// </summary>
        /// <param name="ex">The exception thrown by the state machine.</param>
        /// <param name="e">The event being handled when the exception was thrown.</param>
        /// <returns>The action that the runtime should take.</returns>
        protected Task OnExceptionHandledAsync(Exception ex, Event e) => Task.CompletedTask;
        
        /// <summary>
        /// Asynchronous callback that is invoked when the state machine halts.
        /// </summary>
        /// <param name="e">The event being handled when the state machine halted.</param>
        /// <returns>Task that represents the asynchronous operation.</returns>
        protected Task OnHaltAsync(Event e) => Task.CompletedTask;
        
        /// <summary>
        /// Halts the state machine.
        /// </summary>
        /// <param name="e">The event being handled when the state machine halts.</param>
        private protected Task HaltAsync(Event e)
        {
            CurrentStatus = Status.Halted;

            // Close the inbox, which will stop any subsequent enqueues.
            Inbox.Close();

            Runtime.LogWriter.LogHalt(Id, Inbox.Size);

            // Dispose any held resources.
            Inbox.Dispose();

            // Invoke user callback.
            return OnHaltAsync(e);
        }
        
        /// <summary>
        /// Initializes the state machine with the specified optional event.
        /// </summary>
        /// <param name="initialEvent">Optional event used for initialization.</param>
        internal async Task InitializeAsync(Event initialEvent)
        {
            // Invoke the custom initializer, if there is one.
            await InvokeUserCallbackAsync(UserCallbackType.OnInitialize, initialEvent);

            // Execute the entry action of the start state, if there is one.
            await ExecuteCurrentStateOnEntryAsync(initialEvent);
            if (CurrentStatus is Status.Halting)
            {
                await HaltAsync(initialEvent);
            }
        }
        
        /// <summary>
        /// An exception filter that calls,
        /// which can choose to fast-fail the app to get a full dump.
        /// </summary>
        /// <param name="action">The action being executed when the failure occurred.</param>
        /// <param name="ex">The exception being tested.</param>
        private protected bool InvokeOnFailureExceptionFilter(CachedDelegate action, Exception ex)
        {
            // This is called within the exception filter so the stack has not yet been unwound.
            // If the call does not fail-fast, return false to process the exception normally.
            Runtime.RaiseOnFailureEvent(new ActionExceptionFilterException(action.MethodInfo.Name, ex));
            return false;
        }
        
        /// <summary>
        /// Tries to handle an exception thrown during an action invocation.
        /// </summary>
        private protected Task TryHandleActionInvocationExceptionAsync(Exception ex, string actionName)
        {
            var innerException = ex;
            while (innerException is TargetInvocationException)
            {
                innerException = innerException.InnerException;
            }

            if (innerException is AggregateException)
            {
                innerException = innerException.InnerException;
            }

            if (innerException is ExecutionCanceledException || innerException is TaskSchedulerException)
            {
                CurrentStatus = Status.Halted;
                Debug.WriteLine($"<Exception> {innerException.GetType().Name} was thrown from {Id}.");
            }
            else
            {
                // Reports the unhandled exception.
                ReportUnhandledException(innerException, actionName);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Creates a new state machine of the specified type and name, and with the specified
        /// optional <see cref="Event"/>. This <see cref="Event"/> can only be used to
        /// access its payload, and cannot be handled.
        /// </summary>
        /// <param name="type">Type of the state machine.</param>
        /// <param name="name">Optional name used for logging.</param>
        /// <param name="initialEvent">Optional initialization event.</param>
        /// <param name="opGroupId">Optional id that can be used to identify this operation.</param>
        /// <returns>The unique state machine id.</returns>
        protected StateMachineId CreateStateMachine(Type type, string name, Event initialEvent = null) =>
            Runtime.CreateStateMachine(null, type, name, initialEvent, this);
        
        
        /// <summary>
        /// Sends an asynchronous <see cref="Event"/> to a target.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="e">The event to send.</param>
        /// <param name="opGroupId">Optional id that can be used to identify this operation.</param>
        public void SendEvent(PMachineValue target, Event ev)
        {
            Assert(ev != null, "Machine cannot send a null event");
            Assert(target != null, "Machine in send cannot be null");
            Assert(sends.Contains(ev.GetType().Name),
                $"Event {ev.GetType().Name} is not in the sends set of the Machine {GetType().Name}");
            Assert(target.Permissions.Contains(ev.GetType().Name),
                $"Event {ev.GetType().Name} is not in the permissions set of the target machine");
            AnnounceInternal(ev);
            // Update vector clock
            VectorTime.Increment();
            BehavioralObserver.AddToCurrentTimeline(ev, BehavioralObserver.EventType.SEND, VectorTime);
            Runtime.SendEvent(target.Id, ev, this);
        }
        
        /// <summary>
        /// Waits to receive an <see cref="Event"/> of the specified type
        /// that satisfies an optional predicate.
        /// </summary>
        /// <param name="eventType">The event type.</param>
        /// <param name="predicate">The optional predicate.</param>
        /// <returns>The received event.</returns>
        public Task<Event> ReceiveEventAsync(Type eventType, Func<Event, bool> predicate = null)
        {
            Assert(CurrentStatus is Status.Active, "{0} invoked ReceiveEventAsync while halting.", Id);
            Runtime.NotifyReceiveCalled(this);
            return Inbox.ReceiveEventAsync(eventType, predicate);
        }
        
        /// <summary>
        /// Waits to receive an <see cref="Event"/> of the specified types.
        /// </summary>
        /// <param name="eventTypes">The event types to wait for.</param>
        /// <returns>The received event.</returns>
        public Task<Event> ReceiveEventAsync(params Type[] eventTypes)
        {
            Assert(CurrentStatus is Status.Active, "{0} invoked ReceiveEventAsync while halting.", Id);
            Runtime.NotifyReceiveCalled(this);
            return Inbox.ReceiveEventAsync(eventTypes);
        }
        
        /// <summary>
        /// Waits to receive an <see cref="Event"/> of the specified types
        /// that satisfy the specified predicates.
        /// </summary>
        /// <param name="events">Event types and predicates.</param>
        /// <returns>The received event.</returns>
        public Task<Event> ReceiveEventAsync(params Tuple<Type, Func<Event, bool>>[] events)
        {
            Assert(CurrentStatus is Status.Active, "{0} invoked ReceiveEventAsync while halting.", Id);
            Runtime.NotifyReceiveCalled(this);
            return Inbox.ReceiveEventAsync(events);
        }
        
        /// <summary>
        /// Runs the event handler. The handler terminates if there is no next
        /// event to process or if the state machine has halted.
        /// </summary>
        internal async Task RunEventHandlerAsync()
        {
            Event lastDequeuedEvent = null;
            while (CurrentStatus != Status.Halted && Runtime.IsRunning)
            {
                (var status, var e, var info) = Inbox.Dequeue();
                
                if (status is DequeueStatus.Success)
                {
                    // Update state machine vector clock
                    VectorTime.Merge(info.VectorTime);
                    BehavioralObserver.AddToCurrentTimeline(e, BehavioralObserver.EventType.DEQUEUE, VectorTime);
                    
                    // Notify the runtime for a new event to handle. This is only used
                    // during bug-finding and operation bounding, because the runtime
                    // has to schedule an state machine when a new operation is dequeued.
                    Runtime.NotifyDequeuedEvent(this, e, info);
                    await InvokeUserCallbackAsync(UserCallbackType.OnEventDequeued, e);
                    lastDequeuedEvent = e;
                }
                else if (status is DequeueStatus.Raised)
                {
                    // Only supported by types (e.g. StateMachine) that allow
                    // the user to explicitly raise events.
                    Runtime.NotifyHandleRaisedEvent(this, e);
                }
                else if (status is DequeueStatus.Default)
                {
                    Runtime.LogWriter.LogDefaultEventHandler(Id, CurrentStateName);

                    // If the default event was dequeued, then notify the runtime.
                    // This is only used during bug-finding, because the runtime must
                    // instrument a scheduling point between default event handlers.
                    Runtime.NotifyDefaultEventDequeued(this);
                }
                else if (status is DequeueStatus.NotAvailable)
                {
                    // Terminate the handler as there is no event available.
                    break;
                }

                if (CurrentStatus is Status.Active)
                {
                    // Handles the next event, if the state machine is not halted.
                    await HandleEventAsync(e);
                }

                if (!Inbox.IsEventRaised && lastDequeuedEvent != null && CurrentStatus != Status.Halted)
                {
                    // Inform the user that the state machine handled the dequeued event.
                    await InvokeUserCallbackAsync(UserCallbackType.OnEventHandled, lastDequeuedEvent);
                    lastDequeuedEvent = null;
                }

                if (CurrentStatus is Status.Halting)
                {
                    // If the current status is halting, then halt the state machine.
                    await HaltAsync(e);
                }
            }
        }

        /// <summary>
        /// Invokes the specified event handler user callback.
        /// </summary>
        private protected async Task InvokeUserCallbackAsync(string callbackType, Event e, string currentState = default)
        {
            try
            {
                Task task = null;
                if (callbackType is UserCallbackType.OnInitialize)
                {
                    task = OnInitializeAsync(e);
                }
                else if (callbackType is UserCallbackType.OnEventDequeued)
                {
                    task = OnEventDequeuedAsync(e);
                }
                else if (callbackType is UserCallbackType.OnEventHandled)
                {
                    task = OnEventHandledAsync(e);
                }
                else if (callbackType is UserCallbackType.OnEventUnhandled)
                {
                    task = OnEventUnhandledAsync(e, currentState);
                }

                Runtime.NotifyWaitTask(this, task);
                await task;
            }
            catch (Exception ex) when (OnExceptionHandler(ex, callbackType, e))
            {
                // User handled the exception.
                await OnExceptionHandledAsync(ex, e);
            }
            catch (Exception ex)
            {
                // Reports the unhandled exception.
                await TryHandleActionInvocationExceptionAsync(ex, callbackType);
            }
        }
        
        /// <summary>
        /// Invokes the specified action delegate.
        /// </summary>
        private protected async Task InvokeActionAsync(CachedDelegate cachedAction, Event e)
        {
            try
            {
                if (cachedAction.IsAsync)
                {
                    Task task = null;
                    if (cachedAction.Handler is Func<Event, Task> taskFuncWithEvent)
                    {
                        task = taskFuncWithEvent(e);
                    }
                    else if (cachedAction.Handler is Func<Task> taskFunc)
                    {
                        task = taskFunc();
                    }

                    Runtime.NotifyWaitTask(this, task);

                    // We have no reliable stack for awaited operations.
                    await task;
                }
                else if (cachedAction.Handler is Action<Event> actionWithEvent)
                {
                    actionWithEvent(e);
                }
                else if (cachedAction.Handler is Action action)
                {
                    action();
                }
            }
            catch (Exception ex) when (OnExceptionHandler(ex, cachedAction.MethodInfo.Name, e))
            {
                // User handled the exception.
                await OnExceptionHandledAsync(ex, e);
            }
            catch (Exception ex) when (!cachedAction.IsAsync && InvokeOnFailureExceptionFilter(cachedAction, ex))
            {
                // Use an exception filter to call OnFailure before the stack
                // has been unwound. If the exception filter does not fail-fast,
                // it returns false to process the exception normally.
            }
            catch (Exception ex)
            {
                await TryHandleActionInvocationExceptionAsync(ex, cachedAction.MethodInfo.Name);
            }
        }
        
        /// <summary>
        /// Returns the action with the specified name.
        /// </summary>
        private protected MethodInfo GetActionWithName(string actionName)
        {
            MethodInfo action;
            var stateMachineType = GetType();

            do
            {
                var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.Instance | BindingFlags.FlattenHierarchy;
                action = stateMachineType.GetMethod(actionName, bindingFlags, Type.DefaultBinder, SingleEventTypeArray, null);
                if (action is null)
                {
                    action = stateMachineType.GetMethod(actionName, bindingFlags, Type.DefaultBinder, Array.Empty<Type>(), null);
                }

                stateMachineType = stateMachineType.BaseType;
            }
            while (action is null && stateMachineType != typeof(StateMachine));

            Assert(action != null, "Cannot detect action declaration '{0}' in '{1}'.", actionName, GetType().FullName);
            AssertActionValidity(action);
            return action;
        }
        
        /// <summary>
        /// Checks the validity of the specified action.
        /// </summary>
        private void AssertActionValidity(MethodInfo action)
        {
            var actionType = action.DeclaringType;
            var parameters = action.GetParameters();
            Assert(parameters.Length is 0 ||
                   (parameters.Length is 1 && parameters[0].ParameterType == typeof(Event)),
                "Action '{0}' in '{1}' must either accept no parameters or a single parameter of type 'Event'.",
                action.Name, actionType.Name);

            // Check if the action is an 'async' method.
            if (action.GetCustomAttribute(typeof(AsyncStateMachineAttribute)) != null)
            {
                Assert(action.ReturnType == typeof(Task),
                    "Async action '{0}' in '{1}' must have 'Task' return type.",
                    action.Name, actionType.Name);
            }
            else
            {
                Assert(action.ReturnType == typeof(void),
                    "Action '{0}' in '{1}' must have 'void' return type.",
                    action.Name, actionType.Name);
            }
        }
        
        /// <summary>
        /// Invokes user callback when the state machine throws an exception.
        /// </summary>
        /// <param name="ex">The exception thrown by the state machine.</param>
        /// <param name="methodName">The handler (outermost) that threw the exception.</param>
        /// <param name="e">The event being handled when the exception was thrown.</param>
        /// <returns>True if the exception was handled, else false if it should continue to get thrown.</returns>
        private bool OnExceptionHandler(Exception ex, string methodName, Event e)
        {
            if (ex is ExecutionCanceledException)
            {
                // Internal exception used during testing.
                return false;
            }

            Runtime.LogWriter.LogExceptionThrown(Id, CurrentStateName, methodName, ex);

            var outcome = OnException(ex, methodName, e);
            if (outcome is OnExceptionOutcome.ThrowException)
            {
                return false;
            }
            else if (outcome is OnExceptionOutcome.Halt)
            {
                CurrentStatus = Status.Halting;
            }

            Runtime.LogWriter.LogExceptionHandled(Id, CurrentStateName, methodName, ex);
            return true;
        }
        
        /// <summary>
        /// Invokes user callback when the state machine receives an event that it cannot handle.
        /// </summary>
        /// <param name="ex">The exception thrown by the state machine.</param>
        /// <param name="e">The unhandled event.</param>
        /// <returns>True if the state machine should gracefully halt, else false if the exception
        /// should continue to get thrown.</returns>
        private bool OnUnhandledEventExceptionHandler(UnhandledEventException ex, Event e)
        {
            Runtime.LogWriter.LogExceptionThrown(Id, ex.CurrentStateName, string.Empty, ex);

            var outcome = OnException(ex, string.Empty, e);
            if (outcome is OnExceptionOutcome.ThrowException)
            {
                return false;
            }

            CurrentStatus = Status.Halting;
            Runtime.LogWriter.LogExceptionHandled(Id, ex.CurrentStateName, string.Empty, ex);
            return true;
        }
        
        /// <summary>
        /// User callback when the state machine throws an exception. By default,
        /// the state machine throws the exception causing the runtime to fail.
        /// </summary>
        /// <param name="ex">The exception thrown by the state machine.</param>
        /// <param name="methodName">The handler (outermost) that threw the exception.</param>
        /// <param name="e">The event being handled when the exception was thrown.</param>
        /// <returns>The action that the runtime should take.</returns>
        protected OnExceptionOutcome OnException(Exception ex, string methodName, Event e)
        {
            var v = ex is UnhandledEventException;
            if (!v)
            {
                return ex is PNonStandardReturnException
                    ? OnExceptionOutcome.HandledException
                    : OnExceptionOutcome.ThrowException;
            }

            return (ex as UnhandledEventException).UnhandledEvent is PHalt
                ? OnExceptionOutcome.Halt
                : OnExceptionOutcome.ThrowException;
        }
        
        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        public override bool Equals(object obj)
        {
            if (obj is StateMachine m &&
                GetType() == m.GetType())
            {
                return Id.Value == m.Id.Value;
            }

            return false;
        }
        
        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        public override int GetHashCode()
        {
            return Id.Value.GetHashCode();
        }
        
        /// <summary>
        /// Enqueues the specified event and its metadata.
        /// </summary>
        internal EnqueueStatus Enqueue(Event e, EventInfo info)
        {
            if (CurrentStatus is Status.Halted)
            {
                return EnqueueStatus.Dropped;
            }

            return Inbox.Enqueue(e, info);
        }
        
        /// <summary>
        /// Returns a string that represents the current state machine.
        /// </summary>
        public override string ToString()
        {
            return Id.Name;
        }

        /// <summary>
        /// The status of the state machine.
        /// </summary>
        private protected enum Status
        {
            /// <summary>
            /// The state machine is active.
            /// </summary>
            Active = 0,

            /// <summary>
            /// The state machine is halting.
            /// </summary>
            Halting,

            /// <summary>
            /// The state machine is halted.
            /// </summary>
            Halted
        }
        
        /// <summary>
        /// The type of user callback.
        /// </summary>
        private protected static class UserCallbackType
        {
            internal const string OnInitialize = nameof(OnInitializeAsync);
            internal const string OnEventDequeued = nameof(OnEventDequeuedAsync);
            internal const string OnEventHandled = nameof(OnEventHandledAsync);
            internal const string OnEventUnhandled = nameof(OnEventUnhandledAsync);
            internal const string OnExceptionHandled = nameof(OnExceptionHandledAsync);
            internal const string OnHalt = nameof(OnHaltAsync);
        }

        /// <summary>
        /// Raises the specified <see cref="Event"/> at the end of the current action.
        /// </summary>
        /// <remarks>
        /// This event is not handled until the action that calls this method returns control back
        /// to the runtime.  It is handled before any other events are dequeued from the inbox.
        /// Only one of the following can be called per action:
        /// <see cref="RaiseEvent"/>, <see cref="RaiseGotoStateEvent{T}"/> or
        /// <see cref="RaiseHaltEvent"/>.
        /// An Assert is raised if you accidentally try and do two of these operations in a single action.
        /// </remarks>
        /// <param name="e">The event to raise.</param>
        public void RaiseEvent(Event e)
        {
            Assert(CurrentStatus is Status.Active, "{0} invoked RaiseEvent while halting.", Id);
            Assert(e != null, "{0} is raising a null event.", Id);
            CheckDanglingTransition();
            PendingTransition = new Transition(Transition.Type.RaiseEvent, default, e);
            throw new PNonStandardReturnException { ReturnKind = NonStandardReturn.Raise };
        }

        /// <summary>
        /// Raise a special event that performs a goto state operation at the end of the current action.
        /// </summary>
        /// <remarks>
        /// Goto state pops the current <see cref="State"/> and pushes the specified <see cref="State"/> on the active state stack.
        /// This is shorthand for the following code:
        /// <code>
        /// class Event E { }
        /// [OnEventGotoState(typeof(E), typeof(S))]
        /// this.RaiseEvent(new E());
        /// </code>
        /// This event is not handled until the action that calls this method returns control back
        /// to the runtime.  It is handled before any other events are dequeued from the inbox.
        /// Only one of the following can be called per action:
        /// <see cref="RaiseEvent"/>, <see cref="RaiseGotoStateEvent{T}"/> or
        /// <see cref="RaiseHaltEvent"/>.
        /// An Assert is raised if you accidentally try and do two of these operations in a single action.
        /// </remarks>
        /// <typeparam name="S">Type of the state.</typeparam>
        public void RaiseGotoStateEvent<S>(IPValue payload = null) where S : State
        {
            gotoPayload = payload;
            RaiseGotoStateEvent(typeof(S));
            throw new PNonStandardReturnException { ReturnKind = NonStandardReturn.Goto };
        }

        /// <summary>
        /// Raise a special event that performs a goto state operation at the end of the current action.
        /// </summary>
        /// <remarks>
        /// Goto state pops the current <see cref="State"/> and pushes the specified <see cref="State"/> on the active state stack.
        /// This is shorthand for the following code:
        /// <code>
        /// class Event E { }
        /// [OnEventGotoState(typeof(E), typeof(S))]
        /// this.RaiseEvent(new E());
        /// </code>
        /// This event is not handled until the action that calls this method returns control back
        /// to the runtime.  It is handled before any other events are dequeued from the inbox.
        /// Only one of the following can be called per action:
        /// <see cref="RaiseEvent"/>, <see cref="RaiseGotoStateEvent{T}"/> <see cref="RaiseHaltEvent"/>.
        /// An Assert is raised if you accidentally try and do two of these operations in a single action.
        /// </remarks>
        /// <param name="state">Type of the state.</param>
        protected void RaiseGotoStateEvent(Type state)
        {
            Assert(CurrentStatus is Status.Active, "{0} invoked GotoState while halting.", Id);
            Assert(StateTypeCache[GetType()].Any(val => val.DeclaringType.Equals(state.DeclaringType) && val.Name.Equals(state.Name)),
                "{0} is trying to transition to non-existing state '{1}'.", Id, state.Name);
            CheckDanglingTransition();
            PendingTransition = new Transition(Transition.Type.GotoState, state, default);
        }

        /// <summary>
        /// Raises a <see cref='HaltEvent'/> to halt the state machine at the end of the current action.
        /// </summary>
        /// <remarks>
        /// This event is not handled until the action that calls this method returns control back
        /// to the runtime.  It is handled before any other events are dequeued from the inbox.
        ///
        /// Only one of the following can be called per action:
        /// <see cref="RaiseEvent"/>, <see cref="RaiseGotoStateEvent{T}"/> and <see cref="RaiseHaltEvent"/>.
        /// An Assert is raised if you accidentally try and do two of these operations in a single action.
        /// </remarks>
        protected void RaiseHaltEvent()
        {
            Assert(CurrentStatus is Status.Active, "{0} invoked Halt while halting.", Id);
            CurrentStatus = Status.Halting;
            CheckDanglingTransition();
            PendingTransition = new Transition(Transition.Type.Halt, null, default);
        }
        

        /// <summary>
        /// Handles the specified <see cref="Event"/>.
        /// </summary>
        private protected async Task HandleEventAsync(Event e)
        {
            var currentState = CurrentState;

            while (true)
            {
                if (CurrentState is null)
                {
                    // If the stack of states is empty then halt or fail the state machine.
                    if (e is HaltEvent)
                    {
                        // If it is the halt event, then change the state machine status to halting.
                        CurrentStatus = Status.Halting;
                        break;
                    }

                    var currentStateName = currentState.GetType().Name;
                    await InvokeUserCallbackAsync(UserCallbackType.OnEventUnhandled, e, currentStateName);
                    if (CurrentStatus is Status.Active)
                    {
                        // If the event cannot be handled then report an error, else halt gracefully.
                        var ex = new UnhandledEventException(e, currentStateName, "Unhandled Event");
                        var isHalting = OnUnhandledEventExceptionHandler(ex, e);
                        Assert(isHalting, "{0} received event '{1}' that cannot be handled.",
                            Id, e.GetType().FullName);
                    }

                    break;
                }

                if (e is GotoStateEvent gotoStateEvent)
                {
                    await GotoStateAsync(gotoStateEvent.State, null, e);
                }
                else if (EventHandlerMap.ContainsKey(e.GetType()))
                {
                    await HandleEventAsync(e, currentState, EventHandlerMap[e.GetType()]);
                }
                else
                {
                    if (TryGetHandler(e.GetType(), out EventHandlerDeclaration ehandler))
                    {
                        // Then specific event is more recent than any wild card events.
                        await HandleEventAsync(e, currentState, ehandler);
                    }
                    else if (StateMachineActionMap.TryGetValue(e.GetType().Name, out var handler))
                    {
                        // Allow StateMachine to have class level OnEventDoActions.
                        Runtime.NotifyInvokedAction(this, handler.MethodInfo, CurrentStateName, CurrentStateName, e);
                        await InvokeActionAsync(handler, e);
                    }
                    else
                    {
                        // If the current state cannot handle the event.
                        await ExecuteCurrentStateOnExitAsync(null, e);
                        if (CurrentStatus is Status.Active)
                        {
                            Runtime.LogWriter.LogPopStateUnhandledEvent(Id, CurrentStateName, e);
                            EventHandlerMap = EmptyEventHandlerMap;
                            CurrentState = null;
                            CurrentStateName = string.Empty;
                            continue;
                        }
                    }
                }

                break;
            }
        }

        private bool TryGetHandler(Type e, out EventHandlerDeclaration o)
        {
            if (EventHandlerMap.ContainsKey(e))
            {
                o = EventHandlerMap[e];
                return true;
            }

            if (EventHandlerMap.ContainsKey(typeof(WildCardEvent)))
            {
                o = EventHandlerMap[typeof(WildCardEvent)];
                return true;
            }

            o = null;
            return false;
        }

        private async Task HandleEventAsync(Event e, State declaringState, EventHandlerDeclaration eventHandler)
        {
            var handlingStateName = declaringState.GetType().Name;
            if (eventHandler is ActionEventHandlerDeclaration actionEventHandler)
            {
                var cachedAction = StateMachineActionMap[actionEventHandler.Name];
                Runtime.NotifyInvokedAction(this, cachedAction.MethodInfo, handlingStateName, CurrentStateName, e);
                await InvokeActionAsync(cachedAction, e);
                await ApplyEventHandlerTransitionAsync(PendingTransition, e);
            }
            else if (eventHandler is GotoStateTransition gotoTransition)
            {
                await GotoStateAsync(gotoTransition.TargetState, gotoTransition.Lambda, e);
            }
            else if (eventHandler is PushStateTransition pushTransition)
            {
                await PushStateAsync(pushTransition.TargetState, e);
            }
        }

        /// <summary>
        /// Executes the on entry action of the current state.
        /// </summary>
        private async Task ExecuteCurrentStateOnEntryAsync(Event e)
        {
            Runtime.NotifyEnteredState(this);

            CachedDelegate entryAction = null;
            if (CurrentState.EntryAction != null)
            {
                entryAction = StateMachineActionMap[CurrentState.EntryAction];
            }

            // Invokes the entry action of the new state, if there is one available.
            if (entryAction != null)
            {
                Runtime.NotifyInvokedOnEntryAction(this, entryAction.MethodInfo, e);
                await InvokeActionAsync(entryAction, e);
                await ApplyEventHandlerTransitionAsync(PendingTransition, e);
            }
        }

        /// <summary>
        /// Executes the on exit action of the current state.
        /// </summary>
        private async Task ExecuteCurrentStateOnExitAsync(string eventHandlerExitActionName, Event e)
        {
            Runtime.NotifyExitedState(this);

            CachedDelegate exitAction = null;
            if (CurrentState.ExitAction != null)
            {
                exitAction = StateMachineActionMap[CurrentState.ExitAction];
            }

            // Invokes the exit action of the current state,
            // if there is one available.
            if (exitAction != null)
            {
                Runtime.NotifyInvokedOnExitAction(this, exitAction.MethodInfo, e);
                await InvokeActionAsync(exitAction, e);
                var transition = PendingTransition;
                Assert(transition.TypeValue is Transition.Type.None ||
                       transition.TypeValue is Transition.Type.Halt,
                    "{0} has performed a '{1}' transition from an OnExit action.",
                    Id, transition.TypeValue);
                await ApplyEventHandlerTransitionAsync(transition, e);
            }

            // Invokes the exit action of the event handler,
            // if there is one available.
            if (eventHandlerExitActionName != null && CurrentStatus is Status.Active)
            {
                var eventHandlerExitAction = StateMachineActionMap[eventHandlerExitActionName];
                Runtime.NotifyInvokedOnExitAction(this, eventHandlerExitAction.MethodInfo, e);
                await InvokeActionAsync(eventHandlerExitAction, e);
                var transition = PendingTransition;
                Assert(transition.TypeValue is Transition.Type.None ||
                       transition.TypeValue is Transition.Type.Halt,
                    "{0} has performed a '{1}' transition from an OnExit action.",
                    Id, transition.TypeValue);
                await ApplyEventHandlerTransitionAsync(transition, e);
            }
        }

        /// <summary>
        /// Applies the specified event handler transition.
        /// </summary>
        private Task ApplyEventHandlerTransitionAsync(Transition transition, Event e)
        {
            if (transition.TypeValue != PendingTransition.TypeValue && PendingTransition.TypeValue != Transition.Type.None)
            {
                CheckDanglingTransition();
            }
            else if (transition.TypeValue is Transition.Type.RaiseEvent)
            {
                PendingTransition = default;
                Inbox.RaiseEvent(transition.Event);
            }
            else if (transition.TypeValue is Transition.Type.GotoState)
            {
                PendingTransition = default;
                Inbox.RaiseEvent(new GotoStateEvent(transition.State));
            }
            else if (transition.TypeValue is Transition.Type.Halt)
            {
                // If it is the halt transition, then change the state machine status to halting.
                PendingTransition = default;
                CurrentStatus = Status.Halting;
            }
            else
            {
                PendingTransition = default;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Notifies that a Transition was created but not returned to the StateMachine.
        /// </summary>
        private void CheckDanglingTransition()
        {
            var transition = PendingTransition;
            PendingTransition = default;

            if (transition.TypeValue != Transition.Type.None)
            {
                var prefix = string.Format("{0} transition created by {1} in state {2} was not processed",
                    transition.TypeValue, GetType().FullName, CurrentStateName);
                string suffix = null;

                if (transition.State != null && transition.Event != null)
                {
                    suffix = string.Format(", state {0}, event {1}.", transition.State, transition.Event);
                }
                else if (transition.State != null)
                {
                    suffix = string.Format(", state {0}.", transition.State);
                }
                else if (transition.Event != null)
                {
                    suffix = string.Format(", event {0}.", transition.Event);
                }

                Assert(false, prefix + suffix);
            }
        }

        /// <summary>
        /// Performs a goto transition to the specified state.
        /// </summary>
        private async Task GotoStateAsync(Type s, string onExitActionName, Event e)
        {
            Runtime.LogWriter.LogGotoState(Id, CurrentStateName,
                $"{s.DeclaringType}.{s.Name}");

            // The state machine performs the on exit action of the current state.
            await ExecuteCurrentStateOnExitAsync(onExitActionName, e);
            if (CurrentStatus is Status.Active)
            {
                // The state machine transitions to the new state.
                var nextState = StateInstanceCache[GetType()].First(val => val.GetType().Equals(s));
                DoStateTransition(nextState);

                // The state machine performs the on entry action of the new state.
                await ExecuteCurrentStateOnEntryAsync(e);
            }
        }

        /// <summary>
        /// Performs a push transition to the specified state.
        /// </summary>
        private async Task PushStateAsync(Type s, Event e)
        {

            var nextState = StateInstanceCache[GetType()].First(val => val.GetType().Equals(s));
            DoStateTransition(nextState);

            // The state machine performs the on entry statements of the new state.
            await ExecuteCurrentStateOnEntryAsync(e);
        }

        /// <summary>
        /// Configures the state transitions of the state machine when a state is pushed into the stack.
        /// </summary>
        private void DoStateTransition(State state)
        {
            EventHandlerMap = state.EventHandlers;  // non-inheritable handlers.
            CurrentState = state;
            CurrentStateName = CurrentState.GetType().Name;
        }

        

        /// <summary>
        /// Checks if the specified event is ignored in the current state.
        /// </summary>
        internal bool IsEventIgnoredInCurrentState(Event e)
        {
            var eventType = e.GetType();

            // If a non-inheritable transition is defined, then the event is not ignored
            // because the non-inheritable operation takes precedent.
            if (EventHandlerMap.ContainsKey(eventType))
            {
                return EventHandlerMap[eventType] is IgnoreEventHandlerDeclaration;
            }

            return false;
        }

        /// <summary>
        /// Checks if the specified event is deferred in the current state.
        /// </summary>
        internal bool IsEventDeferredInCurrentState(Event e)
        {
            var eventType = e.GetType();

            // If a non-inheritable transition is defined, then the event is not deferred.
            if (EventHandlerMap.ContainsKey(eventType))
            {
                return EventHandlerMap[eventType] is DeferEventHandlerDeclaration;
            }
            return false;
        }

        /// <summary>
        /// Checks if a default handler is installed in current state.
        /// </summary>
        internal bool IsDefaultHandlerInstalledInCurrentState() =>
            EventHandlerMap.ContainsKey(typeof(DefaultEvent));

        /// <summary>
        /// Returns the hashed state of this state machine.
        /// </summary>
        internal int GetHashedState()
        {
            unchecked
            {
                var hash = 19;
                hash = (hash * 31) + GetType().GetHashCode();
                hash = (hash * 31) + Id.Value.GetHashCode();
                hash = (hash * 31) + IsHalted.GetHashCode();

                hash = (hash * 31) + Manager.GetCachedState();
                
                return hash;
            }
        }

        /// <summary>
        /// Extracts user declarations and setups the event handlers and state transitions.
        /// </summary>
        internal void SetupEventHandlers()
        {
            var stateMachineType = GetType();

            // If this type has not already been setup in the ActionCache, then we need to try and grab the ActionCacheLock
            // for this type.  First make sure we have one and only one lockable object for this type.
            var syncObject = ActionCacheLocks.GetOrAdd(stateMachineType, _ => new object());

            // Locking this syncObject ensures only one thread enters the initialization code to update
            // the ActionCache for this specific state machine type.
            lock (syncObject)
            {
                if (ActionCache.ContainsKey(stateMachineType))
                {
                    // Note: even if we won the GetOrAdd, there is a tiny window of opportunity for another thread
                    // to slip in and lock the syncObject before us, so we have to check the ActionCache again
                    // here just in case.
                }
                else
                {
                    // Caches the available state types for this state machine type.
                    if (StateTypeCache.TryAdd(stateMachineType, new HashSet<Type>()))
                    {
                        var baseType = stateMachineType;
                        while (baseType != typeof(StateMachine))
                        {
                            foreach (var s in baseType.GetNestedTypes(BindingFlags.Instance |
                                                                      BindingFlags.NonPublic | BindingFlags.Public |
                                                                      BindingFlags.DeclaredOnly))
                            {
                                ExtractStateTypes(s);
                            }

                            baseType = baseType.BaseType;
                        }
                    }

                    // Caches the available state instances for this state machine type.
                    if (StateInstanceCache.TryAdd(stateMachineType, new HashSet<State>()))
                    {
                        foreach (var type in StateTypeCache[stateMachineType])
                        {
                            var stateType = type;
                            if (type.IsAbstract)
                            {
                                continue;
                            }

                            if (type.IsGenericType)
                            {
                                // If the state type is generic (only possible if inherited by a generic state
                                // machine declaration), then iterate through the base state machine classes to
                                // identify the runtime generic type, and use it to instantiate the runtime state
                                // type. This type can be then used to create the state constructor.
                                var declaringType = GetType();
                                while (!declaringType.IsGenericType ||
                                       !type.DeclaringType.FullName.Equals(declaringType.FullName.Substring(
                                           0, declaringType.FullName.IndexOf('['))))
                                {
                                    declaringType = declaringType.BaseType;
                                }

                                if (declaringType.IsGenericType)
                                {
                                    stateType = type.MakeGenericType(declaringType.GetGenericArguments());
                                }
                            }

                            var constructor = stateType.GetConstructor(Type.EmptyTypes);
                            var lambda = Expression.Lambda<Func<State>>(
                                Expression.New(constructor)).Compile();
                            var state = lambda();

                            try
                            {
                                state.InitializeState();
                            }
                            catch (InvalidOperationException ex)
                            {
                                Assert(false, "{0} {1} in state '{2}'.", Id, ex.Message, state);
                            }

                            StateInstanceCache[stateMachineType].Add(state);
                        }
                    }

                    // Caches the action declarations for this state machine type.
                    var map = new Dictionary<string, MethodInfo>();
                    foreach (var state in StateInstanceCache[stateMachineType])
                    {
                        if (state.EntryAction != null &&
                            !map.ContainsKey(state.EntryAction))
                        {
                            map.Add(state.EntryAction, GetActionWithName(state.EntryAction));
                        }

                        if (state.ExitAction != null &&
                            !map.ContainsKey(state.ExitAction))
                        {
                            map.Add(state.ExitAction, GetActionWithName(state.ExitAction));
                        }

                        foreach (var handler in state.EventHandlers.Values)
                        {
                            if (handler is GotoStateTransition transition)
                            {
                                if (transition.Lambda != null &&
                                    !map.ContainsKey(transition.Lambda))
                                {
                                    map.Add(transition.Lambda, GetActionWithName(transition.Lambda));
                                }
                            }
                            
                            if (handler is ActionEventHandlerDeclaration action)
                            {
                                if (!map.ContainsKey(action.Name))
                                {
                                    map.Add(action.Name, GetActionWithName(action.Name));
                                }
                            }
                        }
                    }

                    ActionCache.TryAdd(stateMachineType, map);
                }
            }

            // Populates the map of event handlers for this state machine instance.
            foreach (var kvp in ActionCache[stateMachineType])
            {
                StateMachineActionMap.Add(kvp.Key, new CachedDelegate(kvp.Value, this));
            }

            var initialStates = StateInstanceCache[stateMachineType].Where(state => state.IsStart).ToList();
            Assert(initialStates.Count != 0, "{0} must declare a start state.", Id);
            Assert(initialStates.Count is 1, "{0} can not declare more than one start states.", Id);

            DoStateTransition(initialStates[0]);
            AssertStateValidity();
        }

        /// <summary>
        /// Processes a type, looking for states.
        /// </summary>
        private void ExtractStateTypes(Type type)
        {
            var stack = new Stack<Type>();
            stack.Push(type);

            while (stack.Count > 0)
            {
                var nextType = stack.Pop();

                if (nextType.IsClass && nextType.IsSubclassOf(typeof(State)))
                {
                    StateTypeCache[GetType()].Add(nextType);
                }
                
                /* TODO: figure whether this part is needed */
                if (nextType.BaseType != null)
                {
                    stack.Push(nextType.BaseType);
                }
            }
        }

        /// <summary>
        /// Returns the set of all states in the state machine (for code coverage).
        /// </summary>
        internal HashSet<string> GetAllStates()
        {
            Assert(StateInstanceCache.ContainsKey(GetType()), "{0} has not populated its states yet.", Id);

            var allStates = new HashSet<string>();
            foreach (var state in StateInstanceCache[GetType()])
            {
                allStates.Add(state.GetType().Name);
            }

            return allStates;
        }

        private static bool IncludeInCoverage(EventHandlerDeclaration handler)
        {
            if (handler is DeferEventHandlerDeclaration || handler is IgnoreEventHandlerDeclaration)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns the set of all (states, registered event) pairs in the state machine (for code coverage).
        /// It does not include events that are deferred or ignored.
        /// </summary>
        internal HashSet<Tuple<string, string>> GetAllStateEventPairs()
        {
            Assert(StateInstanceCache.ContainsKey(GetType()), "{0} has not populated its states yet.", Id);

            var pairs = new HashSet<Tuple<string, string>>();
            foreach (var state in StateInstanceCache[GetType()])
            {
                foreach (var binding in from b in state.EventHandlers 
                         where IncludeInCoverage(b.Value)
                         select b)
                {
                    pairs.Add(Tuple.Create(state.GetType().Name, binding.Key.FullName));
                }
            }

            return pairs;
        }

        /// <summary>
        /// Checks the state machine for state related errors.
        /// </summary>
        private void AssertStateValidity()
        {
            Assert(StateTypeCache[GetType()].Count > 0, "{0} must have one or more states.", Id);
            Assert(CurrentState != null, "{0} must not have a null current state.", Id);
        }

        /// <summary>
        /// Wraps the unhandled exception inside an <see cref="AssertionFailureException"/>
        /// exception, and throws it to the user.
        /// </summary>
        private protected void ReportUnhandledException(Exception ex, string actionName)
        {
            var state = CurrentState is null ? "<unknown>" : CurrentStateName;
            Runtime.WrapAndThrowException(ex, "{0} (state '{1}', action '{2}')", Id, state, actionName);
        }

        /// <summary>
        /// Defines the <see cref="StateMachine"/> transition that is the
        /// result of executing an event handler.  Transitions are created by using
        /// <see cref="RaiseGotoStateEvent{T}"/>, <see cref="RaiseEvent"/> and <see cref="RaiseHaltEvent"/>.
        /// The Transition is processed by the ControlledRuntime when
        /// an event handling method of a StateMachine returns a Transition object.
        /// This means such a method can only do one such Transition per method call.
        /// If the method wants to do a conditional transition it can return
        /// Transition.None to indicate no transition is to be performed.
        /// </summary>
        internal readonly struct Transition
        {
            /// <summary>
            /// The type of the transition.
            /// </summary>
            public readonly Type TypeValue;

            /// <summary>
            /// The target state of the transition, if there is one.
            /// </summary>
            internal readonly System.Type State;

            /// <summary>
            /// The event participating in the transition, if there is one.
            /// </summary>
            internal readonly Event Event;

            /// <summary>
            /// This special transition represents a transition that does not change the current <see cref="StateMachine.State"/>.
            /// </summary>
            public static Transition None = default;

            /// <summary>
            /// Initializes a new instance of the <see cref="Transition"/> struct.
            /// </summary>
            /// <param name="type">The type of the transition.</param>
            /// <param name="state">The target state of the transition, if there is one.</param>
            /// <param name="e">The event participating in the transition, if there is one.</param>
            internal Transition(Type type, System.Type state, Event e)
            {
                TypeValue = type;
                State = state;
                Event = e;
            }

            /// <summary>
            /// Defines the type of a <see cref="StateMachine"/> transition.
            /// </summary>
            public enum Type
            {
                /// <summary>
                /// A transition that does not change the <see cref="StateMachine.State"/>.
                /// This is the value used by <see cref="Transition.None"/>.
                /// </summary>
                None = 0,

                /// <summary>
                /// A transition created by <see cref="StateMachine.RaiseEvent(Event)"/> that raises an <see cref="Event"/> bypassing
                /// the <see cref="StateMachine.State"/> inbox.
                /// </summary>
                RaiseEvent,

                /// <summary>
                /// A transition created by <see cref="RaiseGotoStateEvent{S}"/> that pops the current <see cref="StateMachine.State"/>
                /// and pushes the specified <see cref="StateMachine.State"/> on the
                /// stack of <see cref="StateMachine"/> states.
                /// </summary>
                GotoState,

                /// <summary>
                /// A transition created by <see cref="RaiseHaltEvent"/> that halts the <see cref="StateMachine"/>.
                /// </summary>
                Halt
            }
        }

        /// <summary>
        /// Abstract class representing a state.
        /// </summary>
        public abstract class State
        {
            /// <summary>
            /// The entry action of the state.
            /// </summary>
            internal string EntryAction { get; private set; }

            /// <summary>
            /// The exit action of the state.
            /// </summary>
            internal string ExitAction { get; private set; }

            /// <summary>
            /// Map containing all non-inheritable event handler declarations.
            /// </summary>
            internal Dictionary<Type, EventHandlerDeclaration> EventHandlers;

            /// <summary>
            /// True if this is the start state.
            /// </summary>
            internal bool IsStart { get; private set; }

            /// <summary>
            /// Initializes a new instance of the <see cref="State"/> class.
            /// </summary>
            protected State()
            {
            }

            /// <summary>
            /// Initializes the state.
            /// </summary>
            internal void InitializeState()
            {
                IsStart = false;
                EventHandlers = new Dictionary<Type, EventHandlerDeclaration>();

                if (GetType().GetCustomAttribute(typeof(OnEntryAttribute), true) is OnEntryAttribute entryAttribute)
                {
                    EntryAction = entryAttribute.Action;
                }

                if (GetType().GetCustomAttribute(typeof(OnExitAttribute), true) is OnExitAttribute exitAttribute)
                {
                    ExitAction = exitAttribute.Action;
                }

                if (GetType().IsDefined(typeof(StartAttribute), false))
                {
                    IsStart = true;
                }

                // Events with already declared handlers.
                var handledEvents = new HashSet<Type>();

                // Install event handlers.
                InstallGotoTransitions(handledEvents);
                InstallActionBindings(handledEvents);
                InstallIgnoreHandlers(handledEvents);
                InstallDeferHandlers(handledEvents);
            }

            /// <summary>
            /// Declares goto event handlers, if there are any.
            /// </summary>
            private void InstallGotoTransitions(HashSet<Type> handledEvents)
            {
                var gotoAttributes = GetType().GetCustomAttributes(typeof(OnEventGotoStateAttribute), false)
                    as OnEventGotoStateAttribute[];

                foreach (var attr in gotoAttributes)
                {
                    CheckEventHandlerAlreadyDeclared(attr.Event, handledEvents);

                    if (attr.Action is null)
                    {
                        EventHandlers.Add(attr.Event, new GotoStateTransition(attr.State));
                    }
                    else
                    {
                        EventHandlers.Add(attr.Event, new GotoStateTransition(attr.State, attr.Action));
                    }

                    handledEvents.Add(attr.Event);
                }
            }

            /// <summary>
            /// Installs action bindings, if there are any.
            /// </summary>
            private void InstallActionBindings(HashSet<Type> handledEvents)
            {
                var doAttributes = GetType().GetCustomAttributes(typeof(OnEventDoActionAttribute), false)
                    as OnEventDoActionAttribute[];

                foreach (var attr in doAttributes)
                {
                    CheckEventHandlerAlreadyDeclared(attr.Event, handledEvents);

                    EventHandlers.Add(attr.Event, new ActionEventHandlerDeclaration(attr.Action));
                    handledEvents.Add(attr.Event);
                }
            }
            
            /// <summary>
            /// Declares ignore event handlers, if there are any.
            /// </summary>
            private void InstallIgnoreHandlers(HashSet<Type> handledEvents)
            {
                var ignoredEvents = new HashSet<Type>();
                if (GetType().GetCustomAttribute(typeof(IgnoreEventsAttribute), false) is IgnoreEventsAttribute ignoreEventsAttribute)
                {
                    foreach (var e in ignoreEventsAttribute.Events)
                    {
                        CheckEventHandlerAlreadyDeclared(e, handledEvents);

                        EventHandlers.Add(e, new IgnoreEventHandlerDeclaration());
                        ignoredEvents.Add(e);
                        handledEvents.Add(e);
                    }
                }
            }
            

            /// <summary>
            /// Declares defer event handlers, if there are any.
            /// </summary>
            private void InstallDeferHandlers(HashSet<Type> handledEvents)
            {
                var deferredEvents = new HashSet<Type>();
                if (GetType().GetCustomAttribute(typeof(DeferEventsAttribute), false) is DeferEventsAttribute deferEventsAttribute)
                {
                    foreach (var e in deferEventsAttribute.Events)
                    {
                        CheckEventHandlerAlreadyDeclared(e, handledEvents);
                        EventHandlers.Add(e, new DeferEventHandlerDeclaration());
                        deferredEvents.Add(e);
                        handledEvents.Add(e);
                    }
                }

                InheritDeferHandlers(GetType().BaseType, handledEvents, deferredEvents);
            }

            /// <summary>
            /// Inherits defer event handlers from a base state, if there is one.
            /// </summary>
            private void InheritDeferHandlers(Type baseState, HashSet<Type> handledEvents, HashSet<Type> deferredEvents)
            {
                if (!baseState.IsSubclassOf(typeof(State)))
                {
                    return;
                }

                if (baseState.GetCustomAttribute(typeof(DeferEventsAttribute), false) is DeferEventsAttribute deferEventsAttribute)
                {
                    foreach (var e in deferEventsAttribute.Events)
                    {
                        if (deferredEvents.Contains(e))
                        {
                            continue;
                        }
                        CheckEventHandlerAlreadyDeclared(e, handledEvents);
                        EventHandlers.Add(e, new DeferEventHandlerDeclaration());
                        deferredEvents.Add(e);
                        handledEvents.Add(e);
                    }
                }

                InheritDeferHandlers(baseState.BaseType, handledEvents, deferredEvents);
            }

            /// <summary>
            /// Checks if an event handler has been already declared.
            /// </summary>
            private static void CheckEventHandlerAlreadyDeclared(Type e, HashSet<Type> handledEvents)
            {
                if (handledEvents.Contains(e))
                {
                    throw new InvalidOperationException($"declared multiple handlers for event '{e}'");
                }
            }

            /// <summary>
            /// Attribute for declaring the state that a state machine transitions upon creation.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class)]
            protected sealed class StartAttribute : Attribute
            {
            }

            /// <summary>
            /// Attribute for declaring what action to perform when entering a state.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class)]
            protected sealed class OnEntryAttribute : Attribute
            {
                /// <summary>
                /// Action name.
                /// </summary>
                internal readonly string Action;

                /// <summary>
                /// Initializes a new instance of the <see cref="OnEntryAttribute"/> class.
                /// </summary>
                /// <param name="actionName">The name of the action to execute.</param>
                public OnEntryAttribute(string actionName)
                {
                    Action = actionName;
                }
            }

            /// <summary>
            /// Attribute for declaring what action to perform when exiting a state.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class)]
            protected sealed class OnExitAttribute : Attribute
            {
                /// <summary>
                /// Action name.
                /// </summary>
                internal string Action;

                /// <summary>
                /// Initializes a new instance of the <see cref="OnExitAttribute"/> class.
                /// </summary>
                /// <param name="actionName">The name of the action to execute.</param>
                public OnExitAttribute(string actionName)
                {
                    Action = actionName;
                }
            }

            /// <summary>
            /// Attribute for declaring a goto state transition when the state machine
            /// is in the specified state and dequeues an event of the specified type.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
            protected sealed class OnEventGotoStateAttribute : Attribute
            {
                /// <summary>
                /// The type of the dequeued event.
                /// </summary>
                internal readonly Type Event;

                /// <summary>
                /// The type of the state.
                /// </summary>
                internal readonly Type State;

                /// <summary>
                /// Action name.
                /// </summary>
                internal readonly string Action;

                /// <summary>
                /// Initializes a new instance of the <see cref="OnEventGotoStateAttribute"/> class.
                /// </summary>
                /// <param name="eventType">The type of the dequeued event.</param>
                /// <param name="stateType">The type of the state.</param>
                public OnEventGotoStateAttribute(Type eventType, Type stateType)
                {
                    Event = eventType;
                    State = stateType;
                }

                /// <summary>
                /// Initializes a new instance of the <see cref="OnEventGotoStateAttribute"/> class.
                /// </summary>
                /// <param name="eventType">The type of the dequeued event.</param>
                /// <param name="stateType">The type of the state.</param>
                /// <param name="actionName">Name of action to perform on exit.</param>
                public OnEventGotoStateAttribute(Type eventType, Type stateType, string actionName)
                {
                    Event = eventType;
                    State = stateType;
                    Action = actionName;
                }
            }

            /// <summary>
            /// Attribute for declaring which action should be invoked when the state machine
            /// is in the specified state to handle a dequeued event of the specified type.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
            protected sealed class OnEventDoActionAttribute : Attribute
            {
                /// <summary>
                /// The type of the dequeued event.
                /// </summary>
                internal Type Event;

                /// <summary>
                /// The name of the action to invoke.
                /// </summary>
                internal string Action;

                /// <summary>
                /// Initializes a new instance of the <see cref="OnEventDoActionAttribute"/> class.
                /// </summary>
                /// <param name="eventType">The type of the dequeued event.</param>
                /// <param name="actionName">The name of the action to invoke.</param>
                public OnEventDoActionAttribute(Type eventType, string actionName)
                {
                    Event = eventType;
                    Action = actionName;
                }
            }

            /// <summary>
            /// Attribute for declaring what events should be deferred in a state.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class)]
            protected sealed class DeferEventsAttribute : Attribute
            {
                /// <summary>
                /// Event types.
                /// </summary>
                internal Type[] Events;

                /// <summary>
                /// Initializes a new instance of the <see cref="DeferEventsAttribute"/> class.
                /// </summary>
                /// <param name="eventTypes">Event types</param>
                public DeferEventsAttribute(params Type[] eventTypes)
                {
                    Events = eventTypes;
                }
            }

            /// <summary>
            /// Attribute for declaring what events should be ignored in a state.
            /// </summary>
            [AttributeUsage(AttributeTargets.Class)]
            protected sealed class IgnoreEventsAttribute : Attribute
            {
                /// <summary>
                /// Event types.
                /// </summary>
                internal Type[] Events;

                /// <summary>
                /// Initializes a new instance of the <see cref="IgnoreEventsAttribute"/> class.
                /// </summary>
                /// <param name="eventTypes">Event types</param>
                public IgnoreEventsAttribute(params Type[] eventTypes)
                {
                    Events = eventTypes;
                }
            }
        }
    }
}