using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Plang.Compiler.Backend.ASTExt;
using Plang.Compiler.TypeChecker.AST;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.TypeChecker.AST.Expressions;
using Plang.Compiler.TypeChecker.AST.Statements;
using Plang.Compiler.TypeChecker.Types;

namespace Plang.Compiler.Backend
{
    public class IRTransformer
    {
        private readonly Function function;
        private int numTemp;

        private IRTransformer(Function function)
        {
            this.function = function;
        }

        public static void SimplifyMethod(Function function)
        {
            if (function.IsForeign)
            {
                return;
            }

            var transformer = new IRTransformer(function);
            IPStmt functionBody = function.Body;
            function.Body = new CompoundStmt(functionBody.SourceLocation, transformer.SimplifyStatement(functionBody));
        }

        private (VariableAccessExpr, IPStmt) SaveInTemporary(IPExpr expr)
        {
            return SaveInTemporary(expr, expr.Type);
        }

        private (VariableAccessExpr, IPStmt) SaveInTemporary(IPExpr expr, PLanguageType tempType)
        {
            var location = expr.SourceLocation;
            var temp = function.Scope.Put($"$tmp{numTemp++}", location, VariableRole.Local | VariableRole.Temp);
            Debug.Assert(tempType.IsAssignableFrom(expr.Type));
            temp.Type = tempType;
            function.AddLocalVariable(temp);
            var stmt = new AssignStmt(location, new VariableAccessExpr(location, temp), expr);
            return (new VariableAccessExpr(location, temp), stmt);
        }

        private (IPExpr, List<IPStmt>) SimplifyLvalue(IPExpr expr)
        {
            // TODO: I am suspicious.
            var location = expr.SourceLocation;
            var type = expr.Type;
#pragma warning disable CCN0002 // Non exhaustive patterns in switch block
            switch (expr)
            {
                case MapAccessExpr mapAccessExpr:
                    (var mapExpr, var mapExprDeps) = SimplifyLvalue(mapAccessExpr.MapExpr);
                    (var mapIndex, var mapIndexDeps) = SimplifyExpression(mapAccessExpr.IndexExpr);
                    return (new MapAccessExpr(location, mapExpr, mapIndex, type),
                        mapExprDeps.Concat(mapIndexDeps).ToList());

                case SetAccessExpr setAccessExpr:
                    (var setExpr, var setExprDeps) = SimplifyLvalue(setAccessExpr.SetExpr);
                    (var setIndex, var setIndexDeps) = SimplifyExpression(setAccessExpr.IndexExpr);
                    return (new SetAccessExpr(location, setExpr, setIndex, type),
                        setExprDeps.Concat(setIndexDeps).ToList());

                case NamedTupleAccessExpr namedTupleAccessExpr:
                    (var ntExpr, var ntExprDeps) = SimplifyLvalue(namedTupleAccessExpr.SubExpr);
                    return (new NamedTupleAccessExpr(location, ntExpr, namedTupleAccessExpr.Entry), ntExprDeps);

                case SeqAccessExpr seqAccessExpr:
                    (var seqExpr, var seqExprDeps) = SimplifyLvalue(seqAccessExpr.SeqExpr);
                    (var seqIndex, var seqIndexDeps) = SimplifyExpression(seqAccessExpr.IndexExpr);
                    return (new SeqAccessExpr(location, seqExpr, seqIndex, type),
                        seqExprDeps.Concat(seqIndexDeps).ToList());

                case TupleAccessExpr tupleAccessExpr:
                    (var tupExpr, var tupExprDeps) = SimplifyLvalue(tupleAccessExpr.SubExpr);
                    return (new TupleAccessExpr(location, tupExpr, tupleAccessExpr.FieldNo, type), tupExprDeps);

                case VariableAccessExpr variableAccessExpr:
                    return (variableAccessExpr, new List<IPStmt>());

                default:
                    throw new ArgumentOutOfRangeException(nameof(expr));
            }
#pragma warning restore CCN0002 // Non exhaustive patterns in switch block
        }

