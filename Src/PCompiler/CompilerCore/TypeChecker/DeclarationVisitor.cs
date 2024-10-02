using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Antlr4.Runtime.Tree;
using Plang.Compiler.TypeChecker.AST;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.TypeChecker.AST.States;
using Plang.Compiler.TypeChecker.Types;
using Plang.Compiler.Util;

namespace Plang.Compiler.TypeChecker
{
    public class DeclarationVisitor : PParserBaseVisitor<object>
    {
        private readonly StackProperty<Machine> currentMachine = new StackProperty<Machine>();
        private readonly StackProperty<Scope> currentScope;
        private readonly ParseTreeProperty<IPDecl> nodesToDeclarations;

        private DeclarationVisitor(
            ITranslationErrorHandler handler,
            Scope topLevelScope,
            ParseTreeProperty<IPDecl> nodesToDeclarations)
        {
            Handler = handler;
            currentScope = new StackProperty<Scope>(topLevelScope);
            this.nodesToDeclarations = nodesToDeclarations;
        }

        private Scope CurrentScope => currentScope.Value;
        private Machine CurrentMachine => currentMachine.Value;
        private ITranslationErrorHandler Handler { get; }

        public static void PopulateDeclarations(
            ITranslationErrorHandler handler,
            Scope topLevelScope,
            PParser.ProgramContext context,
            ParseTreeProperty<IPDecl> nodesToDeclarations)
        {
            var visitor = new DeclarationVisitor(handler, topLevelScope, nodesToDeclarations);
            visitor.Visit(context);
        }

        #region Events

        public override object VisitEventDecl(PParser.EventDeclContext context)
        {
            // EVENT name=Iden
            var pEvent = (Event) nodesToDeclarations.Get(context);

            // cardinality?
            var hasAssume = context.cardinality()?.ASSUME() != null;
            var hasAssert = context.cardinality()?.ASSERT() != null;
            var cardinality = int.Parse(context.cardinality()?.IntLiteral().GetText() ?? "-1");
            pEvent.Assume = hasAssume ? cardinality : -1;
            pEvent.Assert = hasAssert ? cardinality : -1;

            // (COLON type)?
            pEvent.PayloadType = ResolveType(context.type());

            // SEMI
            return pEvent;
        }
        
        public override object VisitDelayDecl(PParser.DelayDeclContext context)
        {

            var delay = (Delay) nodesToDeclarations.Get(context);

            if (CurrentScope.Events.All(ev => ev.Name != delay.Name))
            {
                throw Handler.MissingDeclaration(context, "event", delay.Name);
            }
            var evDecl = CurrentScope.Events.First(ev => ev.Name == delay.Name);
            evDecl.Distribution = context.StringLiteral().GetText();
            evDecl.IsOrdered = context.BoolLiteral() == null || bool.Parse(context.BoolLiteral().GetText());
            
            return delay;
        }

        #endregion

        #region Interfaces

        public override object VisitInterfaceDecl(PParser.InterfaceDeclContext context)
        {
            // TYPE name=Iden
            var mInterface = (Interface) nodesToDeclarations.Get(context);

            // LPAREN type? RPAREN
            mInterface.PayloadType = ResolveType(context.type());

            IEventSet eventSet;
            if (context.RECEIVES() == null)
            {
                eventSet = CurrentScope.UniversalEventSet;
            }
            else
            {
                eventSet = new EventSet();
                if (context.nonDefaultEventList()?._events is IList<PParser.NonDefaultEventContext> events)
                    foreach (var eventContext in events)
                        eventSet.AddEvent((Event) Visit(eventContext));
            }

            mInterface.ReceivableEvents = eventSet;
            return mInterface;
        }

        #endregion

        private PLanguageType ResolveType(PParser.TypeContext typeContext)
        {
            return TypeResolver.ResolveType(typeContext, CurrentScope, Handler);
        }

        private Function CreateAnonFunction(PParser.AnonEventHandlerContext context)
        {
            var fun = new Function(context)
            {
                Owner = CurrentMachine,
                Scope = CurrentScope.MakeChildScope()
            };

            CurrentMachine.AddMethod(fun);

            if (context.funParam() is PParser.FunParamContext paramContext)
            {
                var param = fun.Scope.Put(paramContext.name.GetText(), paramContext, VariableRole.Param);
                param.Type = ResolveType(paramContext.type());
                nodesToDeclarations.Put(paramContext, param);
                fun.Signature.Parameters.Add(param);
            }

            nodesToDeclarations.Put(context, fun);
            return fun;
        }

