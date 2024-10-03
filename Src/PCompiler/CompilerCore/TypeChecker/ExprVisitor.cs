using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Antlr4.Runtime.Misc;
using Plang.Compiler.TypeChecker.AST;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.TypeChecker.AST.Expressions;
using Plang.Compiler.TypeChecker.Types;

namespace Plang.Compiler.TypeChecker
{
    public class ExprVisitor : PParserBaseVisitor<IPExpr>
    {
        public static readonly string EventFieldSender = "MSG_sender";
        public static readonly string EventFieldReceiver = "MSG_receiver";
        public static readonly string EventFieldState = "MSG_state";
        public static readonly string EventFieldIndex = "MSG_index";

        public static Dictionary<string, NamedTupleEntry> ReservedEventFeilds = new Dictionary<string, NamedTupleEntry>()
        {
            { EventFieldSender, new NamedTupleEntry("Sender", -1, PrimitiveType.String) },
            { EventFieldReceiver, new NamedTupleEntry("Receiver", -1, PrimitiveType.String) },
            { EventFieldState, new NamedTupleEntry("State", -1, PrimitiveType.String) },
            { EventFieldIndex, new NamedTupleEntry("Index", -1, PrimitiveType.Int) },
        };
        private readonly ITranslationErrorHandler handler;
        private readonly Function method;
        private readonly Scope table;

        public ExprVisitor(Function method, ITranslationErrorHandler handler)
        {
            table = method.Scope;
            this.method = method;
            this.handler = handler;
        }

        public override IPExpr VisitPrimitiveExpr(PParser.PrimitiveExprContext context)
        {
            return Visit(context.primitive());
        }

        public override IPExpr VisitUnnamedTupleExpr(PParser.UnnamedTupleExprContext context)
        {
            return Visit(context.unnamedTupleBody());
        }

        public override IPExpr VisitNamedTupleExpr(PParser.NamedTupleExprContext context)
        {
            return Visit(context.namedTupleBody());
        }

        public override IPExpr VisitParenExpr(PParser.ParenExprContext context)
        {
            return Visit(context.expr());
        }

        public override IPExpr VisitNamedTupleAccessExpr(PParser.NamedTupleAccessExprContext context)
        {
            var subExpr = Visit(context.expr());
            var fieldName = context.field.GetText();
            if (subExpr is VariableAccessExpr v && method.Role == FunctionRole.Scenario && method.Signature.Parameters.Contains(v.Variable))
            {
                if (ReservedEventFeilds.TryGetValue(fieldName, out var reservedEntry))
                {
                    return new NamedTupleAccessExpr(context, subExpr, reservedEntry);
                }
            }
            if (!(subExpr.Type.Canonicalize() is NamedTupleType tuple))
            {
                throw handler.TypeMismatch(subExpr, TypeKind.NamedTuple);
            }
            if (!tuple.LookupEntry(fieldName, out var entry))
            {
                throw handler.MissingNamedTupleEntry(context.field, tuple);
            }

            return new NamedTupleAccessExpr(context, subExpr, entry);
        }

        public override IPExpr VisitTupleAccessExpr(PParser.TupleAccessExprContext context)
        {
            var subExpr = Visit(context.expr());
            var fieldNo = int.Parse(context.field.GetText());
            if (!(subExpr.Type.Canonicalize() is TupleType tuple))
            {
                throw handler.TypeMismatch(subExpr, TypeKind.Tuple, TypeKind.NamedTuple);
            }

            if (fieldNo >= tuple.Types.Count)
            {
                throw handler.OutOfBoundsTupleAccess(context.field, tuple);
            }

            return new TupleAccessExpr(context, subExpr, fieldNo, tuple.Types[fieldNo]);
        }

