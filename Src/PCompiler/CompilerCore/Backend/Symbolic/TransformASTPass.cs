using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Plang.Compiler.Backend.ASTExt;
using Plang.Compiler.TypeChecker;
using Plang.Compiler.TypeChecker.AST;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.TypeChecker.AST.Expressions;
using Plang.Compiler.TypeChecker.AST.Statements;
using Plang.Compiler.TypeChecker.AST.States;

namespace Plang.Compiler.Backend.Symbolic
{
    class TransformASTPass
    {

        static private int continuationNumber = 0;
        static private int whileNumber = 0;
        static private int callNum = 0;

        static public List<IPDecl> GetTransformedDecls(Scope globalScope)
        {
            continuationNumber = 0;
            callNum = 0;
            var decls = new List<IPDecl>();

            foreach (var decl in globalScope.AllDecls)
            {
                var result = TransformDecl(decl);
                if (result != null)
                    decls.Add(result);
            }

            continuationNumber = 0;
            callNum = 0;
            return decls;
        }

        static private IPDecl TransformDecl(IPDecl decl)
        {
            switch (decl)
            {
                case Function function:
                    if (function.IsForeign)
                        return function;
                    else
                    if (function.CanReceive == true)
                    {
                        return null;
                    }
                    else
                    {
                        return function;
                    }
                case Machine machine:
                    if (machine.Receives.Events.GetEnumerator().MoveNext())
                        return TransformMachine(machine);
                    else return machine;
                default:
                    return decl;
            }
        }

        static private Machine TransformMachine(Machine machine)
        {
            var transformedMachine = new Machine(machine.Name, machine.SourceLocation);
            transformedMachine.Assume = machine.Assume;
            transformedMachine.Assert = machine.Assert;
            transformedMachine.Receives = machine.Receives;
            transformedMachine.Sends = machine.Sends;
            transformedMachine.Creates = machine.Creates;
            foreach (var field in machine.Fields) transformedMachine.AddField(field);
            var functionMap = new Dictionary<Function, Function>();
            var requiredMethods = new HashSet<Function>();
            foreach (var method in machine.Methods)
            {
                if (method.IsForeign || (method.Callers.Count() == 0))
                {
                    requiredMethods.Add(method);
                }
            }
            foreach (var method in machine.Methods)
            {
                if (!method.IsForeign)
                {
                    InlineInFunction(method);
                    if (method.Callers.Count() != 0)
                    {
                        requiredMethods.Add(method);
                    }
                }
            }
            foreach (var method in machine.Methods)
            {
                if (!requiredMethods.Contains(method))
                    continue;

                var transformedFunction = method;
                if (!method.IsForeign)
                {
                    transformedFunction = TransformFunction(method, transformedMachine);
                    functionMap.Add(method, transformedFunction);
                    transformedMachine.AddMethod(transformedFunction);
                }
            }
            transformedMachine.StartState = machine.StartState;
            transformedMachine.Observes = machine.Observes;
            transformedMachine.PayloadType = machine.PayloadType;
            transformedMachine.Scope = machine.Scope;
            foreach (var state in machine.States)
            {
                transformedMachine.AddState(TransformState(state, functionMap));
            }

            foreach (var method in machine.Methods)
            {
                if (!requiredMethods.Contains(method))
                    continue;

                foreach (var callee in method.Callees)
                {
                    if (functionMap.ContainsKey(callee))
                        functionMap[method].AddCallee(functionMap[callee]);
                    else functionMap[method].AddCallee(callee);
                }
            }
            return transformedMachine;
        }

        static private State TransformState(State state, IDictionary<Function, Function> functionMap)
        {
            var transformedState = new State(state.SourceLocation, state.Name);
            transformedState.Temperature = state.Temperature;
            transformedState.IsStart = state.IsStart;
            if (state.Entry != null)
                transformedState.Entry = functionMap[state.Entry];
            if (state.Exit != null)
                transformedState.Exit = functionMap[state.Exit];
            transformedState.OwningMachine = state.OwningMachine;

            foreach (var handler in state.AllEventHandlers)
            {
                if (handler.Key.IsNullEvent)
                {
                    throw new NotImplementedException($"Null actions are not supported, found in state {transformedState.Name} of machine {transformedState.OwningMachine.Name}");
                }
                transformedState[handler.Key] = TransformAction(handler.Value, functionMap);
            }

            if (transformedState.Exit != null)
            {
                if (transformedState.Exit.CanReceive == true)
                {
                    throw new NotImplementedException($"Receive in state exit functions are not supported, found in state {transformedState.Name} of machine {transformedState.OwningMachine.Name}");
                }
            }

            return transformedState;
        }

