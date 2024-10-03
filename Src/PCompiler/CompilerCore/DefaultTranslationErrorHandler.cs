using System;
using System.IO;
using System.Linq;
using Antlr4.Runtime;
using Plang.Compiler.TypeChecker;
using Plang.Compiler.TypeChecker.AST;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.TypeChecker.AST.States;
using Plang.Compiler.TypeChecker.Types;

namespace Plang.Compiler
{
    public class DefaultTranslationErrorHandler : ITranslationErrorHandler
    {
        private readonly ILocationResolver locationResolver;

        public DefaultTranslationErrorHandler(ILocationResolver locationResolver)
        {
            this.locationResolver = locationResolver;
        }

        public Exception DuplicateStartState(
            ParserRuleContext location,
            State duplicateStart,
            State originalStart,
            Machine machine)
        {
            return IssueError(location,
                $"state '{duplicateStart.Name}' tries to replace start state '{originalStart.Name}' in machine '{machine.Name}'");
        }

        public Exception DuplicateEventAction(ParserRuleContext location, IStateAction existingAction, State state)
        {
            return IssueError(location,
                $"event handler in state '{state.Name}' duplicates handler at {locationResolver.GetLocation(existingAction.SourceLocation)}");
        }

        public Exception DuplicateStateExitHandler(ParserRuleContext location, Function existingHandler, State state)
        {
            return IssueError(location,
                $"exit handler in state '{state.Name}' duplicates exit handler at {locationResolver.GetLocation(existingHandler.SourceLocation)}");
        }

        public Exception DuplicateStateEntry(ParserRuleContext location, Function existingHandler, State state)
        {
            return IssueError(location,
                $"entry handler in state '{state.Name}' duplicates entry handler at {locationResolver.GetLocation(existingHandler.SourceLocation)}");
        }

        public Exception DuplicateDeclaration(ParserRuleContext location, IPDecl duplicate, IPDecl existing)
        {
            return IssueError(location,
                $"'{duplicate.Name}' duplicates declaration '{existing.Name}' at {locationResolver.GetLocation(existing.SourceLocation)}");
        }

        public Exception IncorrectArgumentCount(ParserRuleContext location, int actualCount, int expectedCount)
        {
            return IssueError(location,
                $"goto, function or constructor call expected {expectedCount} arguments, got {actualCount}");
        }

        public Exception MissingDeclaration(ParserRuleContext location, string declarationKind, string missingName)
        {
            return IssueError(location, $"could not find {declarationKind} '{missingName}'");
        }

        public Exception CircularTypeDef(ParserRuleContext location, TypeDef typeDef)
        {
            return IssueError(location, $"typedef '{typeDef.Name}' has a circular dependency");
        }

        public Exception DuplicateNamedTupleEntry(ParserRuleContext location, string duplicateName)
        {
            return IssueError(location, $"named tuple has duplicate field '{duplicateName}'");
        }

        public Exception IllegalTypeInCoerceExpr(ParserRuleContext location)
        {
            return IssueError(location, "expected an interface or int or float type");
        }

        public Exception NoMainOrTestCase(string message)
        {
            return new TranslationException(message);
        }

        public Exception IllegalInterfaceCoerce(ParserRuleContext context, PLanguageType oldType, PLanguageType newType)
        {
            var outlierEvent =
                newType.AllowedPermissions.Value.First(x => !oldType.AllowedPermissions.Value.Contains(x));
            return IssueError(context,
                $"illegal Coerce, {oldType.OriginalRepresentation} permissions is not a superset of {newType.OriginalRepresentation} (e.g., event {outlierEvent.Name})");
        }

        public Exception StaticFunctionNotAllowedAsHandler(ParserRuleContext funName, string name)
        {
            return IssueError(funName,
                $"global functions or foreign functions are not directly allowed as handlers, {name}");
        }

        public Exception TypeMismatch(ParserRuleContext location, PLanguageType actual, params PLanguageType[] expected)
        {
            return IssueError(location,
                $"got type: {actual.OriginalRepresentation}, expected: {string.Join("; ", expected.Select(t => t.OriginalRepresentation))}");
        }

        public Exception TypeMismatch(IPExpr expr, params TypeKind[] expected)
        {
            return IssueError(expr.SourceLocation,
                $"got type: {expr.Type.OriginalRepresentation}, expected: {string.Join(", ", expected.Select(e => e.Name))}");
        }