        private (IExprTerm, List<IPStmt>) SimplifyRvalue(IPExpr expr)
        {
            // TODO: I am suspicious.
            var location = expr.SourceLocation;
            var type = expr.Type;
#pragma warning disable CCN0002 // Non exhaustive patterns in switch block
            switch (expr)
            {
                case NamedTupleAccessExpr _:
                case SeqAccessExpr _:
                case SetAccessExpr _:
                case TupleAccessExpr _:
                case MapAccessExpr _:
                    (var dataTypeItem, var dataTypeItemDeps) = SimplifyExpression(expr);
                    (IExprTerm cloneddataTypeItem, var cloneddataTypeItemDeps) = SaveInTemporary(new CloneExpr(dataTypeItem));
                    return (cloneddataTypeItem, dataTypeItemDeps.Append(cloneddataTypeItemDeps).ToList());

                default:
                    return SimplifyExpression(expr);
            }
#pragma warning restore CCN0002 // Non exhaustive patterns in switch block
        }

        private (IExprTerm, List<IPStmt>) SimplifyExpression(IPExpr expr)
        {
            var location = expr.SourceLocation;
            var deps = new List<IPStmt>();
#pragma warning disable CCN0002 // Non exhaustive patterns in switch block
            switch (expr)
            {
                case IExprTerm term:
                    return (term, deps);

                case BinOpExpr binOpExpr:
                    (var lhsTemp, var lhsDeps) = SimplifyExpression(binOpExpr.Lhs);
                    (var rhsTemp, var rhsDeps) = SimplifyExpression(binOpExpr.Rhs);

                    if (binOpExpr.Operation == BinOpType.And)
                    {
                        // And is short-circuiting, so we need to treat it differently from other binary operators
                        deps.AddRange(lhsDeps);
                        (var andTemp, var andInitialStore) = SaveInTemporary(new CloneExpr(lhsTemp));
                        deps.Add(andInitialStore);
                        var reassignFromRhs = new CompoundStmt(location, rhsDeps.Append(new AssignStmt(location, andTemp, new CloneExpr(rhsTemp))));
                        deps.Add(new IfStmt(location, andTemp, reassignFromRhs, null));
                        return (andTemp, deps);
                    }
                    else if (binOpExpr.Operation == BinOpType.Or)
                    {
                        // Or is short-circuiting, so we need to treat it differently from other binary operators
                        deps.AddRange(lhsDeps);
                        (var orTemp, var orInitialStore) = SaveInTemporary(new CloneExpr(lhsTemp));
                        deps.Add(orInitialStore);
                        var reassignFromRhs = new CompoundStmt(location, rhsDeps.Append(new AssignStmt(location, orTemp, new CloneExpr(rhsTemp))));
                        deps.Add(new IfStmt(location, orTemp, new NoStmt(location), reassignFromRhs));
                        return (orTemp, deps);
                    }
                    else
                    {
                        (var binOpTemp, var binOpStore) =
                            SaveInTemporary(new BinOpExpr(location, binOpExpr.Operation, lhsTemp, rhsTemp));
                        deps.AddRange(lhsDeps.Concat(rhsDeps));
                        deps.Add(binOpStore);
                        return (binOpTemp, deps);
                    }

                case CastExpr castExpr:
                    (var castSubExpr, var castDeps) = SimplifyExpression(castExpr.SubExpr);
                    (var castTemp, var castStore) = SaveInTemporary(new CastExpr(location, new CloneExpr(castSubExpr), castExpr.Type));
                    deps.AddRange(castDeps);
                    deps.Add(castStore);
                    return (castTemp, deps);

                case ChooseExpr chooseExpr:
                    if (chooseExpr.SubExpr != null)
                    {
                        (var chooseSubExpr, var chooseDeps) = SimplifyExpression(chooseExpr.SubExpr);
                        (var chooseTemp, var chooseStore) = SaveInTemporary(new ChooseExpr(location, chooseSubExpr, chooseExpr.Type));
                        deps.AddRange(chooseDeps);
                        deps.Add(chooseStore);
                        return (chooseTemp, deps);
                    }
                    else
                    {
                        (var chooseTemp, var chooseStore) = SaveInTemporary(chooseExpr);
                        deps.Add(chooseStore);
                        return (chooseTemp, deps);
                    }

                case CoerceExpr coerceExpr:
                    (var coerceSubExpr, var coerceDeps) = SimplifyExpression(coerceExpr.SubExpr);
                    (var coerceTemp, var coerceStore) =
                        SaveInTemporary(new CoerceExpr(location, coerceSubExpr, coerceExpr.NewType));
                    deps.AddRange(coerceDeps);
                    deps.Add(coerceStore);
                    return (coerceTemp, deps);

                case ContainsExpr containsKeyExpr:
                    (var contKeyExpr, var contKeyDeps) = SimplifyExpression(containsKeyExpr.Item);
                    (var contMapExpr, var contMapDeps) = SimplifyExpression(containsKeyExpr.Collection);
                    (var contTemp, var contStore) =
                        SaveInTemporary(new ContainsExpr(location, contKeyExpr, contMapExpr));
                    deps.AddRange(contKeyDeps.Concat(contMapDeps));
                    deps.Add(contStore);
                    return (contTemp, deps);

                case CtorExpr ctorExpr:
                    (var ctorArgs, var ctorArgDeps) = SimplifyArgPack(ctorExpr.Arguments);
                    deps.AddRange(ctorArgDeps);
                    (var ctorTemp, var ctorStore) = SaveInTemporary(new CtorExpr(location, ctorExpr.Interface, ctorArgs));
                    deps.Add(ctorStore);
                    return (ctorTemp, deps);

                case DefaultExpr defaultExpr:
                    (var defTemp, var defStore) = SaveInTemporary(defaultExpr);
                    deps.Add(defStore);
                    return (defTemp, deps);

                case FairNondetExpr fairNondetExpr:
                    (var fndTemp, var fndStore) = SaveInTemporary(fairNondetExpr);
                    deps.Add(fndStore);
                    return (fndTemp, deps);

                case FunCallExpr funCallExpr:
                    (var funArgs, var funArgsDeps) = SimplifyFunArgs(funCallExpr.Arguments);
                    deps.AddRange(funArgsDeps);
                    (var funTemp, var funStore) = SaveInTemporary(new FunCallExpr(location, funCallExpr.Function, funArgs));
                    deps.Add(funStore);
                    return (funTemp, deps);

                case KeysExpr keysExpr:
                    (var keysColl, var keysDeps) = SimplifyExpression(keysExpr.Expr);
                    (var keysTemp, var keysStore) = SaveInTemporary(new KeysExpr(location, keysColl, keysExpr.Type));
                    deps.AddRange(keysDeps);
                    deps.Add(keysStore);
                    return (keysTemp, deps);

                case MapAccessExpr mapAccessExpr:
                    (var mapExpr, var mapDeps) = SimplifyExpression(mapAccessExpr.MapExpr);
                    (var mapIdxExpr, var mapIdxDeps) = SimplifyExpression(mapAccessExpr.IndexExpr);
                    (var mapItemTemp, var mapItemStore) =
                        SaveInTemporary(new MapAccessExpr(location, mapExpr, mapIdxExpr, mapAccessExpr.Type));
                    deps.AddRange(mapDeps.Concat(mapIdxDeps));
                    deps.Add(mapItemStore);
                    return (mapItemTemp, deps);

                case SetAccessExpr setAccessExpr:
                    (var setExpr, var setDeps) = SimplifyExpression(setAccessExpr.SetExpr);
                    (var setIdxExpr, var setIdxDeps) = SimplifyExpression(setAccessExpr.IndexExpr);
                    (var setItemTemp, var setItemStore) =
                        SaveInTemporary(new SetAccessExpr(location, setExpr, setIdxExpr, setAccessExpr.Type));
                    deps.AddRange(setDeps.Concat(setIdxDeps));
                    deps.Add(setItemStore);
                    return (setItemTemp, deps);

                case NamedTupleAccessExpr namedTupleAccessExpr:
                    (var ntSubExpr, var ntSubDeps) = SimplifyExpression(namedTupleAccessExpr.SubExpr);
                    (var ntTemp, var ntStore) =
                        SaveInTemporary(new NamedTupleAccessExpr(location, ntSubExpr, namedTupleAccessExpr.Entry));
                    deps.AddRange(ntSubDeps);
                    deps.Add(ntStore);
                    return (ntTemp, deps);

                case NamedTupleExpr namedTupleExpr:
                    (var args, var argDeps) = SimplifyArgPack(namedTupleExpr.TupleFields);
                    deps.AddRange(argDeps);

                    (var ntVal, var ntValStore) =
                        SaveInTemporary(new NamedTupleExpr(location, args, namedTupleExpr.Type));
                    deps.Add(ntValStore);
                    return (ntVal, deps);

                case NondetExpr nondetExpr:
                    (var ndTemp, var ndStore) = SaveInTemporary(nondetExpr);
                    deps.Add(ndStore);
                    return (ndTemp, deps);

                case SeqAccessExpr seqAccessExpr:
                    (var seqExpr, var seqDeps) = SimplifyExpression(seqAccessExpr.SeqExpr);
                    (var seqIdx, var seqIdxDeps) = SimplifyExpression(seqAccessExpr.IndexExpr);
                    (var seqElem, var seqElemStore) =
                        SaveInTemporary(new SeqAccessExpr(location, seqExpr, seqIdx, seqAccessExpr.Type));
                    deps.AddRange(seqDeps.Concat(seqIdxDeps));
                    deps.Add(seqElemStore);
                    return (seqElem, deps);

                case SizeofExpr sizeofExpr:
                    (var sizeExpr, var sizeDeps) = SimplifyExpression(sizeofExpr.Expr);
                    (var sizeTemp, var sizeStore) = SaveInTemporary(new SizeofExpr(location, sizeExpr));
                    deps.AddRange(sizeDeps);
                    deps.Add(sizeStore);
                    return (sizeTemp, deps);

                case TupleAccessExpr tupleAccessExpr:
                    (var tupItemExpr, var tupAccessDeps) = SimplifyExpression(tupleAccessExpr.SubExpr);
                    (var tupItemTemp, var tupItemStore) =
                        SaveInTemporary(new TupleAccessExpr(location,
                            tupItemExpr,
                            tupleAccessExpr.FieldNo,
                            tupleAccessExpr.Type));
                    deps.AddRange(tupAccessDeps);
                    deps.Add(tupItemStore);
                    return (tupItemTemp, deps);

                case UnaryOpExpr unaryOpExpr:
                    (var unExpr, var unDeps) = SimplifyExpression(unaryOpExpr.SubExpr);
                    (var unTemp, var unStore) = SaveInTemporary(new UnaryOpExpr(location, unaryOpExpr.Operation, unExpr));
                    deps.AddRange(unDeps);
                    deps.Add(unStore);
                    return (unTemp, deps);

                case UnnamedTupleExpr unnamedTupleExpr:
                    (var tupFields, var tupFieldDeps) = SimplifyArgPack(unnamedTupleExpr.TupleFields);
                    deps.AddRange(tupFieldDeps);
                    (var tupVal, var tupStore) = SaveInTemporary(new UnnamedTupleExpr(location, tupFields));
                    deps.Add(tupStore);
                    return (tupVal, deps);

                case ValuesExpr valuesExpr:
                    (var valuesColl, var valuesDeps) = SimplifyExpression(valuesExpr.Expr);
                    (var valuesTemp, var valuesStore) =
                        SaveInTemporary(new ValuesExpr(location, valuesColl, valuesExpr.Type));
                    deps.AddRange(valuesDeps);
                    deps.Add(valuesStore);
                    return (valuesTemp, deps);

                case StringExpr stringExpr:
                    (IPExpr[] stringArgs, var stringArgsDeps) = SimplifyFunArgs(stringExpr.Args);
                    (var stringTemp, var stringStore) = SaveInTemporary(new StringExpr(location, stringExpr.BaseString, stringArgs.ToList()));
                    deps.AddRange(stringArgsDeps);
                    deps.Add(stringStore);
                    return (stringTemp, deps);
                default:
                    throw new ArgumentOutOfRangeException(nameof(expr));
            }
#pragma warning restore CCN0002 // Non exhaustive patterns in switch block
        }

