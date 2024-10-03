using System;
using System.Collections.Generic;
using System.Linq;
using Plang.Compiler.Backend.ASTExt;
using Plang.Compiler.TypeChecker.AST;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.TypeChecker.AST.Expressions;
using Plang.Compiler.TypeChecker.AST.Statements;
using Plang.Compiler.TypeChecker.AST.States;
using Plang.Compiler.TypeChecker.Types;

namespace Plang.Compiler.Backend.Java {

    internal class MachineGenerator : JavaSourceGenerator
    {

        private Machine _currentMachine; // Some generated code is machine-dependent, so stash the current machine here.
        private HashSet<Function> _calledStaticFunctions = new HashSet<Function>(); // static functions allowed
        private bool debug = false;

        internal MachineGenerator(ICompilerConfiguration job, string filename) : base(job, filename)
        {
            debug = job.Debug;
        }

        /// <summary>
        /// Generates Java code for a given compilation job's machine and monitor definitions.
        ///
        /// Currently, we should be able to use nested classes to put everything we need in a single
        /// Java file, in a manner similar to how the C# extractor uses namespaces.
        /// </summary>
        /// <param name="job"></param>
        /// <param name="scope"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        protected override void GenerateCodeImpl()
        {
            WriteLine($"public class {Constants.MachineNamespaceName} {{");
            WriteLine($"private static Logger logger = Logger.getLogger({Constants.MachineNamespaceName}.class.getName());");
            if (debug) {
                WriteLine($"static {{ logger.setLevel(Level.ALL); }};");
            } else {
                WriteLine($"static {{ logger.setLevel(Level.OFF); }};");
            }

            foreach (var m in GlobalScope.Machines)
            {
                _currentMachine = m;
                if (m.IsSpec)
                {
                    WriteMonitorDecl();
                }
                else
                {
                    WriteMachineDecl();
                }
                _currentMachine = null;
            }

            // static functions
            foreach (var f in _calledStaticFunctions)
            {
                WriteFunction(f);
            }

            WriteLine("}");
        }


        private void WriteMachineDecl()
        {
            WriteLine($"// PMachine {_currentMachine.Name} elided ");
        }

        private void WriteMonitorDecl()
        {
            var cname = Names.GetNameForDecl(_currentMachine);

            WriteLine($"public static class {cname} extends prt.Monitor<{cname}.{Constants.StateEnumName}> implements Serializable {{");

            WriteLine();
            WriteSupplierCDef(cname);
            WriteLine();

            // monitor fields
            foreach (var field in _currentMachine.Fields)
            {
                var type = Types.JavaTypeFor(field.Type);
                var name = Names.GetNameForDecl(field);

                WriteLine($"private {type.TypeName} {name} = {type.DefaultValue};");
                WriteLine($"public {type.TypeName} get_{name}() {{ return this.{name}; }};");
                WriteLine();
            }
            WriteLine();

            // state identifiers
            WriteLine($"public enum {Constants.StateEnumName} {{");
            foreach (var (state, sep) in _currentMachine.States.WithPostfixSep(","))
            {
                var name = Names.GetNameForDecl(state);
                WriteLine($"{name}{sep}");
            }
            WriteLine("}");
            WriteLine();

            // constructor
            WriteMonitorCstr();
            WriteLine();

            // .getEventTypes()
            WriteEventsAccessor();
            WriteLine();

            // functions
            foreach (var f in _currentMachine.Methods)
            {
                WriteFunction(f);
            }
            WriteLine();

            WriteLine($"}} // {cname} monitor definition");
        }

        private void WriteSupplierCDef(string cname)
        {
            WriteLine($"public static class Supplier implements java.util.function.Supplier<{cname}>, Serializable {{");
            WriteLine($"public {cname} get() {{");
            WriteLine($"{cname} ret = new {cname}();");
            WriteLine("ret.ready();"); // TODO: at this point, we may as well ready() within the monitor's constructor?
            WriteLine("return ret;");
            WriteLine("}");
            WriteLine("}");
        }