        private Function CreateAnonFunction(PParser.NoParamAnonEventHandlerContext context)
        {
            var fun = new Function(context)
            {
                Owner = CurrentMachine,
                Scope = CurrentScope.MakeChildScope()
            };

            CurrentMachine.AddMethod(fun);

            nodesToDeclarations.Put(context, fun);
            return fun;
        }

        private State FindState(PParser.StateNameContext context)
        {
            var scope = CurrentMachine.Scope;

            if (!scope.Get(context.state.GetText(), out State state))
                throw Handler.MissingDeclaration(context.state, "state", context.state.GetText());
            return state;
        }

        #region Typedefs

        public override object VisitForeignTypeDef(PParser.ForeignTypeDefContext context)
        {
            // TYPE name=iden
            var typedef = (TypeDef) nodesToDeclarations.Get(context);
            // SEMI
            typedef.Type = new ForeignType(typedef.Name);
            return typedef;
        }

        public override object VisitPTypeDef(PParser.PTypeDefContext context)
        {
            // TYPE name=iden
            var typedef = (TypeDef) nodesToDeclarations.Get(context);
            // ASSIGN type
            typedef.Type = ResolveType(context.type());
            // SEMI
            return typedef;
        }

        #endregion

        #region Enum typedef

        public override object VisitEnumTypeDefDecl(PParser.EnumTypeDefDeclContext context)
        {
            // ENUM name=iden
            var pEnum = (PEnum) nodesToDeclarations.Get(context);

            // LBRACE enumElemList RBRACE
            if (context.enumElemList() is PParser.EnumElemListContext elemList)
            {
                var elems = (EnumElem[]) Visit(elemList);
                for (var i = 0; i < elems.Length; i++)
                {
                    var elem = elems[i];
                    elem.Value = i;
                    pEnum.AddElement(elem);
                }
            }
            // | LBRACE numberedEnumElemList RBRACE
            else if (context.numberedEnumElemList() is PParser.NumberedEnumElemListContext numberedElemList)
            {
                var numberedElems = (EnumElem[]) Visit(numberedElemList);
                foreach (var elem in numberedElems) pEnum.AddElement(elem);
            }
            else
            {
                Debug.Fail("grammar requires enum declarations to have element lists");
            }

            return pEnum;
        }

        public override object VisitEnumElemList(PParser.EnumElemListContext context)
        {
            // enumElem (COMMA enumElem)*
            return context.enumElem().Select(Visit).Cast<EnumElem>().ToArray();
        }

        public override object VisitEnumElem(PParser.EnumElemContext context)
        {
            // name=iden
            return (EnumElem) nodesToDeclarations.Get(context);
        }

        public override object VisitNumberedEnumElemList(PParser.NumberedEnumElemListContext context)
        {
            // numberedEnumElem (COMMA numberedEnumElem)*
            return context.numberedEnumElem().Select(Visit).Cast<EnumElem>().ToArray();
        }

        public override object VisitNumberedEnumElem(PParser.NumberedEnumElemContext context)
        {
            // name=iden ASSIGN value=IntLiteral
            var elem = (EnumElem) nodesToDeclarations.Get(context);
            elem.Value = int.Parse(context.value.Text);
            return elem;
        }

        #endregion

        #region Event sets

        public override object VisitEventSetDecl(PParser.EventSetDeclContext context)
        {
            // EVENTSET name=iden
            var es = (NamedEventSet) nodesToDeclarations.Get(context);
            // ASSIGN LBRACE eventSetLiteral RBRACE
            es.AddEvents((Event[]) Visit(context.eventSetLiteral()));
            // SEMI
            return es;
        }

        public override object VisitEventSetLiteral(PParser.EventSetLiteralContext context)
        {
            // events+=nonDefaultEvent (COMMA events+=nonDefaultEvent)*
            return context._events.Select(Visit).Cast<Event>().ToArray();
        }

        public override object VisitNonDefaultEvent(PParser.NonDefaultEventContext context)
        {
            // HALT | iden
            var eventName = context.GetText();
            if (!CurrentScope.Lookup(eventName, out Event pEvent))
                throw Handler.MissingDeclaration(context, "event", eventName);
            return pEvent;
        }

        #endregion

        #region Machines

