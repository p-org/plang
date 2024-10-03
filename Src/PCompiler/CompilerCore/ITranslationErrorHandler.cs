using System;
using System.IO;
using Antlr4.Runtime;
using Plang.Compiler.TypeChecker.AST;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.TypeChecker.AST.States;
using Plang.Compiler.TypeChecker.Types;

namespace Plang.Compiler
{
    public interface ITranslationErrorHandler
    {
        Exception DuplicateStartState(
            ParserRuleContext location,
            State duplicateStart,
            State originalStart,
            Machine machine);

        Exception DuplicateEventAction(ParserRuleContext location, IStateAction existingAction, State state);

        Exception DuplicateStateExitHandler(ParserRuleContext location, Function existingHandler, State state);

        Exception DuplicateStateEntry(ParserRuleContext location, Function existingHandler, State state);

        Exception DuplicateDeclaration(ParserRuleContext location, IPDecl duplicate, IPDecl existing);

        Exception IncorrectArgumentCount(ParserRuleContext location, int actualCount, int expectedCount);

        Exception MissingDeclaration(ParserRuleContext location, string declarationKind, string missingName);

        Exception CircularTypeDef(ParserRuleContext location, TypeDef typeDef);

        Exception DuplicateNamedTupleEntry(ParserRuleContext location, string duplicateName);

        Exception TypeMismatch(ParserRuleContext location, PLanguageType actual, params PLanguageType[] expected);

        Exception TypeMismatch(IPExpr expr, params TypeKind[] expected);

        Exception MissingNamedTupleEntry(PParser.IdenContext location, NamedTupleType namedTuple);

        Exception OutOfBoundsTupleAccess(PParser.IntContext location, TupleType tuple);

        Exception IncomparableTypes(ParserRuleContext location, PLanguageType lhsType, PLanguageType rhsType);

        Exception MisplacedThis(PParser.PrimitiveContext location);

        Exception BinOpTypeMismatch(PParser.BinExprContext location, PLanguageType lhsType, PLanguageType rhsType);

        Exception ParseFailure(FileInfo file, string message);

        Exception EmittedNullEvent(IPExpr evtExpr);

        Exception MissingStartState(Machine machine);

        Exception ChangedStateMidTransition(ParserRuleContext location, Function method);

        Exception InvalidPrintFormat(PParser.PrintStmtContext context, IToken symbol);

        Exception InvalidStringExprFormat(PParser.FormatedStringContext context, IToken symbol);

        // module system related
        Exception InvalidBindExpr(ParserRuleContext location, string message);

        Exception InvalidAssertExpr(ParserRuleContext location, Machine monitor, PEvent illegalEvent);

        Exception InvalidAssertExpr(ParserRuleContext location, Machine monitor);

        Exception InvalidHideEventExpr(ParserRuleContext location, string message);

        Exception InvalidHideInterfaceExpr(ParserRuleContext location, string message);

        Exception InvalidRenameExpr(ParserRuleContext location, string message);

        Exception InvalidCompositionExpr(ParserRuleContext location, string message);

        Exception NotClosed(ParserRuleContext sourceLocation, string v);

        // General errors
        Exception InternalError(ParserRuleContext location, Exception inner);

        Exception TwoStartStates(Machine machine, State state);

        Exception ValueOutOfRange(ParserRuleContext location, string type);

        Exception NullTransitionInMonitor(ParserRuleContext location, Machine monitor);

        Exception IllegalMonitorOperation(ParserRuleContext location, IToken operation, Machine monitor);

        Exception DeferredEventInMonitor(ParserRuleContext location, Machine monitor);

        Exception NotAllPathsReturn(Function function);

        Exception ExpectedMonitor(ParserRuleContext location, Machine machine);

        Exception RaiseEventInNonVoidFunction(ParserRuleContext context);

        Exception ChangeStateInNonVoidFunction(ParserRuleContext context);


        Exception DuplicateReceiveCase(ParserRuleContext location, PEvent pEvent);

        Exception NoMain(ParserRuleContext sourceLocation, string v);

        Exception NoMainOrTestCase(string v);

        Exception IllegalTypeInCoerceExpr(ParserRuleContext context);

        Exception IllegalInterfaceCoerce(ParserRuleContext context, PLanguageType oldType, PLanguageType newType);

        Exception StaticFunctionNotAllowedAsHandler(ParserRuleContext funName, string name);

        Exception BareLoopControlFlow(string stmtName, ParserRuleContext context);

        Exception ExitFunctionCannotTakeParameters(ParserRuleContext sourceLocation, int count);

        Exception MoreThanOneParameterForHandlers(ParserRuleContext sourceLocation, int count);

        Exception IllegalChooseSubExprType(PParser.ChooseExprContext context, PLanguageType subExprType);
        Exception IllegalChooseSubExprValue(PParser.ChooseExprContext context, int numChoices);
        Exception IllegalFunctionUsedInSpecMachine(Function function, Machine callerOwner);

        String SpecObservesSetIncompleteWarning(ParserRuleContext loc, PEvent ev, Machine machine);
        Exception DuplicateBindings(ParserRuleContext loc, Interface @interface);
    }
}