        static private IStateAction TransformAction(IStateAction action, IDictionary<Function, Function> functionMap)
        {
            switch (action)
            {
                case EventDoAction doAction:
                    return new EventDoAction(doAction.SourceLocation, doAction.Trigger, functionMap[doAction.Target]);
                case EventGotoState gotoState:
                    Function transition = null;
                    if(gotoState.TransitionFunction != null) transition = functionMap[gotoState.TransitionFunction];
                    return new EventGotoState(gotoState.SourceLocation, gotoState.Trigger, gotoState.Target, transition);
                default:
                    return action;
            }
        }

        static private void GenerateInline(Function caller, Function callee, IReadOnlyList<IPExpr> argsList, List<IPStmt> body, ParserRuleContext sourceLocation)
        {
            var newVarMap = new Dictionary<Variable,Variable>();
            for (var i = 0; i < callee.Signature.Parameters.Count; i++)
            {
                var expr = argsList[i];
                var newVar = new Variable($"inline_{callNum}_{callee.Signature.Parameters[i].Name}", sourceLocation, VariableRole.Temp);
                newVar.Type = expr.Type;
                body.Add(new AssignStmt(sourceLocation, new VariableAccessExpr(sourceLocation, newVar), expr));
                newVarMap.Add(callee.Signature.Parameters[i], newVar);
                caller.AddLocalVariable(newVar);
            }
            foreach(var local in callee.LocalVariables)
            {
                var newVar = new Variable($"local_{callNum}_{local.Name}", sourceLocation, VariableRole.Temp);
                newVar.Type = local.Type;
                newVarMap.Add(local, newVar);
                caller.AddLocalVariable(newVar);
            }
            foreach(var funStmt in callee.Body.Statements)
                body.Add(ReplaceVars(funStmt, newVarMap));
            callNum++;
        }

        static private List<IPStmt> ReplaceReturn(IReadOnlyList<IPStmt> body, IPExpr location)
        {
            var newBody = new List<IPStmt>();
            foreach (var stmt in body)
            {
                switch (stmt)
                {
                    case ReturnStmt returnStmt:
                        newBody.Add(new AssignStmt(returnStmt.SourceLocation, location, returnStmt.ReturnValue));
                        break;
                    case CompoundStmt compoundStmt:
                        var replace = ReplaceReturn(compoundStmt.Statements, location);
                        foreach (var statement in replace) newBody.Add(statement);
                        break;
                    case IfStmt ifStmt:
                        IPStmt thenStmt = null;
                        if (ifStmt.ThenBranch != null)
                        {
                            var replaceThen = ReplaceReturn(ifStmt.ThenBranch.Statements, location);
                            thenStmt =  new CompoundStmt(ifStmt.ThenBranch.SourceLocation, replaceThen);
                        }
                        IPStmt elseStmt = null;
                        if (ifStmt.ElseBranch != null)
                        {
                            var replaceElse = ReplaceReturn(ifStmt.ElseBranch.Statements, location);
                            elseStmt =  new CompoundStmt(ifStmt.ElseBranch.SourceLocation, replaceElse);
                        }
                        newBody.Add(new IfStmt(ifStmt.SourceLocation, ifStmt.Condition, thenStmt, elseStmt));
                        break;
                    case ReceiveStmt receiveStmt:
                        foreach(var entry in receiveStmt.Cases)
                        {
                            entry.Value.Body = new CompoundStmt(entry.Value.Body.SourceLocation, ReplaceReturn(entry.Value.Body.Statements, location));
                            entry.Value.Signature.ReturnType = null;
                        }
                        newBody.Add(new ReceiveStmt(receiveStmt.SourceLocation, receiveStmt.Cases));
                        break;
                    case WhileStmt whileStmt:
                        var bodyList = new List<IPStmt>();
                        bodyList.Add(whileStmt.Body);
                        var replaceWhile = ReplaceReturn(bodyList, location);
                        newBody.Add(new WhileStmt(whileStmt.SourceLocation, whileStmt.Condition, new CompoundStmt(whileStmt.Body.SourceLocation, replaceWhile)));
                        break;
                    default:
                        newBody.Add(stmt);
                        break;
                }
            }
            return newBody;
        }