        public override object VisitImplMachineDecl(PParser.ImplMachineDeclContext context)
        {
            // MACHINE name=iden
            var machine = (Machine) nodesToDeclarations.Get(context);

            // cardinality?
            var hasAssume = context.cardinality()?.ASSUME() != null;
            var hasAssert = context.cardinality()?.ASSERT() != null;
            var cardinality = long.Parse(context.cardinality()?.IntLiteral().GetText() ?? "-1");
            if (cardinality > uint.MaxValue) throw Handler.ValueOutOfRange(context.cardinality(), "uint32");
            machine.Assume = hasAssume ? (uint?) cardinality : null;
            machine.Assert = hasAssert ? (uint?) cardinality : null;

            // receivesSends*
            foreach (var receivesSends in context.receivesSends())
            {
                var recvSendTuple = (Tuple<string, Event[]>) Visit(receivesSends);
                var eventSetType = recvSendTuple.Item1;
                if (eventSetType.Equals("RECV", StringComparison.InvariantCulture))
                {
                    if (machine.Receives == null) machine.Receives = new EventSet();
                    foreach (var @event in recvSendTuple.Item2) machine.Receives.AddEvent(@event);
                }
                else if (eventSetType.Equals("SEND", StringComparison.InvariantCulture))
                {
                    if (machine.Sends == null) machine.Sends = new EventSet();
                    foreach (var @event in recvSendTuple.Item2) machine.Sends.AddEvent(@event);
                }
                else
                {
                    Debug.Fail("grammar changed surrounding receives/sends.");
                }
            }

            if (machine.Receives == null) machine.Receives = CurrentScope.UniversalEventSet;

            if (machine.Sends == null) machine.Sends = CurrentScope.UniversalEventSet;

            // machineBody
            using (currentScope.NewContext(machine.Scope))
            using (currentMachine.NewContext(machine))
            {
                Visit(context.machineBody());
            }

            // initialize the corresponding interface
            currentScope.Value.Get(machine.Name, out Interface @interface);
            @interface.ReceivableEvents = machine.Receives;
            @interface.PayloadType = machine.PayloadType;

            return machine;
        }

        public override object VisitMachineReceive(PParser.MachineReceiveContext context)
        {
            var events = context.eventSetLiteral() == null
                ? new Event[0]
                : (Event[]) Visit(context.eventSetLiteral());
            return Tuple.Create("RECV", events);
        }

        public override object VisitMachineSend(PParser.MachineSendContext context)
        {
            var events = context.eventSetLiteral() == null
                ? new Event[0]
                : (Event[]) Visit(context.eventSetLiteral());
            return Tuple.Create("SEND", events);
        }

        public override object VisitSpecMachineDecl(PParser.SpecMachineDeclContext context)
        {
            // SPEC name=Iden
            var specMachine = (Machine) nodesToDeclarations.Get(context);

            // spec machines neither send nor receive events.
            specMachine.Receives = new EventSet();
            specMachine.Sends = new EventSet();

            // OBSERVES eventSetLiteral
            specMachine.Observes = new EventSet();
            foreach (var pEvent in (Event[]) Visit(context.eventSetLiteral())) specMachine.Observes.AddEvent(pEvent);

            // machineBody
            using (currentScope.NewContext(specMachine.Scope))
            using (currentMachine.NewContext(specMachine))
            {
                Visit(context.machineBody());
            }

            return specMachine;
        }

        public override object VisitMachineBody(PParser.MachineBodyContext context)
        {
            foreach (var machineEntryContext in context.machineEntry())
                switch (Visit(machineEntryContext))
                {
                    case Variable[] fields:
                        CurrentMachine.AddFields(fields);
                        break;
                    case Function method:
                        CurrentMachine.AddMethod(method);
                        break;
                    case State state:
                        CurrentMachine.AddState(state);
                        break;
                    default:
                        throw Handler.InternalError(machineEntryContext,
                            new ArgumentOutOfRangeException(nameof(context)));
                }
            return null;
        }

        public override object VisitMachineEntry(PParser.MachineEntryContext context)
        {
            var subExpr = context.varDecl() ??
                          context.funDecl() ??
                          (IParseTree) context.stateDecl() ??
                          throw Handler.InternalError(context, new ArgumentOutOfRangeException(nameof(context)));
            return Visit(subExpr);
        }

        public override object VisitVarDecl(PParser.VarDeclContext context)
        {
            // COLON type
            var variableType = ResolveType(context.type());

            // VAR idenList
            var variables = new Variable[context.idenList()._names.Count];
            var varNameCtxs = context.idenList()._names;
            for (var i = 0; i < varNameCtxs.Count; i++)
            {
                var variable = (Variable) nodesToDeclarations.Get(varNameCtxs[i]);
                variable.Type = variableType;
                variables[i] = variable;
            }

            // SEMI
            return variables;
        }