        public override IPExpr VisitSeqAccessExpr(PParser.SeqAccessExprContext context)
        {
            var seqOrMap = Visit(context.seq);
            var indexExpr = Visit(context.index);
            switch (seqOrMap.Type.Canonicalize())
            {
                case SequenceType seqType:
                    if (!PrimitiveType.Int.IsAssignableFrom(indexExpr.Type))
                    {
                        throw handler.TypeMismatch(context.index, indexExpr.Type, PrimitiveType.Int);
                    }

                    return new SeqAccessExpr(context, seqOrMap, indexExpr, seqType.ElementType);

                case MapType mapType:
                    if (!mapType.KeyType.IsAssignableFrom(indexExpr.Type))
                    {
                        throw handler.TypeMismatch(context.index, indexExpr.Type, mapType.KeyType);
                    }

                    return new MapAccessExpr(context, seqOrMap, indexExpr, mapType.ValueType);

                case SetType setType:
                    if (!PrimitiveType.Int.IsAssignableFrom(indexExpr.Type))
                    {
                        throw handler.TypeMismatch(context.index, indexExpr.Type, PrimitiveType.Int);
                    }

                    return new SetAccessExpr(context, seqOrMap, indexExpr, setType.ElementType);
            }

            throw handler.TypeMismatch(seqOrMap, TypeKind.Sequence, TypeKind.Map);
        }

        public override IPExpr VisitKeywordExpr(PParser.KeywordExprContext context)
        {
            switch (context.fun.Text)
            {
                case "keys":
                {
                    var expr = Visit(context.expr());
                    if (!(expr.Type.Canonicalize() is MapType mapType))
                    {
                        throw handler.TypeMismatch(expr, TypeKind.Map);
                    }

                    return new KeysExpr(context, expr, new SequenceType(mapType.KeyType));
                }
                case "values":
                {
                    var expr = Visit(context.expr());
                    if (!(expr.Type.Canonicalize() is MapType mapType))
                    {
                        throw handler.TypeMismatch(expr, TypeKind.Map);
                    }

                    return new ValuesExpr(context, expr, new SequenceType(mapType.ValueType));
                }
                case "sizeof":
                {
                    var expr = Visit(context.expr());
                    if (!(expr.Type.Canonicalize() is SequenceType)
                        && !(expr.Type.Canonicalize() is MapType)
                        && !(expr.Type.Canonicalize() is SetType))
                        throw handler.TypeMismatch(expr, TypeKind.Map, TypeKind.Sequence, TypeKind.Set);
                    return new SizeofExpr(context, expr);
                }
                case "default":
                {
                    var type = TypeResolver.ResolveType(context.type(), table, handler);
                    return new DefaultExpr(context, type.Canonicalize());
                }
                default:
                {
                    throw handler.InternalError(context,
                        new ArgumentException($"Unknown keyword expression {context.fun.Text}", nameof(context)));
                }
            }
        }

        public override IPExpr VisitCtorExpr(PParser.CtorExprContext context)
        {
            var interfaceName = context.interfaceName.GetText();
            if (!table.Lookup(interfaceName, out Interface @interface))
            {
                throw handler.MissingDeclaration(context.interfaceName, "interface", interfaceName);
            }

            if (method.Owner?.IsSpec == true)
            {
                throw handler.IllegalMonitorOperation(context, context.NEW().Symbol, method.Owner);
            }

            var arguments = TypeCheckingUtils.VisitRvalueList(context.rvalueList(), this).ToArray();
            TypeCheckingUtils.ValidatePayloadTypes(handler, context, @interface.PayloadType, arguments);

            method.CanCreate = true;

            return new CtorExpr(context, @interface, arguments);
        }