        static private void InlineStmt(Function function, IPStmt stmt, List<IPStmt> body)
        {
            switch (stmt)
            {
                case AssignStmt assign:
                    if (assign.Value is FunCallExpr)
                    {
                        var rhsExpr = ((FunCallExpr) assign.Value);
                        if (!rhsExpr.Function.IsForeign)
                        {
                            var inlined = InlineInFunction(rhsExpr.Function);
                            if (inlined)
                                function.RemoveCallee(rhsExpr.Function);
                            var appendToBody = new List<IPStmt>();
                            GenerateInline(function, rhsExpr.Function, rhsExpr.Arguments, appendToBody, assign.SourceLocation);
                            appendToBody = ReplaceReturn(appendToBody, assign.Location);
                            foreach (var statement in appendToBody) body.Add(statement);
                        }
                        else
                        {
                            body.Add(assign);
                        }
                    }
                    else
                    {
                        body.Add(assign);
                    }
                    break;
                case CompoundStmt compound:
                    foreach (var statement in compound.Statements) InlineStmt(function, statement, body);
                    break;
                case FunCallStmt call:
                    if ((!call.Function.IsForeign) & (call.Function.CanReceive == true))
                    {
                        var inlined = InlineInFunction(call.Function);
                        if (inlined)
                            function.RemoveCallee(call.Function);
                        GenerateInline(function, call.Function, call.ArgsList, body, call.SourceLocation);
                    }
                    else
                    {
                        body.Add(call);
                    }
                    break;
                case IfStmt ifStmt:
                    IPStmt thenStmt = null;
                    if (ifStmt.ThenBranch != null)
                    {
                        var thenBranch = new List<IPStmt>();
                        InlineStmt(function, ifStmt.ThenBranch, thenBranch);
                        thenStmt = new CompoundStmt(ifStmt.ThenBranch.SourceLocation, thenBranch);
                    }
                    IPStmt elseStmt = null;
                    if (ifStmt.ElseBranch != null)
                    {
                        var elseBranch = new List<IPStmt>();
                        InlineStmt(function, ifStmt.ElseBranch, elseBranch);
                        elseStmt = new CompoundStmt(ifStmt.ElseBranch.SourceLocation, elseBranch);
                    }
                    body.Add(new IfStmt(ifStmt.SourceLocation, ifStmt.Condition, thenStmt, elseStmt));
                    break;
                case WhileStmt whileStmt:
                    var bodyList = new List<IPStmt>();
                    InlineStmt(function, whileStmt.Body, bodyList);
                    body.Add(new WhileStmt(whileStmt.SourceLocation, whileStmt.Condition, new CompoundStmt(whileStmt.Body.SourceLocation, bodyList)));
                    break;
                default:
                    body.Add(stmt);
                    break;
            }
        }

        static private bool InlineInFunction(Function function)
        {
            if (!function.Callees.Contains(function) && (function.CanReceive == true)) {
                var body = new List<IPStmt>();
                foreach (var stmt in function.Body.Statements)
                {
                    InlineStmt(function, stmt, body);
                }
                function.Body = new CompoundStmt(function.Body.SourceLocation, body);
                return true;
            }
            else
            {
                return false;
            }
        }

