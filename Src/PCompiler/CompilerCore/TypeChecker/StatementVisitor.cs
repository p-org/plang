using System;
using System.Collections.Generic;
using System.Linq;
using Antlr4.Runtime;
using Plang.Compiler.TypeChecker.AST;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.TypeChecker.AST.Expressions;
using Plang.Compiler.TypeChecker.AST.Statements;
using Plang.Compiler.TypeChecker.Types;

namespace Plang.Compiler.TypeChecker
{
    public class StatementVisitor : PParserBaseVisitor<IPStmt>
    {
        private readonly ExprVisitor exprVisitor;
        private readonly ICompilerConfiguration config;
        private readonly ITranslationErrorHandler handler;
        private readonly Machine machine;
        private readonly Function method;
        private readonly Scope table;

        public StatementVisitor(ICompilerConfiguration config, Machine machine, Function method)
        {
            this.config = config;
            this.handler = this.config.Handler;
            this.machine = machine;
            this.method = method;
            table = method.Scope;
            exprVisitor = new ExprVisitor(method, config.Handler);
        }

        public override IPStmt VisitFunctionBody(PParser.FunctionBodyContext context)
        {
            var statements = context.statement().Select(Visit).ToList();
            return new CompoundStmt(context, statements);
        }

        public override IPStmt VisitCompoundStmt(PParser.CompoundStmtContext context)
        {
            var statements = context.statement().Select(Visit).ToList();
            return new CompoundStmt(context, statements);
        }

        public override IPStmt VisitAssertStmt(PParser.AssertStmtContext context)
        {
            var assertion = exprVisitor.Visit(context.assertion);
            if (!PrimitiveType.Bool.IsSameTypeAs(assertion.Type))
            {
                throw handler.TypeMismatch(context.assertion, assertion.Type, PrimitiveType.Bool);
            }
            IPExpr assertMessage = new StringExpr(context, @$"{config.LocationResolver.GetLocation(context).ToString().Replace(@"\", @"\\")}",new List<IPExpr>());
            if (context.message != null)
            {
                var message = exprVisitor.Visit(context.message);
                if (!message.Type.IsSameTypeAs(PrimitiveType.String))
                {
                    throw handler.TypeMismatch(context.message, message.Type, PrimitiveType.String);
                }

                assertMessage = new StringExpr(message.SourceLocation, "{0} {1}",new List<IPExpr>() {assertMessage,
                    message});
            }

            return new AssertStmt(context, assertion, assertMessage);
        }

        public override IPStmt VisitPrintStmt(PParser.PrintStmtContext context)
        {
            var message = exprVisitor.Visit(context.message);
            if (!message.Type.IsSameTypeAs(PrimitiveType.String))
            {
                throw handler.TypeMismatch(context.message, message.Type, PrimitiveType.String);
            }
            return new PrintStmt(context, message);
        }

        public override IPStmt VisitReturnStmt(PParser.ReturnStmtContext context)
        {
            var returnValue = context.expr() == null ? null : exprVisitor.Visit(context.expr());
            var returnType = returnValue?.Type ?? PrimitiveType.Null;
            if (!method.Signature.ReturnType.IsAssignableFrom(returnType))
            {
                throw handler.TypeMismatch(context, returnType, method.Signature.ReturnType);
            }

            return new ReturnStmt(context, returnValue);
        }

        public override IPStmt VisitBreakStmt(PParser.BreakStmtContext context)
        {
            return new BreakStmt(context);
        }

        public override IPStmt VisitContinueStmt(PParser.ContinueStmtContext context)
        {
            return new ContinueStmt(context);
        }

        public override IPStmt VisitAssignStmt(PParser.AssignStmtContext context)
        {
            var variable = exprVisitor.Visit(context.lvalue());
            var value = exprVisitor.Visit(context.rvalue());

            // If this is a value assignment, we just need subtyping
            if (!variable.Type.IsAssignableFrom(value.Type))
            {
                throw handler.TypeMismatch(context.rvalue(), value.Type, variable.Type);
            }

            return new AssignStmt(context, variable, value);
        }