        private void WriteFunction(Function f)
        {
            if (f.IsForeign)
            {
                return;
            }

            if (f.CanReceive)
            {
                WriteLine($"// Async function {f.Name} elided");
                return;
            }

            WriteFunctionSignature(f); WriteLine(" {");

            if (f.LocalVariables.Any())
            {
                foreach (var decl in f.LocalVariables)
                {
                    var t = Types.JavaTypeFor(decl.Type);

                    if (decl.Role.HasFlag(VariableRole.Temp))
                    {
                        /* Temporary values are only emitted as part of the frontend's SSA algorithm, and therefore
                         * will never be read but only overwritten.  There's no need to initialise this variable,
                         * especially if it's a reference type (resulting in a GC allocation that will be thrown
                         * away immediately). */
                        WriteLine($"{t.TypeName} {Names.GetNameForDecl(decl)};");
                    }
                    else
                    {
                        WriteLine($"{t.TypeName} {Names.GetNameForDecl(decl)} = {t.DefaultValue};");
                    }
                }
                WriteLine();
            }

            foreach (var stmt in f.Body.Statements)
            {
                WriteStmt(stmt);
            }

            WriteLine("}");
        }

        private void WriteFunctionSignature(Function f)
        {
            var fname = Names.GetNameForDecl(f);

            Write("private ");

            var isStatic = f.Owner == null;
            if (isStatic)
            {
                Write("static ");
            }


            string args;
            if (f.IsAnon)
            {
                if (f.Signature.Parameters.Count == 0)
                {
                    args = "";
                }
                else if (f.Signature.Parameters.Count == 1)
                {
                    var t = Types.JavaTypeFor(f.Signature.ParameterTypes.First());
                    var argname = Names.GetNameForDecl(f.Signature.Parameters.First());
                    args = $"{t.TypeName} {argname}";
                }
                else
                {
                    var file = f.SourceLocation.start.TokenSource.SourceName;
                    var line = f.SourceLocation.start.Line;
                    throw new Exception(
                        $"Function beginning at {file}:{line} has unexpected number {f.Signature.Parameters.Count} of arguments");
                }
            }
            else
            {
                args = string.Join(
                    ",",
                    f.Signature.Parameters.Select(v =>
                        $"{Types.JavaTypeFor(v.Type).TypeName} {Names.GetNameForDecl(v)}"));
            }

            var retType = Types.JavaTypeFor(f.Signature.ReturnType);

            Write($"{retType.TypeName} {fname}({args})");

            // If this function has exceptional control flow (for raising events or state transition)
            // we need to annotate it appropriately.
            var throwables = new List<string>();
            if (f.CanChangeState)
            {
                throwables.Add("prt.exceptions.TransitionException");
            }
            if (f.CanRaiseEvent)
            {
                throwables.Add("prt.exceptions.RaiseEventException");
            }
            if (throwables.Count > 0)
            {
                Write($" throws {string.Join(", ", throwables)}");
            }

        }

        private void WriteMonitorCstr()
        {
            var cname = Names.GetNameForDecl(_currentMachine);

            WriteLine($"public {cname}() {{");
            WriteLine("super();");

            foreach (var s in _currentMachine.States)
            {
                WriteStateBuilderDecl(s, true);
            }
            WriteLine("} // constructor");
            WriteLine();

            WriteLine($"public void reInitializeMonitor() {{");
            
            foreach (var s in _currentMachine.States)
            {
                WriteStateBuilderDecl(s, false);
            }
            WriteLine("}");

        }

        private void WriteEventsAccessor()
        {
            TypeManager.JType eventType = new TypeManager.JType.JEvent();
            WriteLine($"public java.util.List<Class<? extends {eventType.TypeName}>> getEventTypes() {{");
            Write("return java.util.Arrays.asList(");
            foreach (var (sep, ev) in _currentMachine.Observes.Events.WithPrefixSep(", "))
            {
                var name = Names.GetNameForDecl(ev);
                Write($"{sep}{Constants.EventNamespaceName}.{name}.class");
            }
            WriteLine(");");
            WriteLine("}");
        }

        private void WriteStateBuilderDecl(State s, bool isConstructor)
        {
            if (isConstructor) {
                WriteLine($"addState(prt.State.keyedOn({Names.IdentForState(s)})");
            } else {
                WriteLine($"registerState(prt.State.keyedOn({Names.IdentForState(s)})");
            }
            
            if (s.IsStart)
            {
                WriteLine($".isInitialState(true)");
            }

            if (s.Entry != null)
            {
                WriteStateBuilderEntryHandler(s.Entry);
            }
            foreach (var (e, a) in s.AllEventHandlers)
            {
                WriteStateBuilderEventHandler(e, a);
            }
            if (s.Exit != null)
            {
                WriteStateBuilderExitHandler(s.Exit);
            }

            WriteLine(".build());");
        }