         static private IPStmt ReplaceVars(IPStmt stmt, Dictionary<Variable,Variable> varMap)
         {
             if (stmt == null) return null;
             switch(stmt)
             {
                 case AddStmt addStmt:
                     return new AddStmt(addStmt.SourceLocation, ReplaceVars(addStmt.Variable, varMap), ReplaceVars(addStmt.Value, varMap));
                 case AnnounceStmt announceStmt:
                     return new AnnounceStmt(announceStmt.SourceLocation, ReplaceVars(announceStmt.Event, varMap), ReplaceVars(announceStmt.Payload, varMap));
                 case AssertStmt assertStmt:
                     return new AssertStmt(assertStmt.SourceLocation, ReplaceVars(assertStmt.Assertion, varMap), ReplaceVars(assertStmt.Message, varMap));
                 case AssignStmt assignStmt:
                     return new AssignStmt(assignStmt.SourceLocation, ReplaceVars(assignStmt.Location, varMap), ReplaceVars(assignStmt.Value, varMap));
                 case CompoundStmt compoundStmt:
                     var statements = new List<IPStmt>();
                     foreach (var inner in compoundStmt.Statements) statements.Add(ReplaceVars(inner, varMap));
                     return new CompoundStmt(compoundStmt.SourceLocation, statements);
                 case CtorStmt ctorStmt:
                     var arguments = new List<IPExpr>();
                     foreach (var arg in ctorStmt.Arguments) arguments.Add(ReplaceVars(arg, varMap));
                     return new CtorStmt(ctorStmt.SourceLocation, ctorStmt.Interface, arguments);
                 case FunCallStmt funCallStmt:
                     var newArgs = new List<IPExpr>();
                     foreach (var arg in funCallStmt.ArgsList) newArgs.Add(ReplaceVars(arg, varMap));
                     return new FunCallStmt(funCallStmt.SourceLocation, funCallStmt.Function, new List<IPExpr>(newArgs));
                 case GotoStmt gotoStmt:
                     return new GotoStmt(gotoStmt.SourceLocation, gotoStmt.State, ReplaceVars(gotoStmt.Payload, varMap));
                 case IfStmt ifStmt:
                     return new IfStmt(ifStmt.SourceLocation, ReplaceVars(ifStmt.Condition, varMap), ReplaceVars(ifStmt.ThenBranch, varMap), ReplaceVars(ifStmt.ElseBranch, varMap));
                 case InsertStmt insertStmt:
                     return new InsertStmt(insertStmt.SourceLocation, ReplaceVars(insertStmt.Variable, varMap), ReplaceVars(insertStmt.Index, varMap), ReplaceVars(insertStmt.Value, varMap));
                 case MoveAssignStmt moveAssignStmt:
                     var fromVar = moveAssignStmt.FromVariable;
                     if (varMap.ContainsKey(moveAssignStmt.FromVariable)) fromVar = varMap[moveAssignStmt.FromVariable];
                     return new MoveAssignStmt(moveAssignStmt.SourceLocation, ReplaceVars(moveAssignStmt.ToLocation, varMap), fromVar);
                 case PrintStmt printStmt:
                     return new PrintStmt(printStmt.SourceLocation, ReplaceVars(printStmt.Message, varMap));
                 case RaiseStmt raiseStmt:
                     var payload = new List<IPExpr>();
                     foreach(var p in raiseStmt.Payload) payload.Add(ReplaceVars(p, varMap));
                     return new RaiseStmt(raiseStmt.SourceLocation, ReplaceVars(raiseStmt.Event, varMap), payload);
                 case ReceiveStmt receiveStmt:
                     var cases = new Dictionary<Event, Function>();
                     foreach(var entry in receiveStmt.Cases)
                     {
                         var replacement = new Function(entry.Value.Name, entry.Value.SourceLocation);
                         replacement.Owner = entry.Value.Owner;
                         replacement.ParentFunction = entry.Value.ParentFunction;
                         replacement.CanReceive = entry.Value.CanReceive;
                         replacement.Role = entry.Value.Role;
                         replacement.Scope = entry.Value.Scope;
                         foreach (var local in entry.Value.LocalVariables) replacement.AddLocalVariable(local);
                         foreach (var i in entry.Value.CreatesInterfaces) replacement.AddCreatesInterface(i);
                         foreach (var param in entry.Value.Signature.Parameters) replacement.Signature.Parameters.Add(param);
                         replacement.Signature.ReturnType = entry.Value.Signature.ReturnType;
                         foreach (var callee in entry.Value.Callees) replacement.AddCallee(callee);
                         replacement.Body = (CompoundStmt) ReplaceVars(entry.Value.Body, varMap);
                         cases.Add(entry.Key, replacement);
                     }
                     return new ReceiveStmt(receiveStmt.SourceLocation, cases);
                 case RemoveStmt removeStmt:
                     return new RemoveStmt(removeStmt.SourceLocation, ReplaceVars(removeStmt.Variable, varMap), ReplaceVars(removeStmt.Value, varMap));
                 case ReturnStmt returnStmt:
                     return new ReturnStmt(returnStmt.SourceLocation, ReplaceVars(returnStmt.ReturnValue, varMap));
                 case SendStmt sendStmt:
                     var sendArgs = new List<IPExpr>();
                     foreach (var arg in sendStmt.Arguments) sendArgs.Add(ReplaceVars(arg, varMap));
                     return new SendStmt(sendStmt.SourceLocation, ReplaceVars(sendStmt.MachineExpr, varMap), ReplaceVars(sendStmt.Evt, varMap), sendArgs, sendStmt.DelayDistribution);
                 case WhileStmt whileStmt:
                     return new WhileStmt(whileStmt.SourceLocation, ReplaceVars(whileStmt.Condition, varMap), ReplaceVars(whileStmt.Body, varMap));
                 default:
                     return stmt;
             }
         }