        private List<IPStmt> SimplifyStatement(IPStmt statement)
        {
            var location = statement?.SourceLocation;
            switch (statement)
            {
                case null:
                    throw new ArgumentNullException(nameof(statement));
                case AnnounceStmt announceStmt:
                    (var annEvt, var annEvtDeps) = SimplifyExpression(announceStmt.Event);
                    (IExprTerm annPayload, List<IPStmt> annPayloadDeps) = announceStmt.Payload == null
                        ? (null, new List<IPStmt>())
                        : SimplifyExpression(announceStmt.Payload);
                    return annEvtDeps.Concat(annPayloadDeps)
                        .Concat(new[]
                        {
                            new AnnounceStmt(location, annEvt, annPayload)
                        })
                        .ToList();

                case AssertStmt assertStmt:
                    (var assertExpr, var assertDeps) = SimplifyExpression(assertStmt.Assertion);
                    (var messageExpr, var messageDeps) = SimplifyExpression(assertStmt.Message);
                    if (assertExpr is BoolLiteralExpr)
                    {
                        return assertDeps.Concat(messageDeps).Concat(new []{new AssertStmt(location, assertExpr, messageExpr)}).ToList();
                    }

                    var ifStmtForAssert = new IfStmt(location, assertExpr, new NoStmt(location), new CompoundStmt(
                        location, messageDeps.Concat(new[]
                        {
                            new AssertStmt(location, assertExpr, messageExpr)
                        }).ToList()));
                    return assertDeps.Concat(new List<IPStmt>{ifStmtForAssert})
                        .ToList();

                case AssignStmt assignStmt:
                    (var assignLV, var assignLVDeps) = SimplifyLvalue(assignStmt.Location);
                    (var assignRV, var assignRVDeps) = SimplifyRvalue(assignStmt.Value);
                    IPStmt assignment;
                    // If temporary returned, then automatically move.
                    if (assignRV is VariableAccessExpr variableRef &&
                        variableRef.Variable.Role.HasFlag(VariableRole.Temp))
                    {
                        assignment = new MoveAssignStmt(location, assignLV, variableRef.Variable);
                    }
                    else
                    {
                        assignment = new AssignStmt(location, assignLV, new CloneExpr(assignRV));
                    }

                    return assignLVDeps.Concat(assignRVDeps).Concat(new[] { assignment }).ToList();

                case CompoundStmt compoundStmt:
                    var newBlock = new List<IPStmt>();
                    foreach (var step in compoundStmt.Statements)
                    {
                        newBlock.AddRange(SimplifyStatement(step));
                    }
                    // TODO: why not return the list? because of source location info?
                    return new List<IPStmt> { new CompoundStmt(location, newBlock) };

                case CtorStmt ctorStmt:
                    (var ctorArgs, var ctorArgDeps) = SimplifyArgPack(ctorStmt.Arguments);
                    return ctorArgDeps.Concat(new[]
                        {
                            new CtorStmt(location, ctorStmt.Interface, ctorArgs)
                        })
                        .ToList();

                case FunCallStmt funCallStmt:
                    (var funCallArgs, var funCallArgDeps) = SimplifyFunArgs(funCallStmt.ArgsList);
                    return funCallArgDeps.Concat(new[]
                        {
                            new FunCallStmt(location, funCallStmt.Function, funCallArgs)
                        })
                        .ToList();

                case GotoStmt gotoStmt:
                    if (gotoStmt.Payload == null)
                    {
                        return new List<IPStmt> { gotoStmt };
                    }

                    (var gotoPayload, var gotoDeps) = SimplifyExpression(gotoStmt.Payload);
                    (var gotoArgTmp, var gotoArgDep) = SaveInTemporary(new CloneExpr(gotoPayload));
                    return gotoDeps.Concat(new[]
                        {
                            gotoArgDep,
                            new GotoStmt(location, gotoStmt.State, gotoArgTmp)
                        })
                        .ToList();

                case IfStmt ifStmt:
                    (var ifCond, var ifCondDeps) = SimplifyExpression(ifStmt.Condition);
                    var thenBranch = SimplifyStatement(ifStmt.ThenBranch);
                    var elseBranch = SimplifyStatement(ifStmt.ElseBranch);
                    return ifCondDeps.Concat(new[]
                        {
                            new IfStmt(location,
                                ifCond,
                                new CompoundStmt(ifStmt.ThenBranch.SourceLocation, thenBranch),
                                new CompoundStmt(ifStmt.ElseBranch.SourceLocation, elseBranch))
                        })
                        .ToList();

                case AddStmt addStmt:
                    var (addVar, addVarDeps) = SimplifyLvalue(addStmt.Variable);
                    var (addVal, addValDeps) = SimplifyArgPack(new[] { addStmt.Value });
                    Debug.Assert(addVal.Count == 1);
                    return addVarDeps.Concat(addValDeps)
                        .Concat(new[]
                        {
                            new AddStmt(location, addVar, addVal[0])
                        })
                        .ToList();

                case InsertStmt insertStmt:
                    (var insVar, var insVarDeps) = SimplifyLvalue(insertStmt.Variable);
                    (var insIdx, var insIdxDeps) = SimplifyExpression(insertStmt.Index);
                    (var insVal, var insValDeps) = SimplifyArgPack(new[] { insertStmt.Value });
                    Debug.Assert(insVal.Count == 1);
                    return insVarDeps.Concat(insIdxDeps)
                        .Concat(insValDeps)
                        .Concat(new[]
                        {
                            new InsertStmt(location, insVar, insIdx, insVal[0])
                        })
                        .ToList();

                case MoveAssignStmt moveAssignStmt:
                    (var moveDest, var moveDestDeps) = SimplifyLvalue(moveAssignStmt.ToLocation);
                    return moveDestDeps.Concat(new[]
                        {
                            new MoveAssignStmt(moveAssignStmt.SourceLocation, moveDest, moveAssignStmt.FromVariable)
                        })
                        .ToList();

                case NoStmt _:
                    return new List<IPStmt>();

                case PrintStmt printStmt:
                    var deps = new List<IPStmt>();
                    (var newMessage, var printDeps) = SimplifyExpression(printStmt.Message);
                    deps.AddRange(printDeps);
                    return deps.Concat(new[] { new PrintStmt(location, newMessage) }).ToList();

                case RaiseStmt raiseStmt:
                    (var raiseEvent, var raiseEventDeps) = SimplifyExpression(raiseStmt.Event);
                    (var raiseEventTmp, var raiseEventTempDep) = SaveInTemporary(new CloneExpr(raiseEvent));

                    (var raiseArgs, var raiseArgDeps) = SimplifyArgPack(raiseStmt.Payload);

                    return raiseEventDeps.Concat(raiseEventDeps)
                        .Concat(raiseArgDeps)
                        .Concat(new[] { raiseEventTempDep })
                        .Concat(new[]
                        {
                            new RaiseStmt(location, raiseEventTmp, raiseArgs)
                        })
                        .ToList();

                case ReceiveStmt receiveStmt:
                    foreach (var recvCase in receiveStmt.Cases.Values)
                    {
                        IPStmt functionBody = recvCase.Body;
                        recvCase.Body = new CompoundStmt(functionBody.SourceLocation, SimplifyStatement(functionBody));
                    }

                    return new List<IPStmt> { receiveStmt };

                case RemoveStmt removeStmt:
                    (var removeVar, var removeVarDeps) = SimplifyLvalue(removeStmt.Variable);
                    (var removeKey, var removeKeyDeps) = SimplifyExpression(removeStmt.Value);
                    return removeVarDeps.Concat(removeKeyDeps)
                        .Concat(new[]
                        {
                            new RemoveStmt(location, removeVar, removeKey)
                        })
                        .ToList();

                case ReturnStmt returnStmt:
                    if (returnStmt.ReturnValue == null)
                    {
                        return new List<IPStmt> { returnStmt };
                    }

                    (var returnValue, var returnValueDeps) = SimplifyExpression(returnStmt.ReturnValue);
                    return returnValueDeps.Concat(new[]
                        {
                            new ReturnStmt(location, new CloneExpr(returnValue))
                        })
                        .ToList();

                case BreakStmt breakStmt:
                    return new List<IPStmt> { breakStmt };

                case ContinueStmt continueStmt:
                    return new List<IPStmt> { continueStmt };

                case SendStmt sendStmt:
                    (var sendMachine, var sendMachineDeps) = SimplifyExpression(sendStmt.MachineExpr);
                    (var sendMachineAccessExpr, var sendMachineAssn) = SaveInTemporary(new CloneExpr(sendMachine));

                    (var sendEvent, var sendEventDeps) = SimplifyExpression(sendStmt.Evt);
                    (var sendEventAccessExpr, var sendEventAssn) = SaveInTemporary(new CloneExpr(sendEvent));

                    (var sendArgs, var sendArgDeps) = SimplifyArgPack(sendStmt.Arguments);

                    return sendMachineDeps
                        .Concat(new[] { sendMachineAssn })
                        .Concat(sendEventDeps)
                        .Concat(new[] { sendEventAssn })
                        .Concat(sendArgDeps)
                        .Concat(new[]
                        {
                            new SendStmt(location, sendMachineAccessExpr, sendEventAccessExpr, sendArgs, sendStmt.DelayDistribution)
                        })
                        .ToList();

                case ForeachStmt foreachStmt:
                    return SimplifyStatement(SimplifyForeachStmt(foreachStmt));

                case WhileStmt whileStmt:
                    (var condExpr, var condDeps) = SimplifyExpression(whileStmt.Condition);
                    (var condTemp, var condStore) = SaveInTemporary(new CloneExpr(condExpr));
                    var condLocation = whileStmt.Condition.SourceLocation;
                    var condCheck =
                        condDeps
                            .Append(condStore)
                            .Append(new IfStmt(condLocation, condTemp, new NoStmt(condLocation), new BreakStmt(condLocation)))
                            .ToList();

                    var loopBody = new CompoundStmt(
                        whileStmt.Body.SourceLocation,
                        condCheck.Concat(SimplifyStatement(whileStmt.Body)));

                    return new List<IPStmt> { new WhileStmt(location, new BoolLiteralExpr(location, true), loopBody) };

                default:
                    throw new ArgumentOutOfRangeException(nameof(statement));
            }
        }