        public override IPExpr VisitFunCallExpr(PParser.FunCallExprContext context)
        {
            var funName = context.fun.GetText();
            if (!table.Lookup(funName, out Function function))
            {
                throw handler.MissingDeclaration(context.fun, "function", funName);
            }

            // Check the arguments
            var arguments = TypeCheckingUtils.VisitRvalueList(context.rvalueList(), this).ToArray();
            ISet<Variable> linearVariables = new HashSet<Variable>();

            if (function.Signature.Parameters.Count != arguments.Length)
            {
                throw handler.IncorrectArgumentCount(context, arguments.Length, function.Signature.Parameters.Count);
            }

            for (var i = 0; i < arguments.Length; i++)
            {
                var argument = arguments[i];
                var paramType = function.Signature.Parameters[i].Type;
                if (!paramType.IsAssignableFrom(argument.Type))
                {
                    throw handler.TypeMismatch(context.rvalueList().rvalue(i), argument.Type, paramType);
                }

            }

            method.AddCallee(function);
            return new FunCallExpr(context, function, arguments);
        }

        public override IPExpr VisitUnaryExpr(PParser.UnaryExprContext context)
        {
            var subExpr = Visit(context.expr());
            switch (context.op.Text)
            {
                case "-":
                    if (!PrimitiveType.Int.IsAssignableFrom(subExpr.Type) &&
                        !PrimitiveType.Float.IsAssignableFrom(subExpr.Type))
                    {
                        throw handler.TypeMismatch(context.expr(),
                            subExpr.Type,
                            PrimitiveType.Int,
                            PrimitiveType.Float);
                    }

                    return new UnaryOpExpr(context, UnaryOpType.Negate, subExpr);

                case "!":
                    if (!PrimitiveType.Bool.IsAssignableFrom(subExpr.Type))
                    {
                        throw handler.TypeMismatch(context.expr(), subExpr.Type, PrimitiveType.Bool);
                    }

                    return new UnaryOpExpr(context, UnaryOpType.Not, subExpr);

                default:
                    throw handler.InternalError(context,
                        new ArgumentException($"Unknown unary op `{context.op.Text}`", nameof(context)));
            }
        }