        private void WriteStateBuilderEntryHandler(Function f)
        {
            var fname = Names.GetNameForDecl(f);
            WriteLine($".withEntry(this::{fname})");
        }

        private void WriteStateBuilderEventHandler(PEvent e, IStateAction a)
        {
            var ename = $"{Constants.EventNamespaceName}.{Names.GetNameForDecl(e)}";

            switch (a)
            {
                case EventDefer _:
                    WriteLine($"// Ignoring deferred event {ename}");
                    break;
                case EventDoAction da when da.Target.Signature.Parameters.Count == 0:
                {
                    var aname = Names.GetNameForDecl(da.Target);
                    WriteLine($".withEvent({ename}.class, __ -> {aname}())");
                    break;
                }
                case EventDoAction da when da.Target.Signature.Parameters.Count > 0:
                {
                    var aname = Names.GetNameForDecl(da.Target);
                    WriteLine($".withEvent({ename}.class, this::{aname})");
                    break;
                }
                case EventGotoState { TransitionFunction: null } gs:
                {
                    var sname = Names.IdentForState(gs.Target);
                    WriteLine($".withEvent({ename}.class, __ -> gotoState({sname}))");
                    break;
                }
                case EventGotoState { TransitionFunction: { } } gs:
                {
                    var sname = Names.IdentForState(gs.Target);
                    var tname = Names.GetNameForDecl(gs.TransitionFunction);
                    var argcount = gs.TransitionFunction.Signature.ParameterTypes.Count();

                    switch (argcount)
                    {
                        case 0:
                            WriteLine($".withEvent({ename}.class, __ -> {{ {tname}(); gotoState({sname}); }})");
                            break;
                        case 1:
                            WriteLine($".withEvent({ename}.class, p -> {{ {tname}(p); gotoState({sname}); }})");
                            break;
                        default:
                            throw new Exception($"Unexpected {argcount}-arity for event handler for {ename}");
                    }
                    break;
                }
                case EventIgnore _:
                    WriteLine($".withEvent({ename}.class, __ -> {{ ; }})");
                    break;
                default:
                    throw new NotImplementedException($"TODO: {a.GetType()} not implemented.");
            }
        }

        private void WriteStateBuilderExitHandler(Function f)
        {
            var fname = Names.GetNameForDecl(f);
            WriteLine($".withExit(this::{fname})");
        }