        private IPStmt SimplifyForeachStmt(ForeachStmt foreachStmt)
        {
            // var item: element.type;
            // foreach(item in collection) {
            //   body;
            // }
            //
            // is transformed to
            //
            // var item: element.type;
            // var collectionCopy: collection.type;
            // collectionCopy = collection;
            // var i: int;
            // var sizeof: int;
            // i = -1;
            // sizeof = sizeof(collectionCopy);
            // while(i < (sizeof - 1)) {
            //     i = i + 1;
            //     item = collectionCopy[i];
            //     body;
            // }

            var location = foreachStmt.SourceLocation;
            var item = foreachStmt.Item;
            var collection = foreachStmt.IterCollection;
            (IPExpr collectionCopy, var collectionCopyStore) = SaveInTemporary(collection);
            IPStmt body = foreachStmt.Body;

            var newBody = new List<IPStmt>();
            newBody.Add(collectionCopyStore);

            // var i: int;
            var iVar = function.Scope.Put($"$i_{item.Name}_tmp{numTemp++}", location, VariableRole.Local);
            iVar.Type = PrimitiveType.Int;
            function.AddLocalVariable(iVar);

            // var sizeof: int;
            var sizeVar = new Variable($"sizeof_{item.Name}_tmp{numTemp++}", location, VariableRole.Local);
            sizeVar.Type = PrimitiveType.Int;
            function.AddLocalVariable(sizeVar);

            // i = -1;
            newBody.Add(new AssignStmt(location, new VariableAccessExpr(location, iVar), new IntLiteralExpr(location, -1)));

            // sizeof = sizeof(collection)
            newBody.Add(new AssignStmt(location, new VariableAccessExpr(location, sizeVar), new SizeofExpr(location, collectionCopy)));

            // while(i < (sizeof - 1))
            IPExpr cond = new BinOpExpr(location, BinOpType.Lt,
                new VariableAccessExpr(location, iVar),
                new BinOpExpr(location, BinOpType.Sub,
                    new VariableAccessExpr(location, sizeVar),
                    new IntLiteralExpr(location, 1)));

            // inside loop: i = i+1;
            IPStmt incrementI = new AssignStmt(location, new VariableAccessExpr(location, iVar),
                new BinOpExpr(location,
                    BinOpType.Add,
                    new VariableAccessExpr(location, iVar),
                    new IntLiteralExpr(location, 1)));

            // inside loop: item = collection[i]
            IPExpr accessExpr;
            switch (collectionCopy.Type.Canonicalize())
            {
                case SequenceType seqType:
                    accessExpr = new SeqAccessExpr(location, collectionCopy, new VariableAccessExpr(location, iVar), seqType.ElementType);
                    break;

                case SetType setType:
                    accessExpr = new SetAccessExpr(location, collectionCopy, new VariableAccessExpr(location, iVar), setType.ElementType);
                    break;

                default:
                    throw new ArgumentException($"Error in converting foreach to while in {function.Name}");
            }
            IPStmt assignItem = new AssignStmt(location, new VariableAccessExpr(location, item), accessExpr);

            newBody.Add(new WhileStmt(location, cond, new CompoundStmt(location, new List<IPStmt>{ incrementI, assignItem,  body })));
            return new CompoundStmt(location, newBody);
        }