        static private IPExpr ReplaceVars(IPExpr expr, Dictionary<Variable,Variable> varMap)
        {
            switch(expr)
            {
                case BinOpExpr binOpExpr:
                    return new BinOpExpr(binOpExpr.SourceLocation, binOpExpr.Operation, ReplaceVars(binOpExpr.Lhs, varMap), ReplaceVars(binOpExpr.Rhs, varMap));
                case CastExpr castExpr:
                    return new CastExpr(castExpr.SourceLocation, ReplaceVars(castExpr.SubExpr, varMap), castExpr.Type);
                case ChooseExpr chooseExpr:
                    return new ChooseExpr(chooseExpr.SourceLocation, ReplaceVars(chooseExpr.SubExpr, varMap), chooseExpr.Type);
                case CloneExpr cloneExpr:
                    return ReplaceVars(cloneExpr.Term, varMap);
                case CoerceExpr coerceExpr:
                    return new CoerceExpr(coerceExpr.SourceLocation, ReplaceVars(coerceExpr.SubExpr, varMap), coerceExpr.NewType);
                case ContainsExpr containsExpr:
                    return new ContainsExpr(containsExpr.SourceLocation, ReplaceVars(containsExpr.Item, varMap), ReplaceVars(containsExpr.Collection, varMap));
                case CtorExpr ctorExpr:
                    var newArguments = new List<IPExpr>();
                    foreach (var arg in ctorExpr.Arguments) newArguments.Add(ReplaceVars(arg, varMap));
                    return new CtorExpr(ctorExpr.SourceLocation, ctorExpr.Interface, new List<IPExpr>(newArguments));
                case FunCallExpr funCallExpr:
                    var newArgs = new List<IPExpr>();
                    foreach (var arg in funCallExpr.Arguments) newArgs.Add(ReplaceVars(arg, varMap));
                    return new FunCallExpr(funCallExpr.SourceLocation, funCallExpr.Function, new List<IPExpr>(newArgs));
                case KeysExpr keysExpr:
                    return new KeysExpr(keysExpr.SourceLocation, ReplaceVars(keysExpr.Expr, varMap), keysExpr.Type);
                case MapAccessExpr mapAccessExpr:
                    return new MapAccessExpr(mapAccessExpr.SourceLocation, ReplaceVars(mapAccessExpr.MapExpr, varMap), ReplaceVars(mapAccessExpr.IndexExpr, varMap), mapAccessExpr.Type);
                case NamedTupleAccessExpr namedTupleAccessExpr:
                    return new NamedTupleAccessExpr(namedTupleAccessExpr.SourceLocation, ReplaceVars(namedTupleAccessExpr.SubExpr, varMap), namedTupleAccessExpr.Entry);
                case NamedTupleExpr namedTupleExpr:
                    var newFields = new List<IPExpr>();
                    foreach (var field in namedTupleExpr.TupleFields) newFields.Add(ReplaceVars(field, varMap));
                    return new NamedTupleExpr(namedTupleExpr.SourceLocation, new List<IPExpr>(newFields), namedTupleExpr.Type);
                case SeqAccessExpr seqAccessExpr:
                    return new SeqAccessExpr(seqAccessExpr.SourceLocation, ReplaceVars(seqAccessExpr.SeqExpr, varMap), ReplaceVars(seqAccessExpr.IndexExpr, varMap), seqAccessExpr.Type);
                case SetAccessExpr setAccessExpr:
                    return new SetAccessExpr(setAccessExpr.SourceLocation, ReplaceVars(setAccessExpr.SetExpr, varMap), ReplaceVars(setAccessExpr.IndexExpr, varMap), setAccessExpr.Type);
                case SizeofExpr sizeofExpr:
                    return new SizeofExpr(sizeofExpr.SourceLocation, ReplaceVars(sizeofExpr.Expr, varMap));
                case StringExpr stringExpr:
                    var newListArgs = new List<IPExpr>();
                    foreach (var arg in stringExpr.Args) newListArgs.Add(ReplaceVars(arg, varMap));
                    return new StringExpr(stringExpr.SourceLocation, stringExpr.BaseString, newListArgs);
                case TupleAccessExpr tupleAccessExpr:
                    return new TupleAccessExpr(tupleAccessExpr.SourceLocation, ReplaceVars(tupleAccessExpr.SubExpr, varMap), tupleAccessExpr.FieldNo, tupleAccessExpr.Type);
                case UnaryOpExpr unaryOpExpr:
                    return new UnaryOpExpr(unaryOpExpr.SourceLocation, unaryOpExpr.Operation, ReplaceVars(unaryOpExpr.SubExpr, varMap));
                case UnnamedTupleExpr unnamedTupleExpr:
                    var newUnnamedFields = new List<IPExpr>();
                    foreach (var field in unnamedTupleExpr.TupleFields) newUnnamedFields.Add(ReplaceVars(field, varMap));
                    return new UnnamedTupleExpr(unnamedTupleExpr.SourceLocation, new List<IPExpr>(newUnnamedFields));
                case ValuesExpr valuesExpr:
                    return new ValuesExpr(valuesExpr.SourceLocation, ReplaceVars(valuesExpr.Expr, varMap), valuesExpr.Type);
                case VariableAccessExpr variableAccessExpr:
                    if (varMap.ContainsKey(variableAccessExpr.Variable))
                        return new VariableAccessExpr(variableAccessExpr.SourceLocation, varMap[variableAccessExpr.Variable]);
                    else return variableAccessExpr;
                default:
                    return expr;
            }
        }