        public override IPStmt VisitAddStmt(PParser.AddStmtContext context)
        {
            var variable = exprVisitor.Visit(context.lvalue());
            var value = exprVisitor.Visit(context.rvalue());


            // Check subtyping
            var valueType = value.Type;

            PLanguageType expectedValueType;

            switch (variable.Type.Canonicalize())
            {
                case SetType setType:
                    expectedValueType = setType.ElementType;
                    break;

                default:
                    throw handler.TypeMismatch(variable, TypeKind.Set);
            }

            if (!expectedValueType.IsAssignableFrom(valueType))
                throw handler.TypeMismatch(context.rvalue(), valueType, expectedValueType);

            return new AddStmt(context, variable, value);
        }

        public override IPStmt VisitInsertStmt(PParser.InsertStmtContext context)
        {
            var variable = exprVisitor.Visit(context.lvalue());
            var index = exprVisitor.Visit(context.expr());
            var value = exprVisitor.Visit(context.rvalue());

            // Check subtyping
            var keyType = index.Type;
            var valueType = value.Type;

            PLanguageType expectedKeyType;
            PLanguageType expectedValueType;

            switch (variable.Type.Canonicalize())
            {
                case SequenceType sequenceType:
                    expectedKeyType = PrimitiveType.Int;
                    expectedValueType = sequenceType.ElementType;
                    break;

                case MapType mapType:
                    expectedKeyType = mapType.KeyType;
                    expectedValueType = mapType.ValueType;
                    break;

                default:
                    throw handler.TypeMismatch(variable, TypeKind.Sequence, TypeKind.Map);
            }

            if (!expectedKeyType.IsAssignableFrom(keyType))
            {
                throw handler.TypeMismatch(context.rvalue(), keyType, expectedKeyType);
            }

            if (!expectedValueType.IsAssignableFrom(valueType))
            {
                throw handler.TypeMismatch(context.rvalue(), valueType, expectedValueType);
            }

            return new InsertStmt(context, variable, index, value);
        }

        public override IPStmt VisitRemoveStmt(PParser.RemoveStmtContext context)
        {
            var variable = exprVisitor.Visit(context.lvalue());
            var value = exprVisitor.Visit(context.expr());

            if (PLanguageType.TypeIsOfKind(variable.Type, TypeKind.Sequence))
            {
                if (!PrimitiveType.Int.IsAssignableFrom(value.Type))
                {
                    throw handler.TypeMismatch(context.expr(), value.Type, PrimitiveType.Int);
                }
            }
            else if (PLanguageType.TypeIsOfKind(variable.Type, TypeKind.Map))
            {
                var map = (MapType)variable.Type.Canonicalize();
                if (!map.KeyType.IsAssignableFrom(value.Type))
                {
                    throw handler.TypeMismatch(context.expr(), value.Type, map.KeyType);
                }
            }
            else if (PLanguageType.TypeIsOfKind(variable.Type, TypeKind.Set))
            {
                var set = (SetType)variable.Type.Canonicalize();
                if (!set.ElementType.IsAssignableFrom(value.Type))
                    throw handler.TypeMismatch(context.expr(), value.Type, set.ElementType);
            }
            else
            {
                throw handler.TypeMismatch(variable, TypeKind.Sequence, TypeKind.Map);
            }

            return new RemoveStmt(context, variable, value);
        }

        public override IPStmt VisitWhileStmt(PParser.WhileStmtContext context)
        {
            var condition = exprVisitor.Visit(context.expr());
            if (!Equals(condition.Type, PrimitiveType.Bool))
            {
                throw handler.TypeMismatch(context.expr(), condition.Type, PrimitiveType.Bool);
            }

            var body = Visit(context.statement());
            return new WhileStmt(context, condition, body);
        }