        private (IReadOnlyList<IVariableRef> args, List<IPStmt> deps) SimplifyArgPack(IEnumerable<IPExpr> argsPack)
        {
            var args = argsPack.ToList();
            (var refArgs, var deps) = SimplifyFunArgs(args);
            return (refArgs, deps);
        }

        private (IVariableRef[], List<IPStmt>) SimplifyFunArgs(IReadOnlyList<IPExpr> funCallArgs)
        {
            var funArgs = new IVariableRef[funCallArgs.Count];
            var deps = new List<IPStmt>();
            for (var i = 0; i < funCallArgs.Count; i++)
            {
                (var argExpr, var argDeps) = SimplifyExpression(funCallArgs[i]);
                deps.AddRange(argDeps);
                // My thoughts: if SimplifyExpression returns a temporary, there's no way that came from user code, thus
                // it must be either an intermediate expression, or an explicit clone of another value. In either case, the
                // memory there is invisible to the rest of the program and so it can be moved safely. The first case below
                // is therefore an optimization that assumes this holds. If things go wrong, comment it out and take the extra
                // allocations as a hit.
                switch (argExpr)
                {
                    // Move temporaries...
                    case VariableAccessExpr variableAccessExpr
                        when variableAccessExpr.Variable.Role.HasFlag(VariableRole.Temp):
                        funArgs[i] = new VariableAccessExpr(variableAccessExpr.SourceLocation,
                            variableAccessExpr.Variable);
                        break;
                    // ...but clone literals and visible variables/fields.
                    default:
                        (var temp, var tempDep) = SaveInTemporary(new CloneExpr(argExpr));
                        deps.Add(tempDep);
                        funArgs[i] = new VariableAccessExpr(temp.SourceLocation, temp.Variable);
                        break;
                }
            }

            return (funArgs, deps);
        }
    }
}