        static private Function TransformFunction(Function function, Machine machine)
        {
            if (function.CanReceive != true) {
                return function;
            }
            if (machine == null)
                throw new NotImplementedException($"Async functions {function.Name} are not supported");
            var transformedFunction = new Function(function.Name, function.SourceLocation);
            transformedFunction.Owner = function.Owner;
            transformedFunction.ParentFunction = function.ParentFunction;
            foreach (var local in function.LocalVariables) transformedFunction.AddLocalVariable(local);
            foreach (var i in function.CreatesInterfaces) transformedFunction.AddCreatesInterface(i);
            transformedFunction.Role = function.Role;
            transformedFunction.Body = (CompoundStmt) HandleReceives(function.Body, function, machine);
            transformedFunction.Scope = function.Scope;
            transformedFunction.CanChangeState = function.CanChangeState;
            transformedFunction.CanRaiseEvent = function.CanRaiseEvent;
            transformedFunction.CanReceive = function.CanReceive;
            transformedFunction.IsNondeterministic = function.IsNondeterministic;
            foreach (var param in function.Signature.Parameters) transformedFunction.Signature.Parameters.Add(param);
            transformedFunction.Signature.ReturnType = function.Signature.ReturnType;
            return transformedFunction;
        }

        static private IPStmt ReplaceBreaks(IPStmt stmt, List<IPStmt> afterStmts)
        {
            if (stmt == null) return null;
            var statements = new List<IPStmt>();
            switch(stmt)
            {
                case CompoundStmt compoundStmt:
                    foreach (var inner in compoundStmt.Statements)
                    {
                        statements.Add(ReplaceBreaks(inner, afterStmts));
                    }
                    return new CompoundStmt(compoundStmt.SourceLocation, statements);
                case IfStmt ifStmt:
                    return new IfStmt(ifStmt.SourceLocation, ifStmt.Condition, ReplaceBreaks(ifStmt.ThenBranch, afterStmts), ReplaceBreaks(ifStmt.ElseBranch, afterStmts));
                case ReceiveStmt receiveStmt:
                    var cases = new Dictionary<Event, Function>();
                    foreach(var entry in receiveStmt.Cases)
                    {
                        var replacement = new Function(entry.Value.Name, entry.Value.SourceLocation);
                        replacement.Owner = entry.Value.Owner;
                        replacement.ParentFunction = entry.Value.ParentFunction;
                        replacement.CanReceive = entry.Value.CanReceive;
                        replacement.Role = entry.Value.Role;
                        replacement.Scope = entry.Value.Scope;
                        foreach (var local in entry.Value.LocalVariables) replacement.AddLocalVariable(local);
                        foreach (var i in entry.Value.CreatesInterfaces) replacement.AddCreatesInterface(i);
                        foreach (var param in entry.Value.Signature.Parameters) replacement.Signature.Parameters.Add(param);
                        replacement.Signature.ReturnType = entry.Value.Signature.ReturnType;
                        foreach (var callee in entry.Value.Callees) replacement.AddCallee(callee);
                        replacement.Body = (CompoundStmt) ReplaceBreaks(entry.Value.Body, afterStmts);
                        cases.Add(entry.Key, replacement);
                    }
                    return new ReceiveStmt(receiveStmt.SourceLocation, cases);
                case BreakStmt _:
                    IPStmt returnStmt = new ReturnStmt(stmt.SourceLocation, null);
                    foreach (var inner in afterStmts)
                    {
                        statements.Add(inner);
                    }
                    if (statements.Count() == 0)
                    {
                        return returnStmt;
                    }
                    else
                    {
                        statements.Add(returnStmt);
                        return new CompoundStmt(returnStmt.SourceLocation, statements);
                    }
                default:
                    return stmt;
            }
        }