        public override object VisitStateDecl(PParser.StateDeclContext context)
        {
            // STATE name=iden
            var state = (State) nodesToDeclarations.Get(context);
            state.OwningMachine = CurrentMachine;

            // START?
            state.IsStart = context.START() != null;

            // temperature=(HOT | COLD)?
            state.Temperature = context.temperature == null
                ? StateTemperature.Warm
                : context.HOT() != null
                    ? StateTemperature.Hot
                    : StateTemperature.Cold;

            // LBRACE stateBodyItem* RBRACE ;
            foreach (var stateBodyItemContext in context.stateBodyItem())
                switch (Visit(stateBodyItemContext))
                {
                    case IStateAction[] actions:
                        foreach (var action in actions)
                        {
                            if (state.HasHandler(action.Trigger))
                                throw Handler.DuplicateEventAction(action.SourceLocation, state[action.Trigger], state);

                            if (action.Trigger.Name.Equals("null") && CurrentMachine.IsSpec)
                                throw Handler.NullTransitionInMonitor(action.SourceLocation, CurrentMachine);
                            state[action.Trigger] = action;
                        }

                        break;
                    case Tuple<string, Function> entryOrExit:
                        if (entryOrExit.Item1.Equals("ENTRY"))
                        {
                            if (state.Entry != null)
                                throw Handler.DuplicateStateEntry(stateBodyItemContext, state.Entry, state);
                            state.Entry = entryOrExit.Item2;
                        }
                        else
                        {
                            if (state.Exit != null)
                                throw Handler.DuplicateStateExitHandler(stateBodyItemContext, state.Exit, state);
                            state.Exit = entryOrExit.Item2;
                        }

                        break;
                    default:
                        throw Handler.InternalError(stateBodyItemContext,
                            new ArgumentOutOfRangeException(nameof(context)));
                }

            if (state.IsStart)
            {
                if (CurrentMachine.StartState != null)
                    throw Handler.DuplicateStartState(context, state, CurrentMachine.StartState, CurrentMachine);
                CurrentMachine.StartState = state;
                CurrentMachine.PayloadType =
                    state.Entry?.Signature.Parameters.ElementAtOrDefault(0)?.Type ?? PrimitiveType.Null;
            }

            return state;
        }

        public override object VisitStateEntry(PParser.StateEntryContext context)
        {
            Function fun;
            if (context.anonEventHandler() != null)
            {
                fun = CreateAnonFunction(context.anonEventHandler());
            }
            else
            {
                var funName = context.funName.GetText();
                if (!CurrentScope.Lookup(funName, out fun))
                    throw Handler.MissingDeclaration(context.funName, "function", funName);
            }

            fun.Role |= FunctionRole.EntryHandler;

            return Tuple.Create("ENTRY", fun);
        }

        public override object VisitStateExit(PParser.StateExitContext context)
        {
            Function fun;
            if (context.noParamAnonEventHandler() != null)
            {
                fun = CreateAnonFunction(context.noParamAnonEventHandler());
            }
            else
            {
                var funName = context.funName.GetText();
                if (!CurrentScope.Lookup(funName, out fun))
                    throw Handler.MissingDeclaration(context.funName, "function", funName);
            }

            fun.Role |= FunctionRole.ExitHandler;
            return Tuple.Create("EXIT", fun);
        }

        public override object VisitStateDefer(PParser.StateDeferContext context)
        {
            if (CurrentMachine.IsSpec) throw Handler.DeferredEventInMonitor(context, CurrentMachine);

            // DEFER nonDefaultEventList
            var eventContexts = context.nonDefaultEventList()._events;
            var actions = new IStateAction[eventContexts.Count];
            for (var i = 0; i < eventContexts.Count; i++)
            {
                var token = eventContexts[i];
                if (!CurrentScope.Lookup(token.GetText(), out Event evt))
                    throw Handler.MissingDeclaration(token, "event", token.GetText());
                actions[i] = new EventDefer(token, evt);
            }

            return actions;
        }

        public override object VisitStateIgnore(PParser.StateIgnoreContext context)
        {
            // IGNORE nonDefaultEventList
            var actions = new List<IStateAction>();
            foreach (var token in context.nonDefaultEventList()._events)
            {
                if (!CurrentScope.Lookup(token.GetText(), out Event evt))
                    throw Handler.MissingDeclaration(token, "event", token.GetText());
                actions.Add(new EventIgnore(token, evt));
            }

            return actions.ToArray();
        }