        private void WriteStmt(IPStmt stmt)
        {
            TypeManager.JType t;

            switch (stmt)
            {
                case AddStmt addStmt:
                    t = Types.JavaTypeFor(addStmt.Variable.Type);
                    WriteExpr(addStmt.Variable);
                    Write($".{t.MutatorMethodName}(");
                    WriteExpr(addStmt.Value);
                    WriteLine(");");
                    break;

                case AssertStmt assertStmt:
                    Write($"{Constants.TryAssertMethodName}(");
                    WriteExpr(assertStmt.Assertion);
                    Write(", ");
                    WriteExpr(assertStmt.Message);
                    WriteLine(");");
                    break;

                case AssignStmt assignStmt:
                    WriteAssignStatement(assignStmt);
                    break;

                case BreakStmt _:
                    WriteLine("break;");
                    break;

                case CompoundStmt compoundStmt:
                    WriteLine("{");
                    foreach (var s in compoundStmt.Statements)
                    {
                        WriteStmt(s);
                    }
                    WriteLine("}");
                    break;

                case ContinueStmt _:
                    WriteLine("continue;");
                    break;

                case CtorStmt _:
                    goto default;

                case FunCallStmt funCallStmt:
                    WriteFunctionCallExpr(funCallStmt.Function, funCallStmt.ArgsList);
                    WriteLine(";");
                    break;

                case GotoStmt gotoStmt:
                    Write($"gotoState({Names.IdentForState(gotoStmt.State)}");
                    if (gotoStmt.Payload != null)
                    {
                        Write(", ");
                        WriteExpr(gotoStmt.Payload);
                    }
                    WriteLine(");");
                    WriteLine("return;");
                    break;

                case IfStmt ifStmt:
                    Write("if (");
                    WriteExpr(ifStmt.Condition);
                    Write(") ");

                    if (ifStmt.ThenBranch.Statements.Count == 0)
                    {
                        Write("{}");
                    }
                    else
                    {
                        WriteStmt(ifStmt.ThenBranch);
                    }

                    if (ifStmt.ElseBranch != null && ifStmt.ElseBranch.Statements.Count > 0)
                    {
                        WriteLine(" else ");
                        WriteStmt(ifStmt.ElseBranch);
                    }
                    break;


                case InsertStmt insertStmt:
                    t = Types.JavaTypeFor(insertStmt.Variable.Type);
                    WriteExpr(insertStmt.Variable);
                    if (PLanguageType.TypeIsOfKind(insertStmt.Variable.Type, TypeKind.Sequence))
                    {
                        Write($".{t.InsertMethodName}((int)(");
                    }
                    else
                    {
                        Write($".{t.InsertMethodName}((");
                    }
                    WriteExpr(insertStmt.Index);
                    Write("), ");
                    WriteExpr(insertStmt.Value);
                    WriteLine(");");
                    break;

                case MoveAssignStmt moveAssignStmt:
                    WriteMoveAssignStatement(moveAssignStmt);
                    break;

                case NoStmt _:
                    break;

                case PrintStmt printStmt:
                    Write("logger.info(");
                    WriteExpr(printStmt.Message);
                    WriteLine(");");
                    break;

                case RaiseStmt raiseStmt:
                    Write($"{Constants.TryRaiseEventMethodName}(new ");
                    WriteExpr(raiseStmt.PEvent);
                    Write("(");
                    foreach (var (sep, expr) in raiseStmt.Payload.WithPrefixSep(", "))
                    {
                        Write(sep);
                        WriteExpr(expr);
                    }
                    Write(")");
                    WriteLine(");");
                    break;

                case ReceiveStmt _:
                    goto default;

                case RemoveStmt removeStmt:
                    t = Types.JavaTypeFor(removeStmt.Variable.Type);
                    WriteExpr(removeStmt.Variable);
                    Write($".{t.RemoveMethodName}(");
                    WriteExpr(removeStmt.Value);
                    WriteLine(");");
                    break;

                case ReturnStmt returnStmt:
                    Write("return ");
                    if (returnStmt.ReturnValue != null)
                    {
                        WriteExpr(returnStmt.ReturnValue);
                    }
                    WriteLine(";");
                    break;

                case SendStmt _:
                    goto default;

                case ForeachStmt foreachStmt:
                {
                    var varname = Names.GetNameForDecl(foreachStmt.Item);
                    t = Types.JavaTypeFor(foreachStmt.Item.Type);

                    Write($"for ({t.TypeName} {varname} : ");
                    WriteExpr(foreachStmt.IterCollection);
                    Write(") ");
                    WriteStmt(foreachStmt.Body);
                    break;
                }
                case WhileStmt whileStmt:
                    Write("while (");
                    WriteExpr(whileStmt.Condition);
                    Write(") ");
                    WriteStmt(whileStmt.Body);
                    break;

                case AnnounceStmt _:
                    goto default;

                default:
                    WriteLine($"// TODO: {stmt}");
                    return;
                //throw new NotImplementedException(stmt.GetType().ToString());
            }
        }