        public override IPExpr VisitBinExpr(PParser.BinExprContext context)
        {
            var lhs = Visit(context.lhs);
            var rhs = Visit(context.rhs);
            var op = context.op.Text;

            var arithCtors = new Dictionary<string, Func<IPExpr, IPExpr, IPExpr>>
            {
                {"*", (elhs, erhs) => new BinOpExpr(context, BinOpType.Mul, elhs, erhs)},
                {"/", (elhs, erhs) => new BinOpExpr(context, BinOpType.Div, elhs, erhs)},
                {"%", (elhs, erhs) => new BinOpExpr(context, BinOpType.Mod, elhs, erhs)},
                {"+", (elhs, erhs) => new BinOpExpr(context, BinOpType.Add, elhs, erhs)},
                {"-", (elhs, erhs) => new BinOpExpr(context, BinOpType.Sub, elhs, erhs)},
                {"<", (elhs, erhs) => new BinOpExpr(context, BinOpType.Lt, elhs, erhs)},
                {"<=", (elhs, erhs) => new BinOpExpr(context, BinOpType.Le, elhs, erhs)},
                {">", (elhs, erhs) => new BinOpExpr(context, BinOpType.Gt, elhs, erhs)},
                {">=", (elhs, erhs) => new BinOpExpr(context, BinOpType.Ge, elhs, erhs)}
            };

            var logicCtors = new Dictionary<string, Func<IPExpr, IPExpr, IPExpr>>
            {
                {"&&", (elhs, erhs) => new BinOpExpr(context, BinOpType.And, elhs, erhs)},
                {"||", (elhs, erhs) => new BinOpExpr(context, BinOpType.Or, elhs, erhs)}
            };

            var compCtors = new Dictionary<string, Func<IPExpr, IPExpr, IPExpr>>
            {
                {"==", (elhs, erhs) => new BinOpExpr(context, BinOpType.Eq, elhs, erhs)},
                {"!=", (elhs, erhs) => new BinOpExpr(context, BinOpType.Neq, elhs, erhs)}
            };

            switch (op)
            {
                case "+":
                    if (PrimitiveType.String.IsAssignableFrom(lhs.Type) &&
                        PrimitiveType.String.IsAssignableFrom(rhs.Type) ||
                        PrimitiveType.Int.IsAssignableFrom(lhs.Type) &&
                        PrimitiveType.Int.IsAssignableFrom(rhs.Type) ||
                        PrimitiveType.Float.IsAssignableFrom(lhs.Type) &&
                        PrimitiveType.Float.IsAssignableFrom(rhs.Type))
                    {
                        return arithCtors[op](lhs, rhs);
                    }
                    throw handler.BinOpTypeMismatch(context, lhs.Type, rhs.Type);
                case "*":
                case "/":
                case "-":
                case "<":
                case ">":
                case ">=":
                case "<=":
                    if (PrimitiveType.Int.IsAssignableFrom(lhs.Type) &&
                        PrimitiveType.Int.IsAssignableFrom(rhs.Type) ||
                        PrimitiveType.Float.IsAssignableFrom(lhs.Type) &&
                        PrimitiveType.Float.IsAssignableFrom(rhs.Type) ||
                        PrimitiveType.String.IsAssignableFrom(lhs.Type) &&
                        PrimitiveType.String.IsAssignableFrom(rhs.Type))
                    {
                        return arithCtors[op](lhs, rhs);
                    }
                    throw handler.BinOpTypeMismatch(context, lhs.Type, rhs.Type);
                case "%":
                    if (PrimitiveType.Int.IsAssignableFrom(lhs.Type) &&
                        PrimitiveType.Int.IsAssignableFrom(rhs.Type) ||
                        PrimitiveType.Float.IsAssignableFrom(lhs.Type) &&
                        PrimitiveType.Float.IsAssignableFrom(rhs.Type))
                    {
                        return arithCtors[op](lhs, rhs);
                    }
                    throw handler.IncomparableTypes(context, lhs.Type, rhs.Type);
                case "in":
                    var rhsType = rhs.Type.Canonicalize();
                    if (rhsType is MapType rhsMap)
                    {
                        if (!rhsMap.KeyType.IsAssignableFrom(lhs.Type))
                        {
                            throw handler.TypeMismatch(context.lhs, lhs.Type, rhsMap.KeyType);
                        }
                    }
                    else if (rhsType is SequenceType rhsSeq)
                    {
                        if (!rhsSeq.ElementType.IsAssignableFrom(lhs.Type))
                        {
                            throw handler.TypeMismatch(context.lhs, lhs.Type, rhsSeq.ElementType);
                        }
                    }
                    else if (rhsType is SetType rhsSet)
                    {
                        if (!rhsSet.ElementType.IsAssignableFrom(lhs.Type))
                        {
                            throw handler.TypeMismatch(context.lhs, lhs.Type, rhsSet.ElementType);
                        }
                    }
                    else
                    {
                        throw handler.TypeMismatch(rhs, TypeKind.Map, TypeKind.Sequence);
                    }
                    return new ContainsExpr(context, lhs, rhs);

                case "==":
                case "!=":
                    if (!lhs.Type.IsAssignableFrom(rhs.Type) && !rhs.Type.IsAssignableFrom(lhs.Type))
                    {
                        throw handler.IncomparableTypes(context, lhs.Type, rhs.Type);
                    }

                    return compCtors[op](lhs, rhs);

                case "&&":
                case "||":
                    if (!PrimitiveType.Bool.IsAssignableFrom(lhs.Type))
                    {
                        throw handler.TypeMismatch(context.lhs, lhs.Type, PrimitiveType.Bool);
                    }

                    if (!PrimitiveType.Bool.IsAssignableFrom(rhs.Type))
                    {
                        throw handler.TypeMismatch(context.rhs, rhs.Type, PrimitiveType.Bool);
                    }

                    return logicCtors[op](lhs, rhs);

                default:
                    throw handler.InternalError(context,
                        new ArgumentException($"unknown binary operation {op}", nameof(context)));
            }
        }

