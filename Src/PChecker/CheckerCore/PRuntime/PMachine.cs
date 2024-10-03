using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PChecker.Actors;
using PChecker.Actors.Events;
using PChecker.Actors.Exceptions;
using PChecker.Actors.Logging;
using PChecker.PRuntime.Exceptions;
using PChecker.PRuntime.Values;

namespace PChecker.PRuntime
{
    public class PMachine : StateMachine
    {
        public List<string> creates = new List<string>();
        protected IPrtValue gotoPayload;
        private string interfaceName;
        public List<string> receives = new List<string>();
        public PMachineValue self;
        public List<string> sends = new List<string>();

        public void TryAssert(bool predicate)
        {
            Assert(predicate);
        }

        public void TryAssert(bool predicate, string s, params object[] args)
        {
            Assert(predicate, s, args);
        }

        protected void InitializeParametersFunction(Event e)
        {
            if (!(e is InitializeParametersEvent @event))
            {
                throw new ArgumentException("Event type is incorrect: " + e.GetType().Name);
            }

            var initParam = @event.Payload as InitializeParameters;
            interfaceName = initParam.InterfaceName;
            self = new PMachineValue(Id, receives.ToList());
            TryRaiseEvent(GetConstructorEvent(initParam.Payload), initParam.Payload);
        }

        protected virtual Event GetConstructorEvent(IPrtValue value)
        {
            throw new NotImplementedException();
        }

        protected override OnExceptionOutcome OnException(Exception ex, string methodName, Event e)
        {
            var v = ex is UnhandledEventException;
            if (!v)
            {
                return ex is PNonStandardReturnException
                    ? OnExceptionOutcome.HandledException
                    : base.OnException(ex, methodName, e);
            }

            return (ex as UnhandledEventException).UnhandledEvent is PHalt
                ? OnExceptionOutcome.Halt
                : base.OnException(ex, methodName, e);
        }

        public PMachineValue CreateInterface<T>(PMachine creator, IPrtValue payload = null)
            where T : PMachineValue
        {
            var createdInterface = PModule.linkMap[creator.interfaceName][typeof(T).Name];
            Assert(creates.Contains(createdInterface),
                $"Machine {GetType().Name} cannot create interface {createdInterface}, not in its creates set");
            var createMachine = PModule.interfaceDefinitionMap[createdInterface];
            var machineId = CreateActor(createMachine, createdInterface.Substring(2),
                new InitializeParametersEvent(new InitializeParameters(createdInterface, payload)));
            return new PMachineValue(machineId, PInterfaces.GetPermissions(createdInterface));
        }

        public void TrySendEvent(PMachineValue target, Event ev, object payload = null)
        {
            Assert(ev != null, "Machine cannot send a null event");
            Assert(target != null, "Machine in send cannot be null");
            Assert(sends.Contains(ev.GetType().Name),
                $"Event {ev.GetType().Name} is not in the sends set of the Machine {GetType().Name}");
            Assert(target.Permissions.Contains(ev.GetType().Name),
                $"Event {ev.GetType().Name} is not in the permissions set of the target machine");
            var oneArgConstructor = ev.GetType().GetConstructors().First(x => x.GetParameters().Length > 0);
            ev = (Event)oneArgConstructor.Invoke(new[] { payload , ev.Loc});

            ev.Sender = Id.ToString();
            ev.Receiver = target.Id.ToString();
            AnnounceInternal(ev);
            SendEvent(target.Id, ev);
        }

        public void TryRaiseEvent(Event ev, object payload = null)
        {
            Assert(ev != null, "Machine cannot raise a null event");
            var oneArgConstructor = ev.GetType().GetConstructors().First(x => x.GetParameters().Length > 0);
            ev = (Event)oneArgConstructor.Invoke(new[] { payload, ev.Loc });
            RaiseEvent(ev);
            throw new PNonStandardReturnException { ReturnKind = NonStandardReturn.Raise };
        }

        public Task<Event> TryReceiveEvent(params Type[] events)
        {
            return ReceiveEventAsync(events);
        }

        public void TryGotoState<T>(IPrtValue payload = null) where T : State
        {
            gotoPayload = payload;
            RaiseGotoStateEvent<T>();
            throw new PNonStandardReturnException { ReturnKind = NonStandardReturn.Goto };
        }

        public int TryRandomInt(int maxValue)
        {
            return RandomInteger(maxValue);
        }

        public int TryRandomInt(int minValue, int maxValue)
        {
            return minValue + RandomInteger(maxValue - minValue);
        }

        public bool TryRandomBool(int maxValue)
        {
            return RandomBoolean(maxValue);
        }

        public bool TryRandomBool()
        {
            return RandomBoolean();
        }

        public IPrtValue TryRandom(IPrtValue param)
        {
            switch (param)
            {
                case PrtInt maxValue:
                {
                    TryAssert(maxValue <= 10000, $"choose expects a parameter with at most 10000 choices, got {maxValue} choices instead.");
                    return (PrtInt)TryRandomInt(maxValue);
                }

                case PrtSeq seq:
                {
                    TryAssert(seq.Any(), "Trying to choose from an empty sequence!");
                    TryAssert(seq.Count <= 10000, $"choose expects a parameter with at most 10000 choices, got {seq.Count} choices instead.");
                    return seq[TryRandomInt(seq.Count)];
                }
                case PrtSet set:
                {
                    TryAssert(set.Any(), "Trying to choose from an empty set!");
                    TryAssert(set.Count <= 10000, $"choose expects a parameter with at most 10000 choices, got {set.Count} choices instead.");
                    return set.ElementAt(TryRandomInt(set.Count));
                }
                case PrtMap map:
                {
                    TryAssert(map.Any(), "Trying to choose from an empty map!");
                    TryAssert(map.Keys.Count <= 10000, $"choose expects a parameter with at most 10000 choices, got {map.Keys.Count} choices instead.");
                    return map.Keys.ElementAt(TryRandomInt(map.Keys.Count));
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
            var @event = (Event)oneArgConstructor.Invoke(new[] { payload, ev.Loc });
            var pText = payload == null ? "" : $" with payload {((IPrtValue)payload).ToEscapedString()}";

            Logger.WriteLine($"<AnnounceLog> '{Id}' announced event '{ev.GetType().Name}'{pText}.");

            // Log message to JSON output
            JsonLogger.AddLogType(JsonWriter.LogType.Announce);
            JsonLogger.LogDetails.Id = $"{Id}";
            JsonLogger.LogDetails.Event = ev.GetType().Name;
            if (payload != null)
            {
                JsonLogger.LogDetails.Payload = ((IPrtValue)payload).ToDict();
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

        public class InitializeParameters : IPrtValue
        {
            public InitializeParameters(string interfaceName, IPrtValue payload)
            {
                InterfaceName = interfaceName;
                Payload = payload;
            }

            public string InterfaceName { get; }
            public IPrtValue Payload { get; }

            public bool Equals(IPrtValue other)
            {
                throw new NotImplementedException();
            }

            public IPrtValue Clone()
            {
                throw new NotImplementedException();
            }

            public object ToDict()
            {
                throw new NotImplementedException();
            }
        }

        public class InitializeParametersEvent : PEvent
        {
            public InitializeParametersEvent(InitializeParameters payload) : base(payload, 0)
            {
            }
        }
    }
}