        private void WriteAssignStatement(AssignStmt assignStmt)
        {
            var lval = assignStmt.Location;
            var t = Types.JavaTypeForVarLocation(lval);

            var rval = assignStmt.Value;

            // In the case where the types of each side of the assignment differ due to subtyping, we need to ensure
            // that we cast the given rval to the lval's type.  For example, if we are assigning a `seq[int]` to a
            // `seq[any]`, then we need to first downcast the `ArrayList<Long>` to an Object, and then _re-upcast_ it
            // to an `ArrayList<Object>` (due to collection invariance in Java - slightly unfortunate here).
            // TODO: arguably, this should be done across all backends in the StatementVisitor?
            if (!lval.Type.IsSameTypeAs(rval.Type))
            {
                rval = new CastExpr(assignStmt.SourceLocation, rval, PrimitiveType.Any);
                if (!lval.Type.IsSameTypeAs(PrimitiveType.Any))
                {
                    rval = new CastExpr(assignStmt.SourceLocation, rval, lval.Type);
                }
            }

            switch (lval)
            {
                case MapAccessExpr mapAccessExpr:
                    WriteExpr(mapAccessExpr.MapExpr);
                    Write($".{t.MutatorMethodName}(");
                    WriteExpr(mapAccessExpr.IndexExpr);
                    Write(",");
                    WriteExpr(rval);
                    WriteLine(");");
                    break;

                case NamedTupleAccessExpr namedTupleAccessExpr:
                    WriteExpr(namedTupleAccessExpr.SubExpr);
                    Write(".");
                    Write(namedTupleAccessExpr.FieldName);
                    Write(" = ");
                    WriteExpr(rval);
                    WriteLine(";");
                    break;

                case SeqAccessExpr seqAccessExpr:
                    WriteExpr(seqAccessExpr.SeqExpr);
                    Write($".{t.MutatorMethodName}(");
                    WriteExpr(seqAccessExpr.IndexExpr);
                    Write(",");
                    WriteExpr(rval);
                    WriteLine(");");
                    break;

                case TupleAccessExpr tupleAccessExpr:
                    WriteExpr(tupleAccessExpr.SubExpr);
                    Write($".{Constants.UnnamedTupleFieldPrefix + tupleAccessExpr.FieldNo}");
                    Write(" = ");
                    WriteExpr(rval);
                    WriteLine(";");
                    break;

                case VariableAccessExpr variableAccessExpr:
                    Write(Names.GetNameForDecl(variableAccessExpr.Variable));
                    Write(" = ");
                    WriteExpr(rval);
                    WriteLine(";");
                    break;
            }
        }

        private void WriteMoveAssignStatement(MoveAssignStmt moveAssignStmt)
        {
            if (!moveAssignStmt.FromVariable.Type.IsSameTypeAs(moveAssignStmt.ToLocation.Type))
            {
                throw new NotImplementedException("Typecasting in MoveAssignStmt is not yet implemented.");
            }
            var lval = moveAssignStmt.ToLocation;
            IPExpr rval = new VariableAccessExpr(moveAssignStmt.SourceLocation, moveAssignStmt.FromVariable);

            var assignStmt = new AssignStmt(moveAssignStmt.SourceLocation, lval, rval);
            WriteAssignStatement(assignStmt);
        }

        private void WriteFunctionCallExpr(Function f, IEnumerable<IPExpr> args)
        {
            var isStatic = f.Owner == null;
            if (isStatic && !f.IsForeign)
            {
                _calledStaticFunctions.Add(f);
            }

            var fname = Names.GetNameForDecl(f);
            if (f.IsForeign)
            {
                var ffiBridge = Names.FFIBridgeForMachine(
                    isStatic
                        ? Constants.FFIGlobalScopeCname
                        : $"{_currentMachine.Name}{Constants.FFILocalScopeSuffix}");
                Write($"{ffiBridge}.{fname}(");

                // All foreign functions have an implicit first argument to the current machine
                args = args.Prepend(new ThisRefExpr(f.SourceLocation, _currentMachine));
            }
            else
            {
                Write($"{fname}(");
            }

            foreach (var (sep, expr) in args.WithPrefixSep(", "))
            {
                Write(sep);
                WriteExpr(expr);
            }
            Write($")");
        }