        public override IPStmt VisitForeachStmt(PParser.ForeachStmtContext context)
        {
            var varName = context.item.GetText();
            if (!table.Lookup(varName, out Variable var))
            {
                throw handler.MissingDeclaration(context.item, "foreach iterator variable", varName);
            }
            var collection = exprVisitor.Visit(context.collection);

            // make sure that foreach is applied to either sequence or set type

            // Check subtyping
            var itemType = var.Type;

            PLanguageType expectedItemType;

            switch (collection.Type.Canonicalize())
            {
                case SetType setType:
                    expectedItemType = setType.ElementType;
                    break;
                case SequenceType seqType:
                    expectedItemType = seqType.ElementType;
                    break;
                default:
                    throw handler.TypeMismatch(collection, TypeKind.Set, TypeKind.Sequence);
            }

            if (!expectedItemType.IsSameTypeAs(itemType)
                || !expectedItemType.IsAssignableFrom(itemType))
                throw handler.TypeMismatch(context.item, itemType, expectedItemType);

            var body = Visit(context.statement());
            return new ForeachStmt(context, var, collection, body);
        }

        public override IPStmt VisitIfStmt(PParser.IfStmtContext context)
        {
            var condition = exprVisitor.Visit(context.expr());
            if (!Equals(condition.Type, PrimitiveType.Bool))
            {
                throw handler.TypeMismatch(context.expr(), condition.Type, PrimitiveType.Bool);
            }

            var thenBody = Visit(context.thenBranch);
            var elseBody = context.elseBranch == null ? new NoStmt(context) : Visit(context.elseBranch);
            return new IfStmt(context, condition, thenBody, elseBody);
        }

        public override IPStmt VisitCtorStmt(PParser.CtorStmtContext context)
        {
            var interfaceName = context.iden().GetText();
            if (!table.Lookup(interfaceName, out Interface targetInterface))
            {
                throw handler.MissingDeclaration(context.iden(), "interface", interfaceName);
            }

            var args = TypeCheckingUtils.VisitRvalueList(context.rvalueList(), exprVisitor).ToList();
            TypeCheckingUtils.ValidatePayloadTypes(handler, context, targetInterface.PayloadType, args);
            method.CanCreate = true;
            return new CtorStmt(context, targetInterface, args);
        }

        public override IPStmt VisitFunCallStmt(PParser.FunCallStmtContext context)
        {
            var funName = context.fun.GetText();
            var argsList = TypeCheckingUtils.VisitRvalueList(context.rvalueList(), exprVisitor).ToList();
            if (!table.Lookup(funName, out Function fun))
            {
                throw handler.MissingDeclaration(context.fun, "function or function prototype", funName);
            }

            if (fun.Signature.Parameters.Count != argsList.Count)
            {
                throw handler.IncorrectArgumentCount((ParserRuleContext)context.rvalueList() ?? context,
                    argsList.Count,
                    fun.Signature.Parameters.Count);
            }

            foreach (var pair in fun.Signature.Parameters.Zip(argsList, Tuple.Create))
            {
                TypeCheckingUtils.CheckArgument(handler, context, pair.Item1.Type, pair.Item2);
            }

            method.AddCallee(fun);
            return new FunCallStmt(context, fun, argsList);
        }

        public override IPStmt VisitRaiseStmt(PParser.RaiseStmtContext context)
        {
            if (!method.Signature.ReturnType.IsSameTypeAs(PrimitiveType.Null))
            {
                throw handler.RaiseEventInNonVoidFunction(context);
            }

            var evtExpr = exprVisitor.Visit(context.expr());
            if (IsDefinitelyNullEvent(evtExpr))
            {
                throw handler.EmittedNullEvent(evtExpr);
            }

            if (!PrimitiveType.Event.IsAssignableFrom(evtExpr.Type))
            {
                throw handler.TypeMismatch(context.expr(), evtExpr.Type, PrimitiveType.Event);
            }

            method.CanRaiseEvent = true;

            var args = TypeCheckingUtils.VisitRvalueList(context.rvalueList(), exprVisitor).ToArray();
            if (evtExpr is EventRefExpr eventRef)
            {
                TypeCheckingUtils.ValidatePayloadTypes(handler, context, eventRef.Value.PayloadType, args);
            }

            return new RaiseStmt(context, evtExpr, args);
        }