        public Exception MissingNamedTupleEntry(PParser.IdenContext location,
            NamedTupleType namedTuple)
        {
            return IssueError(location,
                $"named tuple type {namedTuple.OriginalRepresentation} has no '{location.GetText()}' field");
        }

        public Exception OutOfBoundsTupleAccess(PParser.IntContext location, TupleType tuple)
        {
            return IssueError(
                location, $"tuple type {tuple.OriginalRepresentation} has no '{location.GetText()}' field");
        }

        public Exception IncomparableTypes(ParserRuleContext location, PLanguageType lhsType, PLanguageType rhsType)
        {
            return IssueError(location,
                $"types {lhsType.OriginalRepresentation} and {rhsType.OriginalRepresentation} are incomparable");
        }

        public Exception MisplacedThis(PParser.PrimitiveContext location)
        {
            return IssueError(location, location.THIS().Symbol, "keyword THIS used outside machine");
        }

        public Exception BinOpTypeMismatch(
            PParser.BinExprContext location,
            PLanguageType lhsType,
            PLanguageType rhsType)
        {
            return IssueError(location,
                location.op,
                $"expected either both float or both int; got {lhsType.OriginalRepresentation} and {rhsType.OriginalRepresentation}");
        }

        public Exception MoreThanOneParameterForHandlers(ParserRuleContext sourceLocation, int count)
        {
            return IssueError(sourceLocation,
                $"functions at entry or exit and do or goto transitions cannot take more than 1 parameter, provided function expects {count} parameters");
        }

        public Exception ParseFailure(FileInfo file, string message)
        {
            return new TranslationException($"[{file.Name}] parse error: {message}");
        }

        public Exception IllegalChooseSubExprType(PParser.ChooseExprContext context, PLanguageType subExprType)
        {
            return IssueError(context,
                $"choose expects a parameter of type int (max value) or a collection type (seq, set, or map) got a parameter of type {subExprType}");
        }

        public Exception IllegalChooseSubExprValue(PParser.ChooseExprContext context, int numChoices)
        {
            return IssueError(context,
                $"choose expects a parameter with at most 10000 choices, got {numChoices} choices instead.");
        }

        public Exception IllegalFunctionUsedInSpecMachine(Function function, Machine callerOwner)
        {
            return IssueError(function.SourceLocation,
                $"Method {DeclarationName(function)} is non-deterministic or has side-effects (new or send or receive or announce), hence cannot be used in spec machine {DeclarationName(callerOwner)}.");
        }

        public Exception EmittedNullEvent(IPExpr evtExpr)
        {
            return IssueError(evtExpr.SourceLocation, "cannot send null events");
        }

        public Exception MissingStartState(Machine machine)
        {
            return IssueError(machine.SourceLocation, $"Value {machine.Name} has no start state");
        }

        public Exception ChangedStateMidTransition(ParserRuleContext location, Function method)
        {
            return IssueError(location,
                $"Method {DeclarationName(method)} is used as a transition function, but might change state here.");
        }

        public Exception InvalidPrintFormat(PParser.PrintStmtContext context, IToken symbol)
        {
            return IssueError(context,
                symbol,
                "Print format placeholders must contain only digits. Escape braces by doubling them.");
        }

        public Exception InvalidStringExprFormat(PParser.FormatedStringContext context, IToken symbol)
        {
            return IssueError(context,
                symbol,
                "String expr format placeholders must contain only digits. Escape braces by doubling them.");
        }

        public Exception InvalidBindExpr(ParserRuleContext location, string message)
        {
            return IssueError(location, $"invalid bind operation. {message}");
        }

        public Exception NotClosed(ParserRuleContext sourceLocation, string message)
        {
            return IssueError(sourceLocation, $"Module not closed. {message}");
        }

        public Exception NoMain(ParserRuleContext sourceLocation, string message)
        {
            return IssueError(sourceLocation, $"Illegal main machine. {message}");
        }

        public Exception InvalidAssertExpr(ParserRuleContext location, Machine monitor, PEvent illegalEvent)
        {
            return IssueError(location,
                $"invalid assert operation. event {illegalEvent.Name} in observes set of {monitor.Name} is not in the sends set of the module");
        }

        public Exception InvalidAssertExpr(ParserRuleContext location, Machine monitor)
        {
            return IssueError(location,
                $"invalid assert operation. monitor {monitor.Name} already attached in the module");
        }

        public Exception InvalidHideEventExpr(ParserRuleContext location, string message)
        {
            return IssueError(location, $"invalid hide event operation. {message}");
        }