        static private IPStmt HandleReceives(IPStmt statement, Function function, Machine machine)
        {
            switch (statement)
            {
                case CompoundStmt compound:
                    var enumerator = compound.Statements.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        var first = enumerator.Current;
                        var afterStmts = new List<IPStmt>();
                        while (enumerator.MoveNext())
                        {
                            afterStmts.Add(enumerator.Current);
                        }
                        CompoundStmt after = null;
                        if (afterStmts.Count > 0)
                        {
                            after = new CompoundStmt(afterStmts[0].SourceLocation, afterStmts);
                        }
                        var result = new List<IPStmt>();
                        switch (first)
                        {
                            case CompoundStmt nestedCompound:
                                var compoundStmts = new List<IPStmt>(nestedCompound.Statements);
                                foreach (var stmt in afterStmts)
                                {
                                    compoundStmts.Add(stmt);
                                }
                                result.Add(HandleReceives(new CompoundStmt(nestedCompound.SourceLocation, compoundStmts), function, machine));
                                break;
                            case IfStmt cond:
                                var thenStmts = new List<IPStmt>();
                                var elseStmts = new List<IPStmt>();
                                if (cond.ThenBranch != null)
                                    thenStmts = new List<IPStmt>(cond.ThenBranch.Statements);
                                if (cond.ElseBranch != null)
                                    elseStmts = new List<IPStmt>(cond.ElseBranch.Statements);
                                IPStmt thenBody = new CompoundStmt(cond.SourceLocation, thenStmts);
                                IPStmt elseBody = new CompoundStmt(cond.SourceLocation, elseStmts);
                                thenBody = HandleReceives(thenBody, function, machine);
                                elseBody = HandleReceives(elseBody, function, machine);
                                result.Add(new IfStmt(cond.SourceLocation, cond.Condition, thenBody, elseBody));
                                if (after != null)
                                {
                                    after = (CompoundStmt) HandleReceives(after, function, machine);
                                    result.Add(after);
                                }
                                break;
                            case ReceiveStmt recv:
                                IDictionary<Event, Function> cases = new Dictionary<Event, Function>();
                                var canReceiveInCase = false;
                                foreach (var c in recv.Cases)
                                {
                                    if (c.Value.CanReceive == true)
                                    {
                                        canReceiveInCase = true;
                                        if (c.Value.LocalVariables.Count() != 0)
                                        {
                                            var caseVarMap = new Dictionary<Variable,Variable>();
                                            foreach (var local in c.Value.LocalVariables)
                                            {
                                                var caseVar = new Variable($"{c.Key.Name}_{local.Name}", local.SourceLocation, local.Role);
                                                caseVar.Type = local.Type;
                                                caseVarMap.Add(local, caseVar);
                                            }
                                            foreach(var entry in caseVarMap)
                                            {
                                                c.Value.RemoveLocalVariable(entry.Key);
                                                c.Value.AddLocalVariable(entry.Value);
                                            }
                                            if (c.Value.Body != null)
                                            {
                                                var newCaseBody = new List<IPStmt>();
                                                newCaseBody.Add(ReplaceVars(c.Value.Body, caseVarMap));
                                                c.Value.Body = new CompoundStmt(c.Value.Body.SourceLocation, newCaseBody);
                                            }
                                        }
                                    }
                                }
                                foreach (var c in recv.Cases)
                                {
                                    c.Value.AddLocalVariables(function.Signature.Parameters);
                                    c.Value.AddLocalVariables(function.LocalVariables);

                                    if (c.Value.Signature.Parameters.Count() != 0)
                                    {
                                        var caseVarMap = new Dictionary<Variable,Variable>();
                                        foreach (var local in c.Value.Signature.Parameters)
                                        {
                                            var caseVar = new Variable($"{c.Key.Name}_{local.Name}", local.SourceLocation, local.Role);
                                            caseVar.Type = local.Type;
                                            caseVarMap.Add(local, caseVar);
                                        }
                                        foreach(var entry in caseVarMap)
                                        {
                                            c.Value.Signature.Parameters.Remove(entry.Key);
                                            c.Value.Signature.Parameters.Add(entry.Value);
                                        }
                                        if (c.Value.Body != null)
                                        {
                                            var newCaseBody = new List<IPStmt>();
                                            newCaseBody.Add(ReplaceVars(c.Value.Body, caseVarMap));
                                            c.Value.Body = new CompoundStmt(c.Value.Body.SourceLocation, newCaseBody);
                                        }
                                    }

                                    if (canReceiveInCase == true && after != null)
                                    {
                                        var caseStmts = new List<IPStmt>();
                                        var caseBody = c.Value.Body;
                                        if (caseBody != null)
                                        {
                                            foreach (var stmt in caseBody.Statements)
                                            {
                                                caseStmts.Add(stmt);
                                            }
                                        }
                                        if (after != null)
                                        {
                                            caseStmts.Add(after);
                                        }
                                        c.Value.Body = new CompoundStmt(c.Value.Body.SourceLocation, caseStmts);
                                    }
                                    cases.Add(c.Key, TransformFunction(c.Value, machine));
                                }
                                if (canReceiveInCase)
                                {
                                    after = null;
                                }
                                if (after != null)
                                {
                                    after = (CompoundStmt) HandleReceives(after, function, machine);
                                }
                                var continuation = GetContinuation(function, cases, after, recv.SourceLocation);
                                if (machine != null) machine.AddMethod(continuation);
                                foreach (var v in continuation.StoreParameters)
                                {
                                    machine.AddField(v);
                                }
                                foreach (var store in continuation.StoreStmts)
                                {
                                    result.Add(store);
                                }
                                var split = new ReceiveSplitStmt(compound.SourceLocation, continuation);
                                result.Add(split);
                                break;
                            case WhileStmt loop:
                                //  throw new NotImplementedException($"Receive in a while statement is not yet supported, found in {machine.Name}");
                                // turn the while statement into a recursive function
                                var whileName = $"while_{whileNumber}";
                                whileNumber++;
                                var rec = new WhileFunction(whileName, loop.SourceLocation);
                                rec.Owner = function.Owner;
                                rec.ParentFunction = function;
                                foreach (var param in function.Signature.Parameters) rec.AddParameter(param);
                                var newVarMap = new Dictionary<Variable,Variable>();
                                foreach (var local in function.LocalVariables)
                                {
                                    var machineVar = new Variable($"{whileName}_{local.Name}", local.SourceLocation, local.Role);
                                    machineVar.Type = local.Type;
                                    machine.AddField(machineVar);
                                    newVarMap.Add(local, machineVar);
                                }
                                foreach (var i in function.CreatesInterfaces) rec.AddCreatesInterface(i);
                                rec.CanChangeState = function.CanChangeState;
                                rec.CanRaiseEvent = function.CanRaiseEvent;
                                rec.CanReceive = function.CanReceive;
                                rec.IsNondeterministic = function.IsNondeterministic;
                                // make while loop body
                                var loopBody = new List<IPStmt>();
                                var bodyEnumerator = loop.Body.Statements.GetEnumerator();
                                while (bodyEnumerator.MoveNext())
                                {
                                    var stmt = bodyEnumerator.Current;
                                    var replaceBreak = ReplaceBreaks(stmt, afterStmts);
                                    if (replaceBreak != null) {
                                        loopBody.Add(ReplaceVars(replaceBreak, newVarMap));
                                    }
                                }
                                var recArgs = new List<VariableAccessExpr>();
                                foreach (var param in rec.Signature.Parameters)
                                {
                                    recArgs.Add(new VariableAccessExpr(rec.SourceLocation, param));
                                }
                                // call the function
                                var recCall = new FunCallStmt(loop.SourceLocation, rec, recArgs);
                                loopBody.Add(recCall);
                                rec.AddCallee(rec);
                                loopBody = new List<IPStmt>(((CompoundStmt) HandleReceives(new CompoundStmt(rec.SourceLocation, loopBody), rec, machine)).Statements);
                                rec.Body = new CompoundStmt(rec.SourceLocation, loopBody);
                                if (machine != null) machine.AddMethod(rec);
                                // assign local variables
                                foreach (var local in function.LocalVariables)
                                {
                                    result.Add(new AssignStmt(local.SourceLocation, new VariableAccessExpr(local.SourceLocation, newVarMap[local]), new VariableAccessExpr(local.SourceLocation, local)));
                                }
                                // replace the while statement with a function call
                                result.Add(recCall);
                                function.AddCallee(rec);
                                break;
                            default:
                                if (after == null) return compound;
                                result.Add(first);
                                after = (CompoundStmt) HandleReceives(after, function, machine);
                                result.Add(after);
                                break;
                        }
                        return new CompoundStmt(compound.SourceLocation, result);
                    }
                    return compound;
/*
                 case FunCallStmt call:
                     Function callee = TransformFunction(callee, machine);
                     FunCallStmt newCall = new FunCallStmt(SourceLocation, callee, call.ArgsList);
                     function.AddCallee(callee);
                     return newCall;
*/
                default:
                    return statement;
            }
        }