        public override IPStmt VisitSendStmt(PParser.SendStmtContext context)
        {
            if (machine?.IsSpec == true)
            {
                throw handler.IllegalMonitorOperation(context, context.SEND().Symbol, machine);
            }

            var machineExpr = exprVisitor.Visit(context.machine);
            if (!PrimitiveType.Machine.IsAssignableFrom(machineExpr.Type))
            {
                throw handler.TypeMismatch(context.machine, machineExpr.Type, PrimitiveType.Machine);
            }

            var evtExpr = exprVisitor.Visit(context.@event);
            if (IsDefinitelyNullEvent(evtExpr))
            {
                throw handler.EmittedNullEvent(evtExpr);
            }

            if (!PrimitiveType.Event.IsAssignableFrom(evtExpr.Type))
            {
                throw handler.TypeMismatch(context.@event, evtExpr.Type, PrimitiveType.Event);
            }

            var args = TypeCheckingUtils.VisitRvalueList(context.rvalueList(), exprVisitor).ToArray();

            if (evtExpr is EventRefExpr eventRef)
            {
                TypeCheckingUtils.ValidatePayloadTypes(handler, context, eventRef.Value.PayloadType, args);
            }

            method.CanSend = true;

            IPExpr distribution = null;
            if (context.dist != null)
            {
                distribution = exprVisitor.Visit(context.dist);
                if (!PrimitiveType.String.IsAssignableFrom(distribution.Type))
                {
                    throw handler.TypeMismatch(context.dist, distribution.Type, PrimitiveType.String);
                }
            }

            return new SendStmt(context, machineExpr, evtExpr, args, distribution);
        }

        private static bool IsDefinitelyNullEvent(IPExpr evtExpr)
        {
            return evtExpr is NullLiteralExpr || evtExpr is EventRefExpr evtRef && evtRef.Value.Name.Equals("null");
        }

        public override IPStmt VisitAnnounceStmt(PParser.AnnounceStmtContext context)
        {
            if (machine?.IsSpec == true)
            {
                throw handler.IllegalMonitorOperation(context, context.ANNOUNCE().Symbol, machine);
            }

            var evtExpr = exprVisitor.Visit(context.expr());
            if (IsDefinitelyNullEvent(evtExpr))
            {
                throw handler.EmittedNullEvent(evtExpr);
            }

            if (!PrimitiveType.Event.IsAssignableFrom(evtExpr.Type))
            {
                throw handler.TypeMismatch(context.expr(), evtExpr.Type, PrimitiveType.Event);
            }

            method.CanSend = true;

            var args = TypeCheckingUtils.VisitRvalueList(context.rvalueList(), exprVisitor).ToList();

            if (evtExpr is EventRefExpr eventRef)
            {
                TypeCheckingUtils.ValidatePayloadTypes(handler, context, eventRef.Value.PayloadType, args);
            }

            return new AnnounceStmt(context, evtExpr, args.Count == 0 ? null : args[0]);
        }