        public override IPExpr VisitChooseExpr(PParser.ChooseExprContext context)
        {
            method.IsNondeterministic = true;
            // if choose is without an argument then its a choose boolean
            if (context.expr() == null)
            {
                return new ChooseExpr(context, null, PrimitiveType.Bool);
            }

            var subExpr = Visit(context.expr());
            var subExprType = subExpr.Type;

            switch (subExprType.Canonicalize())
            {
                case SequenceType seqType:
                    return new ChooseExpr(context, subExpr, seqType.ElementType);

                case SetType setType:
                    return new ChooseExpr(context, subExpr, setType.ElementType);

                case MapType mapType:
                    return new ChooseExpr(context, subExpr, mapType.KeyType);

                case PrimitiveType primType when primType.IsSameTypeAs(PrimitiveType.Int):
                {
                    if (subExpr is IntLiteralExpr subExprAsInt && subExprAsInt.Value > 10000)
                        throw handler.IllegalChooseSubExprValue(context, subExprAsInt.Value);
                    return new ChooseExpr(context, subExpr, PrimitiveType.Int);
                }

                default:
                    throw handler.IllegalChooseSubExprType(context, subExprType);
            }

        }

        public override IPExpr VisitCastExpr(PParser.CastExprContext context)
        {
            var subExpr = Visit(context.expr());
            var oldType = subExpr.Type;
            var newType = TypeResolver.ResolveType(context.type(), table, handler);
            if (context.cast.Text.Equals("as"))
            {
                if (!newType.IsAssignableFrom(oldType) && !oldType.IsAssignableFrom(newType))
                {
                    throw handler.IncomparableTypes(context, oldType, newType);
                }

                return new CastExpr(context, subExpr, newType);
            }

            if (context.cast.Text.Equals("to"))
            {
                if (!(newType is PermissionType || newType.IsSameTypeAs(PrimitiveType.Int) ||
                      newType.IsSameTypeAs(PrimitiveType.Float)))
                {
                    throw handler.IllegalTypeInCoerceExpr(context);
                }

                if (oldType.IsSameTypeAs(PrimitiveType.Int))
                {
                    if (newType.IsSameTypeAs(PrimitiveType.Int))
                    {
                        return subExpr;
                    }

                    if (newType.IsSameTypeAs(PrimitiveType.Float) || PLanguageType.TypeIsOfKind(newType, TypeKind.Enum))
                    {
                        return new CoerceExpr(context, subExpr, newType);
                    }
                }
                else if (oldType.IsSameTypeAs(PrimitiveType.Float))
                {
                    if (newType.IsSameTypeAs(PrimitiveType.Float))
                    {
                        return subExpr;
                    }

                    if (newType.IsSameTypeAs(PrimitiveType.Int))
                    {
                        return new CoerceExpr(context, subExpr, newType);
                    }
                }
                else if (PLanguageType.TypeIsOfKind(oldType, TypeKind.Enum))
                {
                    // TODO: are enum-to-enum coersions allowed?
                    if (newType.IsSameTypeAs(oldType))
                    {
                        return subExpr;
                    }

                    if (newType.IsSameTypeAs(PrimitiveType.Int))
                    {
                        return new CoerceExpr(context, subExpr, newType);
                    }
                }
                else if (oldType.IsSameTypeAs(PrimitiveType.Machine) || oldType is PermissionType)
                {
                    if (newType.IsSameTypeAs(oldType))
                    {
                        return subExpr;
                    }

                    if (newType is PermissionType)
                    {
                        if (oldType.IsSameTypeAs(PrimitiveType.Machine))
                        {
                            return new CoerceExpr(context, subExpr, newType);
                        }

                        if (oldType is PermissionType)
                        {
                            if (newType.AllowedPermissions.Value.Any(x => !oldType.AllowedPermissions.Value.Contains(x))
                               )
                            {
                                throw handler.IllegalInterfaceCoerce(context, oldType, newType);
                            }

                            return new CoerceExpr(context, subExpr, newType);
                        }
                    }
                }

                throw handler.IncomparableTypes(context, oldType, newType);
            }

            throw handler.InternalError(context, new ArgumentOutOfRangeException(nameof(context), "invalid cast"));
        }