        static private Continuation GetContinuation(Function function, IDictionary<Event, Function> cases, IPStmt after, ParserRuleContext location)
        {
            var continuationName = $"continuation_{continuationNumber}";
            continuationNumber++;
            var continuation = new Continuation(continuationName, new Dictionary<Event, Function>(cases), after, location);
            continuation.ParentFunction = function;
            function.AddCallee(continuation);
            function.Role = FunctionRole.Method;
            foreach (var v in function.Signature.Parameters) {
                var local = new Variable(v.Name, v.SourceLocation, v.Role);
                var store = new Variable($"{continuationName}_{v.Name}", v.SourceLocation, v.Role);
                local.Type = v.Type;
                store.Type = v.Type;
                continuation.AddParameter(local, store);
            }
            foreach (var v in function.LocalVariables)
            {
                var local = new Variable(v.Name, v.SourceLocation, v.Role);
                var store = new Variable($"{continuationName}_{v.Name}", v.SourceLocation, v.Role);
                local.Type = v.Type;
                store.Type = v.Type;
                continuation.AddParameter(local, store);
            }
            continuation.CanChangeState = function.CanChangeState;
            continuation.CanRaiseEvent = function.CanRaiseEvent;
            continuation.Signature.ReturnType = function.Signature.ReturnType;
            return continuation;
        }
    }
}