        public override object VisitOnEventDoAction(PParser.OnEventDoActionContext context)
        {
            Function fun;
            if (context.anonEventHandler() is PParser.AnonEventHandlerContext anonEventHandler)
            {
                // DO [...] anonEventHandler
                fun = CreateAnonFunction(anonEventHandler);
            }
            else
            {
                // DO funName=Iden
                var funName = context.funName.GetText();
                if (!CurrentScope.Lookup(funName, out fun))
                    throw Handler.MissingDeclaration(context.funName, "function", funName);
            }

            // TODO: is this correct?
            fun.Role |= FunctionRole.EventHandler;

            // ON eventList
            var actions = new List<IStateAction>();
            foreach (var eventIdContext in context.eventList().eventId())
            {
                if (!CurrentScope.Lookup(eventIdContext.GetText(), out Event evt))
                    throw Handler.MissingDeclaration(eventIdContext, "event", eventIdContext.GetText());

                actions.Add(new EventDoAction(eventIdContext, evt, fun));
            }

            return actions.ToArray();
        }


        public override object VisitOnEventGotoState(PParser.OnEventGotoStateContext context)
        {
            Function transitionFunction;
            if (context.funName != null)
            {
                // WITH funName=Iden
                var funName = context.funName.GetText();
                if (!CurrentScope.Lookup(funName, out transitionFunction))
                    throw Handler.MissingDeclaration(context.funName, "function", funName);
                transitionFunction.Role |= FunctionRole.TransitionFunction;
            }
            else if (context.anonEventHandler() != null)
            {
                // WITH anonEventHandler
                transitionFunction = CreateAnonFunction(context.anonEventHandler());
                transitionFunction.Role |= FunctionRole.TransitionFunction;
            }
            else
            {
                // SEMI
                transitionFunction = null;
            }

            // GOTO stateName
            var target = FindState(context.stateName());

            // ON eventList
            var actions = new List<IStateAction>();
            foreach (var eventIdContext in context.eventList().eventId())
            {
                if (!CurrentScope.Lookup(eventIdContext.GetText(), out Event evt))
                    throw Handler.MissingDeclaration(eventIdContext, "event", eventIdContext.GetText());

                actions.Add(new EventGotoState(eventIdContext, evt, target, transitionFunction));
            }

            return actions.ToArray();
        }

        #endregion

        #region Functions

        public override object VisitPFunDecl(PParser.PFunDeclContext context)
        {
            // FUN name=Iden
            var fun = (Function) nodesToDeclarations.Get(context);

            // LPAREN funParamList? RPAREN
            var paramList = context.funParamList() != null
                ? (Variable[]) Visit(context.funParamList())
                : new Variable[0];
            fun.Signature.Parameters.AddRange(paramList);

            // (COLON type)?
            fun.Signature.ReturnType = ResolveType(context.type());

            // functionBody
            // handled in later phase.
            return fun;
        }

        public override object VisitForeignFunDecl(PParser.ForeignFunDeclContext context)
        {
            // FUN name=Iden
            var fun = (Function) nodesToDeclarations.Get(context);

            // LPAREN funParamList? RPAREN
            var paramList = context.funParamList() != null
                ? (Variable[]) Visit(context.funParamList())
                : new Variable[0];
            fun.Signature.Parameters.AddRange(paramList);

            // (COLON type)?
            fun.Signature.ReturnType = ResolveType(context.type());

            // SEMI
            // no function body
            fun.Role |= FunctionRole.Foreign;

            // Creates
            foreach(var createdInterface in context._interfaces)
            {
                if (CurrentScope.Lookup(createdInterface.GetText(), out Interface @interface))
                    fun.AddCreatesInterface(@interface);
                else
                    throw Handler.MissingDeclaration(createdInterface, "interface", createdInterface.GetText());
            }
            return fun;
        }

        public override object VisitFunParamList(PParser.FunParamListContext context)
        {
            // funParamList : funParam (COMMA funParam)* ;
            return context.funParam().Select(Visit).Cast<Variable>().ToArray();
        }

        public override object VisitFunParam(PParser.FunParamContext context)
        {
            // funParam : name=iden COLON type ;
            var param = (Variable) nodesToDeclarations.Get(context);
            param.Type = ResolveType(context.type());
            return param;
        }

        #endregion
    }
}