        public override IPExpr VisitPrimitive(PParser.PrimitiveContext context)
        {
            if (context.iden() != null)
            {
                var symbolName = context.iden().GetText();
                if (table.Lookup(symbolName, out Variable variable))
                {
                    return new VariableAccessExpr(context, variable);
                }

                if (table.Lookup(symbolName, out EnumElem enumElem))
                {
                    return new EnumElemRefExpr(context, enumElem);
                }

                if (table.Lookup(symbolName, out PEvent evt))
                {
                    return new EventRefExpr(context, evt);
                }

                throw handler.MissingDeclaration(context.iden(), "variable, enum element, or event", symbolName);
            }

            if (context.floatLiteral() != null)
            {
                return Visit(context.floatLiteral());
            }

            if (context.BoolLiteral() != null)
            {
                return new BoolLiteralExpr(context, context.BoolLiteral().GetText().Equals("true"));
            }

            if (context.IntLiteral() != null)
            {
                return new IntLiteralExpr(context, int.Parse(context.IntLiteral().GetText()));
            }

            if (context.NullLiteral() != null)
            {
                return new NullLiteralExpr(context);
            }

            if (context.NONDET() != null)
            {
                if (method.Owner?.IsSpec == true)
                {
                    throw handler.IllegalMonitorOperation(context, context.NONDET().Symbol, method.Owner);
                }

                method.IsNondeterministic = true;
                return new NondetExpr(context);
            }

            if (context.FAIRNONDET() != null)
            {
                if (method.Owner?.IsSpec == true)
                {
                    throw handler.IllegalMonitorOperation(context, context.FAIRNONDET().Symbol, method.Owner);
                }

                method.IsNondeterministic = true;
                return new FairNondetExpr(context);
            }

            if (context.HALT() != null)
            {
                var success = table.Lookup("halt", out PEvent haltEvent);
                Debug.Assert(success);
                return new EventRefExpr(context, haltEvent);
            }

            if (context.THIS() != null)
            {
                if (method.Owner == null)
                {
                    throw handler.MisplacedThis(context);
                }

                if (method.Owner.IsSpec)
                {
                    throw handler.IllegalMonitorOperation(context, context.THIS().Symbol, method.Owner);
                }

                return new ThisRefExpr(context, method.Owner);
            }

            throw handler.InternalError(context, new ArgumentOutOfRangeException(nameof(context), "unknown primitive"));
        }

        public override IPExpr VisitUnnamedTupleBody(PParser.UnnamedTupleBodyContext context)
        {
            var fields = context._fields.Select(Visit).ToArray();
            return new UnnamedTupleExpr(context, fields);
        }

        public override IPExpr VisitNamedTupleBody(PParser.NamedTupleBodyContext context)
        {
            var fields = context._values.Select(Visit).ToArray();

            var entries = new NamedTupleEntry[fields.Length];
            var names = new HashSet<string>();
            for (var i = 0; i < fields.Length; i++)
            {
                var entryName = context._names[i].GetText();
                if (names.Contains(entryName))
                {
                    throw handler.DuplicateNamedTupleEntry(context._names[i], entryName);
                }

                names.Add(entryName);
                entries[i] = new NamedTupleEntry(name: entryName, fieldNo: i, type: fields[i].Type);
            }

            var type = new NamedTupleType(entries);
            return new NamedTupleExpr(context, fields, type);
        }