        private void WriteExpr(IPExpr expr)
        {
            TypeManager.JType t;

            switch (expr)
            {
                case BinOpExpr binOpExpr:
                    WriteBinOp(binOpExpr.Lhs, binOpExpr.Operation, binOpExpr.Rhs);
                    break;
                case BoolLiteralExpr ble:
                    Write($"({TypeManager.JType.JBool.ToJavaLiteral(ble.Value)})");
                    break;
                case CastExpr ce:
                {
                    t = Types.JavaTypeFor(ce.Type);
                    Write($"(");
                    Write($"({t.TypeName})");
                    WriteExpr(ce.SubExpr);
                    Write($")");
                    break;
                }
                case ChooseExpr _:
                    goto default; //TODO
                case CloneExpr ce:
                    t = Types.JavaTypeFor(ce.Type);
                    WriteClone(t, () => WriteExpr(ce.Term));
                    break;
                case CoerceExpr _:
                    goto default; //TODO
                case ContainsExpr ce:
                {
                    t = Types.JavaTypeFor(ce.Collection.Type);
                    WriteExpr(ce.Collection);
                    Write($".{t.ContainsMethodName}(");
                    WriteExpr(ce.Item);
                    Write(")");
                    break;
                }
                case CtorExpr _:
                    goto default;
                case DefaultExpr de:
                    t = Types.JavaTypeFor(de.Type);
                    Write(t.DefaultValue);
                    break;
                case EnumElemRefExpr ee:
                    var typeName = ee.Value.ParentEnum.Name;
                    var valueName = ee.Value.Name;
                    Write($"{Constants.TypesNamespaceName}.{typeName}.{valueName}");
                    break;
                case EventRefExpr _:
                    goto default; //TODO
                case FairNondetExpr _:
                    goto default;
                case FloatLiteralExpr fe:
                    Write(TypeManager.JType.JFloat.ToJavaLiteral(fe.Value));
                    break;
                case FunCallExpr fe:
                    WriteFunctionCallExpr(fe.Function, fe.Arguments);
                    break;
                case IntLiteralExpr ie:
                    Write(TypeManager.JType.JInt.ToJavaLiteral(ie.Value));
                    break;
                case KeysExpr ke:
                {
                    t = Types.JavaTypeFor(ke.Expr.Type);
                    if (!(t is TypeManager.JType.JMap mt))
                    {
                        throw new Exception($"Got an unexpected {t.TypeName} rather than a Map");
                    }

                    // Note: P key sets are in fact sequences, so get the j.l.Set() and then
                    // construct a new List from that collection.
                    Write($"new {mt.KeyCollectionType}(");
                    WriteExpr(ke.Expr);
                    Write($".{mt.KeysMethodName}()");
                    Write(")");
                    break;
                }

                case NamedTupleExpr te:
                {
                    t = Types.JavaTypeFor(te.Type);
                    Write($"new {t.TypeName}(");
                    foreach (var (sep, field) in te.TupleFields.WithPrefixSep(", "))
                    {
                        Write(sep);
                        WriteExpr(field);
                    }
                    Write(")");
                    break;
                }
                case NondetExpr _:
                    goto default; // TODO
                case NullLiteralExpr _:
                    Write("null");
                    break;
                case SizeofExpr se:
                    WriteExpr(se.Expr);
                    Write(".size()");
                    break;
                case StringExpr se:
                {
                    var fmtLit = TypeManager.JType.JString.ToJavaLiteral(se.BaseString);
                    if (se.Args.Count == 0)
                    {
                        Write(fmtLit);
                    }
                    else
                    {
                        Write($"java.text.MessageFormat.format({fmtLit}");
                        foreach (var arg in se.Args)
                        {
                            Write(", ");
                            WriteExpr(arg);
                        }

                        Write(")");
                    }

                    break;
                }
                case ThisRefExpr _:
                    Write("this");
                    break;
                case UnaryOpExpr ue:
                    switch (ue.Operation)
                    {
                        case UnaryOpType.Negate:
                            Write("-");
                            break;
                        case UnaryOpType.Not:
                            Write("!");
                            break;

                    }
                    Write("(");
                    WriteExpr(ue.SubExpr);
                    Write(")");
                    break;
                case UnnamedTupleExpr te:
                {
                    t = Types.JavaTypeFor(te.Type);
                    Write($"new {t.TypeName}(");
                    foreach (var (sep, field) in te.TupleFields.WithPrefixSep(", "))
                    {
                        Write(sep);
                        WriteExpr(field);
                    }
                    Write(")");
                    break;
                }
                case ValuesExpr ve:
                {
                    t = Types.JavaTypeFor(ve.Expr.Type);
                    if (!(t is TypeManager.JType.JMap mt))
                    {
                        throw new Exception($"Got an unexpected {t.TypeName} rather than a Map");
                    }

                    // Note: P key sets are in fact sequences, so get the j.l.Set() and then
                    // construct a new List from that collection.
                    Write($"new {mt.ValueCollectionType}(");
                    WriteExpr(ve.Expr);
                    Write($".{mt.KeysMethodName}()");
                    Write(")");
                    break;
                }

                case VariableAccessExpr variableAccessExpr:
                    Write(Names.GetNameForDecl(variableAccessExpr.Variable));
                    break;

                case MapAccessExpr _:
                case NamedTupleAccessExpr _:
                case SetAccessExpr _:
                case SeqAccessExpr _:
                case TupleAccessExpr _:
                    WriteStructureAccess(expr);
                    break;

                default:
                    throw new NotImplementedException(expr.ToString());
            }
        }