        public override IPStmt VisitGotoStmt(PParser.GotoStmtContext context)
        {
            if (!method.Signature.ReturnType.IsSameTypeAs(PrimitiveType.Null))
            {
                throw handler.ChangeStateInNonVoidFunction(context);
            }

            var stateNameContext = context.stateName();
            var stateName = stateNameContext.state.GetText();
            IStateContainer current = machine;

            var state = current?.GetState(stateName);
            if (state == null)
            {
                throw handler.MissingDeclaration(stateNameContext.state, "state", stateName);
            }

            var expectedType =
                state.Entry?.Signature.ParameterTypes.ElementAtOrDefault(0) ?? PrimitiveType.Null;
            var rvaluesList = TypeCheckingUtils.VisitRvalueList(context.rvalueList(), exprVisitor).ToArray();

            var expectedArgs = state.Entry?.Signature.Parameters.Count() ?? 0;
            if (rvaluesList.Length != expectedArgs)
            {
                throw handler.IncorrectArgumentCount(context, rvaluesList.Length, expectedArgs);
            }

            IPExpr payload;
            if (rvaluesList.Length == 0)
            {
                payload = null;
            }
            else if (rvaluesList.Length == 1)
            {
                payload = rvaluesList[0];
            }
            else
            {
                payload = new UnnamedTupleExpr(context, rvaluesList);
            }

            var payloadType = payload?.Type ?? PrimitiveType.Null;
            if (!expectedType.IsAssignableFrom(payloadType))
            {
                throw handler.TypeMismatch(context, payloadType, expectedType);
            }

            method.CanChangeState = true;
            return new GotoStmt(context, state, payload);
        }

        public override IPStmt VisitReceiveStmt(PParser.ReceiveStmtContext context)
        {
            if (machine?.IsSpec == true)
            {
                throw handler.IllegalMonitorOperation(context, context.RECEIVE().Symbol, machine);
            }

            var cases = new Dictionary<Event, Function>();
            foreach (var caseContext in context.recvCase())
            {


                foreach (var eventIdContext in caseContext.eventList().eventId())
                {
                    var recvHandler =
                        new Function(caseContext.anonEventHandler())
                        {
                            Scope = table.MakeChildScope(),
                            Owner = method.Owner,
                            ParentFunction = method,
                            Role = FunctionRole.ReceiveHandler
                        };

                    var param = caseContext.anonEventHandler().funParam();
                    if (param != null)
                    {
                        var paramVar = recvHandler.Scope.Put(param.name.GetText(), param, VariableRole.Param);
                        paramVar.Type = TypeResolver.ResolveType(param.type(), recvHandler.Scope, handler);
                        recvHandler.Signature.Parameters.Add(paramVar);
                    }

                    FunctionBodyVisitor.PopulateMethod(config, recvHandler);

                    if (!table.Lookup(eventIdContext.GetText(), out Event pEvent))
                    {
                        throw handler.MissingDeclaration(eventIdContext, "event", eventIdContext.GetText());
                    }

                    if (cases.ContainsKey(pEvent))
                    {
                        throw handler.DuplicateReceiveCase(eventIdContext, pEvent);
                    }

                    var expectedType =
                        recvHandler.Signature.ParameterTypes.ElementAtOrDefault(0) ?? PrimitiveType.Null;
                    if (!expectedType.IsAssignableFrom(pEvent.PayloadType))
                    {
                        throw handler.TypeMismatch(caseContext.anonEventHandler(), expectedType,
                            pEvent.PayloadType);
                    }

                    if (recvHandler.CanChangeState == true)
                    {
                        if (!method.Signature.ReturnType.IsSameTypeAs(PrimitiveType.Null))
                        {
                            throw handler.ChangeStateInNonVoidFunction(context);
                        }

                        method.CanChangeState = true;
                    }

                    if (recvHandler.CanRaiseEvent == true)
                    {
                        if (!method.Signature.ReturnType.IsSameTypeAs(PrimitiveType.Null))
                        {
                            throw handler.RaiseEventInNonVoidFunction(context);
                        }

                        method.CanRaiseEvent = true;
                    }

                    foreach (var callee in recvHandler.Callees)
                    {
                        method.AddCallee(callee);
                    }

                    cases.Add(pEvent, recvHandler);
                }
            }

            method.CanReceive = true;
            return new ReceiveStmt(context, cases);
        }

        public override IPStmt VisitNoStmt(PParser.NoStmtContext context)
        {
            return new NoStmt(context);
        }
    }
}