        public Exception InvalidHideInterfaceExpr(ParserRuleContext location, string message)
        {
            return IssueError(location, $"invalid hide interface operation. {message}");
        }

        public Exception InvalidRenameExpr(ParserRuleContext location, string message)
        {
            return IssueError(location, $"invalid rename operation. {message}");
        }

        public Exception InvalidCompositionExpr(ParserRuleContext location, string message)
        {
            return IssueError(location, $"invalid composition operation. {message}");
        }

        public Exception InternalError(ParserRuleContext location, Exception inner)
        {
            return IssueError(location, "[Internal Error]: " + inner.Message);
        }

        public Exception TwoStartStates(Machine machine, State state)
        {
            return IssueError(machine.SourceLocation,
                $"machine {machine.Name} has two start states, {machine.StartState.Name} and {state.Name}.");
        }

        public Exception ValueOutOfRange(ParserRuleContext location, string type)
        {
            return IssueError(location, $"value not in range for {type}.");
        }

        public Exception NullTransitionInMonitor(ParserRuleContext location, Machine monitor)
        {
            return IssueError(location, $"cannot transition on null event in monitor {monitor.Name}");
        }

        public Exception IllegalMonitorOperation(ParserRuleContext location, IToken operation, Machine monitor)
        {
            return IssueError(location, operation,
                $"{monitor.Name}: $, $$, this, new, send, announce, receive, and pop are not allowed in monitors");
        }

        public Exception DeferredEventInMonitor(ParserRuleContext location, Machine monitor)
        {
            return IssueError(location, $"{monitor.Name}: Cannot defer events in monitors.");
        }

        public Exception NotAllPathsReturn(Function function)
        {
            return IssueError(function.Body.SourceLocation,
                $"not all paths in function {DeclarationName(function)} return a value!");
        }

        public Exception ExpectedMonitor(ParserRuleContext location, Machine machine)
        {
            return IssueError(location, $"expected a specification machine, but got {machine.Name}");
        }

        public Exception RaiseEventInNonVoidFunction(ParserRuleContext context)
        {
            return IssueError(context, "raising an event only allowed in functions that do not return a value.");
        }

        public Exception ChangeStateInNonVoidFunction(ParserRuleContext context)
        {
            return IssueError(context, "changing a state only allowed in functions that do not return a value.");
        }

        public Exception PrintStmtLinearArgument(ParserRuleContext argSourceLocation)
        {
            return IssueError(argSourceLocation, "Print is a pure statement and so does not require linear arguments.");
        }

        public Exception StringAssignStmtLinearArgument(ParserRuleContext argSourceLocation)
        {
            return IssueError(argSourceLocation, "String interpolation does not support linear arguments.");
        }

        public Exception DuplicateReceiveCase(ParserRuleContext location, PEvent pEvent)
        {
            return IssueError(location, $"Event {pEvent.Name} appears twice in receive statement argument list");
        }

        public Exception BareLoopControlFlow(string stmtName, ParserRuleContext context)
        {
            return IssueError(context, $"Loop control flow statement '{stmtName}' cannot appear outside a loop body");
        }

        public Exception ExitFunctionCannotTakeParameters(ParserRuleContext sourceLocation, int count)
        {
            return IssueError(sourceLocation,
                $"Exit functions cannot have input parameters, the provided function expects {count} parameters");
        }

        private Exception IssueError(ParserRuleContext location, string message)
        {
            return IssueError(location, location.Start, message);
        }

        private Exception IssueError(ParserRuleContext ctx, IToken location, string message)
        {
            return new TranslationException($"[{locationResolver.GetLocation(ctx, location)}] {message}");
        }

        private string DeclarationName(IPDecl method)
        {
            return method.Name.Length > 0 ? method.Name : $"at {locationResolver.GetLocation(method.SourceLocation)}";
        }

        public string SpecObservesSetIncompleteWarning(ParserRuleContext ctx, PEvent ev, Machine machine)
        {
            return
                $"[!Warning!]\n[{locationResolver.GetLocation(ctx, ctx.start)}] Event {ev.Name} is not in the observes list of the spec machine {machine.Name}. The event-handler is never triggered as the event is not observed by the spec.\n[!Warning!]";
        }

        public Exception DuplicateBindings(ParserRuleContext ctx, Interface @interface)
        {
            return IssueError(ctx, $"Interface or machine {@interface.Name} is mentioned or bounded multiple times in the module");
        }
    }
}