        private void WriteBinOp(IPExpr left, BinOpType op, IPExpr right)
        {
            void WritePrim()
            {
                WriteExpr(left); Write($" {op.JavaPrimitiveBinOp()} "); WriteExpr(right);
            }

            var lhsType = Types.JavaTypeFor(left.Type);
            var rhsType = Types.JavaTypeFor(right.Type);

            switch (op.GetKind())
            {
                // Numeric and boolean operations are straightforward, since `left` and `right` will either be
                // primitive types or boxed primitive types (in which case we let auto-unboxing do its thing)
                case BinOpKind.Boolean:
                case BinOpKind.Numeric:
                    WritePrim();
                    break;

                // Comparison and equality operators are less straightforward, because we have to emit different
                // Java code depending on whether they are primitive types (i.e. "left == right") versus reference
                // types (i.e. "left.equals(right) == true").

                // So long as both sides are primitive types, this is easy, just like the above case.
                case BinOpKind.Comparison when lhsType.IsPrimitive && rhsType.IsPrimitive:
                case BinOpKind.Equality when lhsType.IsPrimitive && rhsType.IsPrimitive:
                    WritePrim();
                    break;

                // For reference types, defer to Values.compare() or Values.deepEqual() , which
                // will runtime-dispatch on the arguments' classes.
                case BinOpKind.Comparison:
                    Write("(");

                    Write($"{Constants.PrtCompareMethodName}(");
                    WriteExpr(left);
                    Write(", ");
                    WriteExpr(right);
                    Write(")");

                    Write($" {op.JavaPrimitiveBinOp()} 0");
                    Write(")");
                    break;

                case BinOpKind.Equality:
                    Write("(");

                    Write($"{Constants.PrtDeepEqualsMethodName}(");
                    WriteExpr(left);
                    Write(", ");
                    WriteExpr(right);
                    Write(")");

                    Write($" {op.JavaPrimitiveBinOp()} true");
                    Write(")");
                    break;
            }
        }

        private void WriteStructureAccess(IPExpr e)
        {
            var t = Types.JavaTypeForVarLocation(e);

            // We have to explicitly cast accesses to collections since we might be upcasting (say,
            // if we're extracting an int out of a tuple (List<Object>).).  Use the reference
            // type name to ensure we're casting to another Object (and let Java handle auto-unboxing
            // if it can.)
            //
            // Note: P collections are covariant (i.e. seq[int] extends seq[any]).  This means we'll
            // throw if we downcast and then try to assign a different type into it.  Confirm that
            // this is okay (or at least undefined in the language spec).
            // Write($"({Types.JavaTypeFor(e.Type).ReferenceTypeName})");

            switch (e) {
                case MapAccessExpr mapAccessExpr:
                    WriteExpr(mapAccessExpr.MapExpr);
                    Write($".{t.AccessorMethodName}(");
                    WriteExpr(mapAccessExpr.IndexExpr);
                    Write(")");
                    break;

                case NamedTupleAccessExpr namedTupleAccessExpr:
                    WriteExpr(namedTupleAccessExpr.SubExpr);
                    Write($".{namedTupleAccessExpr.FieldName}");
                    break;

                case SetAccessExpr setAccessExpr:
                    Write($"{Constants.PrtSetElementAtMethodName}(");
                    WriteExpr(setAccessExpr.SetExpr);
                    Write(", ");
                    WriteExpr(setAccessExpr.IndexExpr);
                    Write(")");
                    break;

                case SeqAccessExpr seqAccessExpr:
                    WriteExpr(seqAccessExpr.SeqExpr);
                    // IndexExpr is a JInt and thus emitted as a long. In this particular case,
                    // though, `ArrayList#get()` takes an int so we have to downcast.
                    Write($".{t.AccessorMethodName}((int)(");
                    WriteExpr(seqAccessExpr.IndexExpr);
                    Write("))");
                    break;

                case TupleAccessExpr tupleAccessExpr:
                    WriteExpr(tupleAccessExpr.SubExpr);
                    Write($".{Constants.UnnamedTupleFieldPrefix + tupleAccessExpr.FieldNo}");
                    break;

            }
        }

    }
}