        public override IPExpr VisitExpFloat(PParser.ExpFloatContext context)
        {
            throw new NotImplementedException("float(x,y) syntax");
        }

        public override IPExpr VisitDecimalFloat(PParser.DecimalFloatContext context)
        {
            var value = double.Parse($"{context.pre?.Text ?? ""}.{context.post.Text}");
            return new FloatLiteralExpr(context, value);
        }

        public override IPExpr VisitRvalue(PParser.RvalueContext context)
        {
            return Visit(context.expr());
        }

        public override IPExpr VisitFormatedString([NotNull] PParser.FormatedStringContext context)
        {
            var baseString = context.StringLiteral().GetText();
            baseString = baseString.Substring(1, baseString.Length - 2); // strip beginning / end double quote
            var numNecessaryArgs = TypeCheckingUtils.PrintStmtNumArgs(baseString);
            if (numNecessaryArgs == -1)
            {
                throw handler.InvalidStringExprFormat(context, context.StringLiteral().Symbol);
            }

            var args = TypeCheckingUtils.VisitRvalueList(context.rvalueList(), this).ToList();

            if (args.Count != numNecessaryArgs)
            {
                throw handler.IncorrectArgumentCount(context, args.Count, numNecessaryArgs);
            }

            return new StringExpr(context, baseString, args);
        }

        public override IPExpr VisitStringExpr(PParser.StringExprContext context)
        {
            return VisitFormatedString(context.formatedString());
        }

        public override IPExpr VisitVarLvalue(PParser.VarLvalueContext context)
        {
            var varName = context.name.GetText();
            if (!table.Lookup(varName, out Variable variable))
            {
                throw handler.MissingDeclaration(context, "variable", varName);
            }

            return new VariableAccessExpr(context, variable);
        }

        public override IPExpr VisitNamedTupleLvalue(PParser.NamedTupleLvalueContext context)
        {
            var lvalue = Visit(context.lvalue());
            if (!(lvalue.Type.Canonicalize() is NamedTupleType type))
            {
                throw handler.TypeMismatch(lvalue, TypeKind.NamedTuple);
            }

            var field = context.field.GetText();
            if (!type.LookupEntry(field, out var entry))
            {
                throw handler.MissingNamedTupleEntry(context.field, type);
            }

            return new NamedTupleAccessExpr(context, lvalue, entry);
        }

        public override IPExpr VisitTupleLvalue(PParser.TupleLvalueContext context)
        {
            var lvalue = Visit(context.lvalue());
            if (!(lvalue.Type.Canonicalize() is TupleType type))
            {
                throw handler.TypeMismatch(lvalue, TypeKind.Tuple);
            }

            var field = int.Parse(context.@int().GetText());
            if (field >= type.Types.Count)
            {
                throw handler.OutOfBoundsTupleAccess(context.@int(), type);
            }

            return new TupleAccessExpr(context, lvalue, field, type.Types[field]);
        }

        public override IPExpr VisitMapOrSeqLvalue(PParser.MapOrSeqLvalueContext context)
        {
            var lvalue = Visit(context.lvalue());
            var index = Visit(context.expr());
            var indexType = index.Type;
            switch (lvalue.Type.Canonicalize())
            {
                case MapType mapType:
                    if (!mapType.KeyType.IsAssignableFrom(indexType))
                    {
                        throw handler.TypeMismatch(context.expr(), indexType, mapType.KeyType);
                    }

                    return new MapAccessExpr(context, lvalue, index, mapType.ValueType);

                case SequenceType seqType:
                    if (!PrimitiveType.Int.IsAssignableFrom(indexType))
                    {
                        throw handler.TypeMismatch(context.expr(), indexType, PrimitiveType.Int);
                    }

                    return new SeqAccessExpr(context, lvalue, index, seqType.ElementType);

                default:
                    throw handler.TypeMismatch(lvalue, TypeKind.Sequence, TypeKind.Map);
            }
        }
    }
}