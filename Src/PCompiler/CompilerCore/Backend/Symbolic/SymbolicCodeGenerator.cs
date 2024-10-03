using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Plang.Compiler.Backend.ASTExt;
using Plang.Compiler.TypeChecker;
using Plang.Compiler.TypeChecker.AST;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.TypeChecker.AST.Expressions;
using Plang.Compiler.TypeChecker.AST.Statements;
using Plang.Compiler.TypeChecker.AST.States;
using Plang.Compiler.TypeChecker.Types;

namespace Plang.Compiler.Backend.Symbolic
{
    class SymbolicCodeGenerator : ICodeGenerator
    {
        public IEnumerable<CompiledFile> GenerateCode(ICompilerConfiguration job, Scope globalScope)
        {
            var context = new CompilationContext(job);
            var javaSource = GenerateSource(context, globalScope);
            return new List<CompiledFile> { javaSource };
        }

        /// <summary>
        /// This compiler has a compilation stage.
        /// </summary>
        public bool HasCompilationStage => true;

        public void Compile(ICompilerConfiguration job)
        {
            var pomPath = Path.Combine(job.OutputDirectory.FullName, "pom.xml");
            var stdout = "";
            var stderr = "";

            // create the pom.xml file
            var pomTemplate = Constants.pomTemplate;
            pomTemplate = pomTemplate.Replace("-project-name-",job.ProjectName);

            string foreignInclude = "";
            var foreignFiles = job.InputForeignFiles.Where(x => x.EndsWith(".java"));
            if (foreignFiles.Any())
            {
                foreignInclude = Constants.pomForeignTemplate;
                string foreignSourceInclude = "";
                SortedSet<string> foreignFolders = new SortedSet<string>();

                foreach (var fileName in foreignFiles)
                {
                    var folderName = Path.GetDirectoryName(fileName);
                    if (folderName is not null)
                    {
                        foreignFolders.Add(folderName);
                    }
                }
                foreach (var folderName in foreignFolders)
                {
                    foreignSourceInclude += $"                                <source>{folderName}</source>\n";
                }
                foreignInclude = foreignInclude.Replace("-foreign-source-include-", foreignSourceInclude);
            }
            pomTemplate = pomTemplate.Replace("-foreign-include-", foreignInclude);

            File.WriteAllText(pomPath, pomTemplate);

            // compile the csproj file
            var args = new[] { "versions:use-latest-versions -DgenerateBackupPoms=false clean package -q"};

            var exitCode = Compiler.RunWithOutput(job.OutputDirectory.FullName, out stdout, out stderr, "mvn", args);
            if (exitCode != 0)
            {
                throw new TranslationException($"Compiling generated Symbolic Java code FAILED!\n" + $"{stdout}\n" + $"{stderr}\n");
            }
            else
            {
//                job.Output.WriteInfo($"{stdout}");
                job.Output.WriteInfo($"  {job.ProjectName} -> {job.OutputDirectory}/target/{job.ProjectName}-jar-with-dependencies.jar");
                job.Output.WriteInfo("Build succeeded.");
            }

            var sourceDirectory = "target/sources/psym/model";

            // create source folder
            args = new[] { $"-p {sourceDirectory}" };
            exitCode = Compiler.RunWithOutput(job.OutputDirectory.FullName, out stdout, out stderr, "mkdir", args);
            if (exitCode != 0)
            {
                throw new TranslationException($"Unable to create source directory {sourceDirectory}\n" + $"{stdout}\n" + $"{stderr}\n");
            }


            // copy source files
            args = new[] { $"{job.ProjectName}Program.java {sourceDirectory}" };
            exitCode = Compiler.RunWithOutput(job.OutputDirectory.FullName, out stdout, out stderr, "cp", args);
            if (exitCode != 0)
            {
                throw new TranslationException($"Unable to copy source file {job.ProjectName}Program.java to source directory {sourceDirectory}\n" + $"{stdout}\n" + $"{stderr}\n");
            }
        }

        private CompiledFile GenerateSource(CompilationContext context, Scope globalScope)
        {
            var source = new CompiledFile(context.FileName);

            WriteSourcePrologue(context, source.Stream);

            context.WriteLine(source.Stream);

            context.WriteLine(source.Stream);

            //WriteMachineTagDefs(context, source.Stream, globalScope.Machines);

            //context.WriteLine(source.Stream);

            IEnumerable<IPDecl> decls = TransformASTPass.GetTransformedDecls(globalScope);
            //IEnumerable<IPDecl> decls = globalScope.AllDecls;

            bool hasSafetyTest = false;
            foreach (var decl in decls) {
                hasSafetyTest |= (decl.GetType() == typeof(SafetyTest));
                WriteDecl(context, source.Stream, decl);
            }

            if (!hasSafetyTest) {
                WriteMainDriver(context, source.Stream, globalScope, decls);
            }

            context.WriteLine(source.Stream, "PTestDriver testDriver = null;");
            context.WriteLine(source.Stream, "@Generated");
            context.WriteLine(source.Stream, "public PTestDriver getTestDriver() { return testDriver; }");
            context.WriteLine(source.Stream, "@Generated");
            context.WriteLine(source.Stream, "public void setTestDriver(PTestDriver input) { testDriver = input; }");
            context.WriteLine(source.Stream);

            context.WriteLine(source.Stream, "@Generated");
            context.WriteLine(source.Stream, "public Machine getStart() { return testDriver.getStart(); }");
            context.WriteLine(source.Stream, "@Generated");
            context.WriteLine(source.Stream, "public List<Monitor> getMonitors() { return testDriver.getMonitors(); }");
            context.WriteLine(source.Stream, "@Generated");
            context.WriteLine(source.Stream, "public Map<Event, List<Monitor>> getListeners() { return testDriver.getListeners(); }");
            context.WriteLine(source.Stream);

            WriteSourceEpilogue(context, source.Stream);

            return source;
        }

        private void WriteDriver(CompilationContext context, StringWriter output, String startMachine, IEnumerable<IPDecl> decls)
        {
            WriteDriverConfigure(context, output, startMachine, decls);
            context.WriteLine(output);
        }

        private void WriteDriverConfigure(CompilationContext context, StringWriter output, String startMachine, IEnumerable<IPDecl> decls)
        {
            context.WriteLine(output);
            context.WriteLine(output, "@Generated");
            context.WriteLine(output, "public void configure() {");

            context.WriteLine(output, $"    mainMachine = new {startMachine}(0);");
            context.WriteLine(output, $"    monitorList.clear();");
            context.WriteLine(output, $"    observerMap.clear();");

            foreach (var decl in decls)
            {
                switch(decl)
                {
                    case Machine machine:
                        if (machine.IsSpec)
                        {
                            context.WriteLine(output);
                            var declName = context.GetNameForDecl(machine);
                            context.WriteLine(output, $"    Monitor instance_{declName} = new {declName}(0);");
                            context.WriteLine(output, $"    monitorList.add(instance_{declName});");
                            foreach (var pEvent in machine.Observes.Events)
                            {
                                context.WriteLine(output, $"    if(!observerMap.containsKey({pEvent.Name}))");
                                context.WriteLine(output, $"        observerMap.put({pEvent.Name}, new ArrayList<>());");
                                context.WriteLine(output, $"    observerMap.get({pEvent.Name}).add(instance_{declName});");
                            }
                        }
                        break;
                    default:
                        break;
                }
            }

            context.WriteLine(output, "}");
        }

        private void WriteMainDriver(CompilationContext context, StringWriter output, Scope globalScope, IEnumerable<IPDecl> decls)
        {
            // TODO: Determine how main machine should be selected.  Should the 'main' method even
            // be generated from the P program, or should it be provided externally?
            Machine mainMachine = null;
            foreach (var machine in globalScope.Machines)
            {
                if (machine.Name == "Main")
                {
                    if (mainMachine != null)
                        throw new NotImplementedException("Cannot have multiple main machines.");

                    mainMachine = machine;
                }
            }

            if (mainMachine == null)
                return;

            foreach (var decl in decls)
            {
                if ((decl is SafetyTest) && (context.GetNameForDecl(decl) == "DefaultImpl"))
                    return;
            }

            context.WriteLine(output, "@Generated");
            context.WriteLine(output, "public static class DefaultImpl extends PTestDriver {");
            WriteDriver(context, output, mainMachine.Name, decls);
            context.WriteLine(output, "}");
            context.WriteLine(output);
        }

        private void WriteEvent(CompilationContext context, StringWriter output, PEvent ev)
        {
            context.WriteLine(output, $"public static Event {context.GetNameForDecl(ev)} = new Event(\"{context.GetNameForDecl(ev)}\");");
        }

        private void WriteDecl(CompilationContext context, StringWriter output, IPDecl decl)
        {
            switch (decl)
            {
                case Function function:
                    if (function.IsForeign)
                        WriteForeignFunction(context, output, function);
                    else
                        WriteFunction(context, output, function);
                    break;
                case Machine machine:
                    if (machine.IsSpec)
                        WriteMonitor(context, output, machine);
                    else
                        WriteMachine(context, output, machine);
                    break;
                case PEvent ev:
                    WriteEvent(context, output, ev);
                    break;
                case SafetyTest safety:
                    WriteSafetyTestDecl(context, output, safety);
                    break;
                default:
                    context.WriteLine(output, $"// Skipping {decl.GetType().Name} '{decl.Name}'\n");
                    break;
            }
        }

        private void WriteSafetyTestDecl(CompilationContext context, StringWriter output, SafetyTest safety)
        {
            context.WriteLine(output, "@Generated");
            context.WriteLine(output, $"public static class {context.GetNameForDecl(safety)} extends PTestDriver {{");
            WriteDriver(context, output, safety.Main, safety.ModExpr.ModuleInfo.MonitorMap.Keys);
            context.WriteLine(output, "}");
            context.WriteLine(output);
        }

        private void WriteMonitor(CompilationContext context, StringWriter output, Machine machine)
        {
            var declName = context.GetNameForDecl(machine);
            context.WriteLine(output, $"public static class {declName} extends Monitor {{");

            context.WriteLine(output);

            for (var i = 0; i < machine.States.Count(); i++)
            {
                var state = machine.States.ElementAt(i);
                context.Write(output, $"static State {context.GetNameForDecl(state)} = ");
                WriteState(context, output, state, machine);
                context.WriteLine(output, ";");
            }

            foreach (var field in machine.Fields)
                context.WriteLine(output, $"private {GetSymbolicType(field.Type)} {CompilationContext.GetVar(field.Name)} = {GetDefaultValueNoGuard(context, field.Type)};");

            context.WriteLine(output);

            context.WriteLine(output, "@Generated");
            context.WriteLine(output, "@Override");
            context.WriteLine(output, "public void reset() {");
            context.WriteLine(output, "    super.reset();");
            foreach (var field in machine.Fields)
                context.WriteLine(output, $"    {CompilationContext.GetVar(field.Name)} = {GetDefaultValueNoGuard(context, field.Type)};");
            context.WriteLine(output, "}");

            context.WriteLine(output);

            context.WriteLine(output, "@Generated");
            context.WriteLine(output, "@Override");
            context.WriteLine(output, "protected List<ValueSummary> getLocalVars() {");
            context.WriteLine(output, "    List<ValueSummary> res = super.getLocalVars();");
            foreach (var field in machine.Fields)
                context.WriteLine(output, $"    res.add({CompilationContext.GetVar(field.Name)});");
            context.WriteLine(output, "    return res;");
            context.WriteLine(output, "}");
            context.WriteLine(output);

            context.WriteLine(output, "@Generated");
            context.WriteLine(output, "@Override");
            context.WriteLine(output, "protected int setLocalVars(List<ValueSummary> localVars) {");
            context.WriteLine(output, "    int idx = super.setLocalVars(localVars);");
            foreach (var field in machine.Fields)
                context.WriteLine(output, $"    {CompilationContext.GetVar(field.Name)} = ({GetSymbolicType(field.Type)}) localVars.get(idx++);");
            context.WriteLine(output, "    return idx;");
            context.WriteLine(output, "}");
            context.WriteLine(output);

            WriteMachineConstructor(context, output, machine);

            context.WriteLine(output);

            foreach (var method in machine.Methods)
                WriteFunction(context, output, method);

            context.WriteLine(output, "}");
            context.WriteLine(output);
        }

        private void WriteMachine(CompilationContext context, StringWriter output, Machine machine)
        {
            var declName = context.GetNameForDecl(machine);
            context.WriteLine(output, $"public static class {declName} extends Machine {{");

            context.WriteLine(output);

            for (var i = 0; i < machine.States.Count(); i++)
            {
                var state = machine.States.ElementAt(i);
                context.Write(output, $"static State {context.GetNameForDecl(state)} = ");
                WriteState(context, output, state, machine);
                context.WriteLine(output, ";");
            }

            foreach (var field in machine.Fields)
                context.WriteLine(output, $"private {GetSymbolicType(field.Type)} {CompilationContext.GetVar(field.Name)} = {GetDefaultValueNoGuard(context, field.Type)};");

            context.WriteLine(output);

            context.WriteLine(output, "@Generated");
            context.WriteLine(output, "@Override");
            context.WriteLine(output, "public void reset() {");
            context.WriteLine(output, "    super.reset();");
            foreach (var field in machine.Fields)
                context.WriteLine(output, $"    {CompilationContext.GetVar(field.Name)} = {GetDefaultValueNoGuard(context, field.Type)};");
            context.WriteLine(output, "}");

            context.WriteLine(output);

            context.WriteLine(output, "@Generated");
            context.WriteLine(output, "@Override");
            context.WriteLine(output, "protected List<ValueSummary> getLocalVars() {");
            context.WriteLine(output, "    List<ValueSummary> res = super.getLocalVars();");
            foreach (var field in machine.Fields)
                context.WriteLine(output, $"    res.add({CompilationContext.GetVar(field.Name)});");
            context.WriteLine(output, "    return res;");
            context.WriteLine(output, "}");
            context.WriteLine(output);

            context.WriteLine(output, "@Generated");
            context.WriteLine(output, "@Override");
            context.WriteLine(output, "protected int setLocalVars(List<ValueSummary> localVars) {");
            context.WriteLine(output, "    int idx = super.setLocalVars(localVars);");
            foreach (var field in machine.Fields)
                context.WriteLine(output, $"    {CompilationContext.GetVar(field.Name)} = ({GetSymbolicType(field.Type)}) localVars.get(idx++);");
            context.WriteLine(output, "    return idx;");
            context.WriteLine(output, "}");
            context.WriteLine(output);

            WriteMachineConstructor(context, output, machine);

            context.WriteLine(output);

            foreach (var method in machine.Methods)
                WriteFunction(context, output, method);

            context.WriteLine(output, "}");
            context.WriteLine(output);
        }

        private void WriteMachineConstructor(CompilationContext context, StringWriter output, Machine machine)
        {
            var declName = context.GetNameForDecl(machine);
            context.WriteLine(output, $"public {declName}(int id) {{");
            context.Write(output, $"super(\"{context.GetMachineName(machine)}\", id, {context.GetNameForDecl(machine.StartState)}");

            foreach (var state in machine.States)
            {
                context.WriteLine(output, $", {context.GetNameForDecl(state)}");
            }

            context.WriteLine(output);
            context.WriteLine(output, ");");

            foreach (var state in machine.States)
            {
                WriteHandlerUpdate(context, output, state);
            }

            foreach (var method in machine.Methods)
            {
                if (method is Continuation)
                {
                    var cont = (Continuation) method;
                    context.Write(output, $"continuations.put(\"{context.GetContinuationName(cont)}\", ");
                    context.Write(output, $"(pc) -> ((continuation_outcome, msg) -> {context.GetContinuationName(cont)}(pc,");
                    context.Write(output, $"getSendBuffer()");
                    context.Write(output, ", continuation_outcome");
                    context.WriteLine(output, $", msg)));");
                    context.WriteLine(output, $"clearContinuationVars.add(() -> clear_{context.GetContinuationName(cont)}());");
                }
            }

            context.WriteLine(output, "}");
        }

        private void WriteHandlerUpdate(CompilationContext context, StringWriter output, State state)
        {
            context.Write(output, $"{context.GetNameForDecl(state)}.addHandlers(");
            var first = true;
            foreach (var handler in state.AllEventHandlers)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    context.WriteLine(output, ",");
                }
                WriteEventHandler(context, output, handler, state);
            }
            context.WriteLine(output, ");");
        }

        private void WriteState(CompilationContext context, StringWriter output, State state, Machine machine)
        {
            var temperature = Enum.GetName(state.Temperature.GetType(), state.Temperature);
            context.Write(output, $"new State(\"{context.GetNameForDecl(state)}\", \"{context.GetNameForDecl(machine)}\", StateTemperature.{temperature}");
            /*
            foreach (var handler in state.AllEventHandlers)
            {
                context.WriteLine(output, ",");
                WriteEventHandler(context, output, handler, state);
            }
            context.WriteLine(output);
            */
            context.WriteLine(output, ") {");
            if (state.Entry != null)
            {
                var entryPcScope = context.FreshPathConstraintScope();

                context.WriteLine(output, "@Generated");
                context.WriteLine(output, "@Override");
                context.WriteLine(output, $"public void entry(Guard {entryPcScope.PathConstraintVar}, Machine machine, EventHandlerReturnReason outcome, UnionVS payload) {{");
                context.WriteLine(output, $"super.entry({entryPcScope.PathConstraintVar}, machine, outcome, payload);");

                var entryFunc = state.Entry;
                entryFunc.Name = $"{context.GetNameForDecl(state)}_entry";
                context.Write(output, $"(({context.GetNameForDecl(entryFunc.Owner)})machine).{context.GetNameForDecl(entryFunc)}({entryPcScope.PathConstraintVar}, machine.getSendBuffer()");
                if (entryFunc.CanChangeState)
                    context.Write(output, ", outcome");
                else if (entryFunc.CanRaiseEvent)
                    context.Write(output, ", outcome");
                if (entryFunc.Signature.Parameters.Any())
                {
                    Debug.Assert(entryFunc.Signature.Parameters.Count() == 1);
                    var payloadType = entryFunc.Signature.Parameters[0].Type;
                    var payloadTypeSymbolic = GetSymbolicType(payloadType);
                    var defaultPayload = GetDefaultValue(context, entryPcScope, payloadType);
                    context.Write(output, $", payload != null ? ({payloadTypeSymbolic}) ValueSummary.castFromAny({entryPcScope.PathConstraintVar}, {defaultPayload}, payload) : {defaultPayload}");
                }
                context.WriteLine(output, ");");

                context.WriteLine(output, "}");
            }
            if (state.Exit != null)
            {
                context.WriteLine(output, "@Override public void exit(Guard pc, Machine machine) {");
                context.WriteLine(output, $"super.exit(pc, machine);");

                var exitFunc = state.Exit;
                exitFunc.Name = $"{context.GetNameForDecl(state)}_exit";
                Debug.Assert(!exitFunc.CanChangeState);
                Debug.Assert(!exitFunc.CanRaiseEvent);
                if (exitFunc.Signature.Parameters.Count() != 0)
                    throw new NotImplementedException("Exit functions with payloads are not yet supported");
                context.WriteLine(output, $"(({context.GetNameForDecl(exitFunc.Owner)})machine).{context.GetNameForDecl(exitFunc)}(pc, machine.getSendBuffer());");

                context.WriteLine(output, "}");
            }
            context.Write(output, "}");
        }

        private void WriteEventHandler(CompilationContext context, StringWriter output, KeyValuePair<PEvent, IStateAction> handler, State state)
        {
            var eventTag = context.GetNameForDecl(handler.Key);
            switch (handler.Value)
            {
                case EventDefer _:
                    context.WriteLine(output, $"new DeferEventHandler({eventTag})");
                    break;
                case EventDoAction action:
                    context.WriteLine(output, $"new EventHandler({eventTag}) {{");
                    context.WriteLine(output, "@Override public void handleEvent(Guard pc, Machine machine, UnionVS payload, EventHandlerReturnReason outcome) {");
                    var actionFunc = action.Target;
                    if (actionFunc.Name == "")
                        actionFunc.Name = $"{context.GetNameForDecl(state)}_{eventTag}";
                    context.Write(output, $"(({context.GetNameForDecl(actionFunc.Owner)})machine).{context.GetNameForDecl(actionFunc)}(pc, machine.getSendBuffer()");
                    if (actionFunc.CanChangeState)
                        context.Write(output, ", outcome");
                    else if (actionFunc.CanRaiseEvent)
                        context.Write(output, ", outcome");
                    if (actionFunc.Signature.Parameters.Count() == 1)
                    {
                        Debug.Assert(!actionFunc.Signature.Parameters[0].Type.IsSameTypeAs(PrimitiveType.Null));
                        var payloadVSType = GetSymbolicType(actionFunc.Signature.Parameters[0].Type);
                        var defaultPayload = GetDefaultValueNoGuard(context, actionFunc.Signature.Parameters[0].Type);
                        context.Write(output, $", ({payloadVSType}) ValueSummary.castFromAny(pc, {defaultPayload}, payload)");
                    }
                    context.WriteLine(output, ");");
                    context.WriteLine(output, "}");
                    context.Write(output, "}");
                    break;
                case EventGotoState gotoState:
                    var destTag = $"{context.GetNameForDecl(gotoState.Target)}";
                    context.Write(output, $"new GotoEventHandler({eventTag}, {destTag}");
                    context.Write(output, ")");

                    if (gotoState.TransitionFunction != null)
                    {
                        context.WriteLine(output, " {");
                        context.WriteLine(output, "@Override public void transitionFunction(Guard pc, Machine machine, UnionVS payload) {");

                        var transitionFunc = gotoState.TransitionFunction;
                        Debug.Assert(!transitionFunc.CanChangeState);
                        Debug.Assert(!transitionFunc.CanRaiseEvent);
                        if (transitionFunc.Name == "")
                            transitionFunc.Name = $"{context.GetNameForDecl(state)}_{eventTag}_{destTag}";

                        context.Write(output, $"(({context.GetNameForDecl(transitionFunc.Owner)})machine).{context.GetNameForDecl(transitionFunc)}(pc, machine.getSendBuffer()");
                        if (transitionFunc.Signature.Parameters.Count() == 1)
                        {
                            Debug.Assert(!transitionFunc.Signature.Parameters[0].Type.IsSameTypeAs(PrimitiveType.Null));
                            var payloadVSType = GetSymbolicType(transitionFunc.Signature.Parameters[0].Type);
                            var defaultPayload = GetDefaultValueNoGuard(context, transitionFunc.Signature.Parameters[0].Type);
                            context.Write(output, $", ({payloadVSType}) ValueSummary.castFromAny(pc, {defaultPayload}, payload)");
                        }
                        context.WriteLine(output, ");");
                        context.WriteLine(output, "}");
                        context.Write(output, "}");
                    }
                    break;
                case EventIgnore _:
                    context.Write(output, $"new IgnoreEventHandler({eventTag})");
                    break;
                default:
                    throw new NotImplementedException($"Unrecognized handler type {handler.Value.GetType().Name}");
            }
        }

        internal struct ControlFlowContext
        {
            internal readonly PathConstraintScope pcScope;
            internal readonly LoopScope? loopScope;
            internal readonly BranchScope? branchScope;

            public ControlFlowContext(PathConstraintScope pcScope, LoopScope? loopScope, BranchScope? branchScope)
            {
                this.pcScope = pcScope;
                this.loopScope = loopScope;
                this.branchScope = branchScope;
            }

            internal static ControlFlowContext FreshFuncContext(CompilationContext context, PathConstraintScope pathConstraintScope)
            {
                return new ControlFlowContext(pathConstraintScope, null, null);
            }

            internal static ControlFlowContext FreshLoopContext(CompilationContext context)
            {
                return new ControlFlowContext(context.FreshPathConstraintScope(), context.FreshLoopScope(), null);
            }

            internal ControlFlowContext FreshBranchSubContext(CompilationContext context)
            {
                return new ControlFlowContext(context.FreshPathConstraintScope(), loopScope, context.FreshBranchScope());
            }
        }

        private bool MayExitWithOutcome(Function func)
        {
            return func.CanChangeState || func.CanRaiseEvent;
        }

        private enum FunctionReturnConvention
        {
            RETURN_VALUE,
            RETURN_VOID,
            RETURN_VALUE_OR_EXIT,
            // Guard indicates path constraint after the call, which may be more restricted
            // than the path constraint before the call if the function exited with an
            // outcome (i.e. a 'raise' or 'goto' statement) along some paths
            RETURN_GUARD
        }

        private FunctionReturnConvention GetReturnConvention(Function function)
        {
            var mayExit = MayExitWithOutcome(function);
            var voidReturn = function.Signature.ReturnType.IsSameTypeAs(PrimitiveType.Null);
            if (!voidReturn && !mayExit)
                return FunctionReturnConvention.RETURN_VALUE;
            if (voidReturn && !mayExit)
                return FunctionReturnConvention.RETURN_VOID;
            if (!voidReturn && mayExit)
                return FunctionReturnConvention.RETURN_VALUE_OR_EXIT;
            if (voidReturn && mayExit)
                return FunctionReturnConvention.RETURN_GUARD;
            throw new InvalidOperationException();
        }

        private void WriteForeignFunction(CompilationContext context, StringWriter output, Function function)
        {
            var isStatic = function.Owner == null;

            if (function.CanReceive)
                throw new NotImplementedException($"Async functions {context.GetNameForDecl(function)} are not supported");

            var staticKeyword = isStatic ? "static " : "";

            var rootPCScope = context.FreshPathConstraintScope();

            string returnType = null;
            var returnStatement = "";
            switch (GetReturnConvention(function))
            {
                case FunctionReturnConvention.RETURN_VALUE:
                    returnType = "Object";
                    returnStatement = "return ";
                    break;
                case FunctionReturnConvention.RETURN_VOID:
                    returnType = "void";
                    break;
            }

            var functionName = $"wrapper__{context.GetNameForDecl(function)}";

            context.Write(output, $"{staticKeyword}{returnType} ");
            context.Write(output, functionName);
            context.WriteLine(output, " (List<Object> args) { ");
            context.Write(output, $"    {returnStatement}GlobalFunctions.{context.GetNameForDecl(function)}(");
            var i = 0;
            foreach (var param in function.Signature.Parameters)
            {
                if (i > 0)
                    context.WriteLine(output, ",");
                string foreignType = GetConcreteForeignBoxedType(param.Type);
                if (foreignType == "Object") {
                    context.Write(output, $"args.get({i})");
                } else {
                    context.Write(output, $"new {foreignType}(args.get({i}))");
                }
                i++;
            }
            context.WriteLine(output, ");");
            context.WriteLine(output, " }");
        }

        private void WriteFunction(CompilationContext context, StringWriter output, Function function)
        {
            if (function is Continuation)
            {
                WriteContinuation(context, output, (Continuation) function);
                return;
            }

            var isStatic = function.Owner == null;

            var staticKeyword = isStatic ? "static " : "";

            var rootPCScope = context.FreshPathConstraintScope();

            string returnType = null;
            switch (GetReturnConvention(function))
            {
                case FunctionReturnConvention.RETURN_VALUE:
                    returnType = GetSymbolicType(function.Signature.ReturnType);
                    break;
                case FunctionReturnConvention.RETURN_VOID:
                    returnType = "void";
                    break;
                case FunctionReturnConvention.RETURN_VALUE_OR_EXIT:
                    returnType = GetSymbolicType(function.Signature.ReturnType);//$"MaybeExited<{GetSymbolicType(function.Signature.ReturnType)}>";
                    break;
                case FunctionReturnConvention.RETURN_GUARD:
                    returnType = "Guard";
                    break;
            }

            context.ReturnType = function.Signature.ReturnType;
            var functionName = context.GetNameForDecl(function);

            context.WriteLine(output, $"{staticKeyword}{returnType} ");
            context.Write(output, functionName);

            context.WriteLine(output, $"(");
            context.WriteLine(output, $"Guard {rootPCScope.PathConstraintVar},");
            context.Write(output, $"EventBuffer {CompilationContext.EffectCollectionVar}");
            if (function.CanChangeState)
            {
                Debug.Assert(function.Owner != null);
                context.WriteLine(output, ",");
                context.Write(output, "EventHandlerReturnReason outcome");
            }
            else if (function.CanRaiseEvent)
            {
                context.WriteLine(output, ",");
                context.Write(output, "EventHandlerReturnReason outcome");
            }
            foreach (var param in function.Signature.Parameters)
            {
                context.WriteLine(output, ",");
                context.Write(output, $"{GetSymbolicType(param.Type, true)} {CompilationContext.GetVar(param.Name)}");
            }
            context.WriteLine(output);
            context.Write(output, ") ");

            context.WriteLine(output, "{");

            WriteFunctionBody(context, output, rootPCScope, function);

            context.WriteLine(output, "}");
            context.WriteLine(output);
            context.ReturnType = null;
        }

        private void WriteFunctionBody(CompilationContext context, StringWriter output, PathConstraintScope rootPCScope, Function function)
        {
            foreach (var local in function.LocalVariables)
            {
                context.WriteLine(output, $"{GetSymbolicType(local.Type)} {CompilationContext.GetVar(local.Name)} =");
                context.WriteLine(output, $"    {GetDefaultValue(context, rootPCScope, local.Type)};");
                context.WriteLine(output);
            }

            var returnConvention = GetReturnConvention(function);
            switch (returnConvention)
            {
                case FunctionReturnConvention.RETURN_VALUE:
                case FunctionReturnConvention.RETURN_VALUE_OR_EXIT:
                    context.WriteLine(output, $"{GetSymbolicType(function.Signature.ReturnType)} {CompilationContext.ReturnValue} = new {GetSymbolicType(function.Signature.ReturnType)}({GetDefaultValue(context, rootPCScope, function.Signature.ReturnType)});");
                    break;
                case FunctionReturnConvention.RETURN_VOID:
                case FunctionReturnConvention.RETURN_GUARD:
                    break;
            }

            if (function is WhileFunction)
            {
                /* Prologue */
                var loopPCScope = context.FreshPathConstraintScope();
                context.WriteLine(output, $"Guard {loopPCScope.PathConstraintVar} = {rootPCScope.PathConstraintVar};");
                /* Loop body */
                WriteStmt(function, context, output, ControlFlowContext.FreshFuncContext(context, loopPCScope), function.Body);
            } else
            {
                WriteStmt(function, context, output, ControlFlowContext.FreshFuncContext(context, rootPCScope), function.Body);
            }

            switch (returnConvention)
            {
                case FunctionReturnConvention.RETURN_VALUE:
                    context.WriteLine(output, $"return {CompilationContext.ReturnValue};");
                    break;
                case FunctionReturnConvention.RETURN_VOID:
                    break;
                case FunctionReturnConvention.RETURN_VALUE_OR_EXIT:
                    context.WriteLine(output, $"return {CompilationContext.ReturnValue}.restrict({rootPCScope.PathConstraintVar});");
                    break;
                case FunctionReturnConvention.RETURN_GUARD:
                    context.WriteLine(output, $"return {rootPCScope.PathConstraintVar};");
                    break;
            }
        }

        private FunCallExpr TryGetCallInAssignment(IPStmt stmt)
        {
            if (stmt is AssignStmt assign)
            {
                if (assign.Value is FunCallExpr call)
                {
                    return call;
                }
            }
            return null;
        }

        private bool CanEarlyReturn(IPStmt stmt)
        {
            var callExpr = TryGetCallInAssignment(stmt);
            if (callExpr != null)
                return MayExitWithOutcome(callExpr.Function);

            switch (stmt)
            {
                case CompoundStmt compoundStmt:
                    return compoundStmt.Statements.Any((subStmt) => CanEarlyReturn(subStmt));
                case IfStmt ifStmt:
                    return CanEarlyReturn(ifStmt.ThenBranch) || CanEarlyReturn(ifStmt.ElseBranch);
                case WhileStmt whileStmt:
                    return CanEarlyReturn(whileStmt.Body);
                case FunCallStmt callStmt:
                    return MayExitWithOutcome(callStmt.Function);

                case GotoStmt _:
                case RaiseStmt _:
                case ReturnStmt _:
                    return true;

                default:
                    return false;
            }
        }

        private bool MustEarlyReturn(IPStmt stmt)
        {
            switch (stmt)
            {
                case CompoundStmt compoundStmt:
                    return compoundStmt.Statements.Any((subStmt) => MustEarlyReturn(subStmt));
                case IfStmt ifStmt:
                    return MustEarlyReturn(ifStmt.ThenBranch) && MustEarlyReturn(ifStmt.ElseBranch);
                case WhileStmt whileStmt:
                    return MustEarlyReturn(whileStmt.Body);

                case GotoStmt _:
                case RaiseStmt _:
                case ReturnStmt _:
                    return true;

                default:
                    return false;
            }
        }

        private bool CanJumpOut(IPStmt stmt)
        {
            var callExpr = TryGetCallInAssignment(stmt);
            if (callExpr != null)
                return MayExitWithOutcome(callExpr.Function);

            switch (stmt)
            {
                case CompoundStmt compoundStmt:
                    return compoundStmt.Statements.Any((subStmt) => CanJumpOut(subStmt));
                case IfStmt ifStmt:
                    return CanJumpOut(ifStmt.ThenBranch) || CanJumpOut(ifStmt.ElseBranch);
                case WhileStmt whileStmt:
                    // Any breaks or continues inside this loop body will be "caught" by the loop,
                    // so we only want to consider statements which return from the entire function.
                    return CanEarlyReturn(whileStmt.Body);
                case FunCallStmt callStmt:
                    return MayExitWithOutcome(callStmt.Function);

                case GotoStmt _:
                case RaiseStmt _:
                case ReturnStmt _:
                case BreakStmt _:
                case ContinueStmt _:
                    return true;

                default:
                    return false;
            }
        }

        private bool MustJumpOut(IPStmt stmt)
        {
            switch (stmt)
            {
                case CompoundStmt compoundStmt:
                    return compoundStmt.Statements.Any((subStmt) => MustJumpOut(subStmt));
                case IfStmt ifStmt:
                    return MustJumpOut(ifStmt.ThenBranch) && MustJumpOut(ifStmt.ElseBranch);
                case WhileStmt whileStmt:
                    // Any breaks or continues inside this loop body will be "caught" by the loop,
                    // so we only want to consider statements which return from the entire function.
                    return MustEarlyReturn(whileStmt.Body);

                case GotoStmt _:
                case RaiseStmt _:
                case ReturnStmt _:
                case BreakStmt _:
                case ContinueStmt _:
                    return true;

                default:
                    return false;
            }
        }

        private void SetFlagsForPossibleReturn(CompilationContext context, StringWriter output, ControlFlowContext flowContext)
        {
            if (!(flowContext.loopScope is null))
            {
                context.WriteLine(output, $"{flowContext.loopScope.Value.LoopEarlyReturnFlag} = true;");
            }

            if (!(flowContext.branchScope is null))
            {
                context.WriteLine(output, $"{flowContext.branchScope.Value.JumpedOutFlag} = true;");
            }
        }

        private IPExpr UnnestCloneExpr(IPExpr expr)
        {
            switch (expr)
            {
                case CloneExpr cloneExpr:
                    return UnnestCloneExpr(cloneExpr.Term);
                default:
                    return expr;
            }
        }

        private void WriteStmt(Function function, CompilationContext context, StringWriter output, ControlFlowContext flowContext, IPStmt stmt)
        {
            if (TryGetCallInAssignment(stmt) is FunCallExpr callExpr)
            {
                WriteFunCallStmt(context, output, flowContext, callExpr.Function, callExpr.Arguments, dest: (stmt as AssignStmt).Location);
                return;
            }

            switch (stmt)
            {
                case AssignStmt assignStmt:
                    Debug.Assert(assignStmt != null);
                    Debug.Assert(assignStmt.Value != null);
                    Debug.Assert(assignStmt.Location != null);
                    CheckIsSupportedAssignment(assignStmt.Value.Type, assignStmt.Location.Type);

                    WriteWithLValueMutationContext(
                        context,
                        output,
                        flowContext.pcScope,
                        assignStmt.Location,
                        false,
                        locationTemp =>
                        {
                            var expr = UnnestCloneExpr(assignStmt.Value);
                            if (expr is NullLiteralExpr)
                            {
                                context.WriteLine(output, $"{locationTemp} = {GetDefaultValue(context, flowContext.pcScope, assignStmt.Location.Type)};");
                            } else
                            {
                                var inlineCastPrefix = GetInlineCastPrefix(assignStmt.Value.Type, assignStmt.Location.Type, context, flowContext.pcScope);
                                context.Write(output, $"{locationTemp} = {inlineCastPrefix}");
                                WriteExpr(context, output, flowContext.pcScope, expr);
                                if (inlineCastPrefix != "") context.Write(output, ")");
                                context.WriteLine(output, ";");
                            }
                        }
                    );

                    break;

                case MoveAssignStmt moveStmt:
                    CheckIsSupportedAssignment(moveStmt.FromVariable.Type, moveStmt.ToLocation.Type);

                    WriteWithLValueMutationContext(
                        context,
                        output,
                        flowContext.pcScope,
                        moveStmt.ToLocation,
                        false,
                        locationTemp =>
                        {
                            var inlineCastPrefix = GetInlineCastPrefix(moveStmt.FromVariable.Type, moveStmt.ToLocation.Type, context, flowContext.pcScope);
                            context.Write(output, $"{locationTemp} = {inlineCastPrefix}");
                            WriteExpr(context, output, flowContext.pcScope, new VariableAccessExpr(moveStmt.FromVariable.SourceLocation, moveStmt.FromVariable));
                            if (inlineCastPrefix != "") context.Write(output, ")");
                            context.WriteLine(output, ";");
                        }
                    );

                    break;

                case AssertStmt assertStmt:
                    context.Write(output, "Assert.progProp(!(");
                    WriteExpr(context, output, flowContext.pcScope, assertStmt.Assertion);
                    context.Write(output, ").getValues().contains(Boolean.FALSE), ");
                    WriteExpr(context, output, flowContext.pcScope, assertStmt.Message);
                    context.Write(output, $", ");
                    WriteExpr(context, output, flowContext.pcScope, assertStmt.Assertion);
                    context.Write(output, ".getGuardFor(Boolean.FALSE));");
                    break;

                case ReturnStmt returnStmt:
                    if (!(returnStmt.ReturnValue is null))
                    {
                        context.Write(output, $"{CompilationContext.ReturnValue} = {CompilationContext.ReturnValue}.updateUnderGuard(");
                        var inlineCastPrefix = GetInlineCastPrefix(returnStmt.ReturnValue.Type, context.ReturnType, context, flowContext.pcScope);
                        context.Write(output, $"{flowContext.pcScope.PathConstraintVar}, {inlineCastPrefix}");
                        WriteExpr(context, output, flowContext.pcScope, returnStmt.ReturnValue);
                        if (inlineCastPrefix != "") context.Write(output, ")");
                        context.WriteLine(output, $");");
                    }

                    context.WriteLine(output, $"{flowContext.pcScope.PathConstraintVar} = Guard.constFalse();");
                    SetFlagsForPossibleReturn(context, output, flowContext);

                    break;

                case GotoStmt gotoStmt:
                    context.Write(output, $"outcome.addGuardedGoto({flowContext.pcScope.PathConstraintVar}, {context.GetNameForDecl(gotoStmt.State)}");
                    if (gotoStmt.Payload != null)
                    {
                        context.Write(output, $", new UnionVS(");
                        WriteExpr(context, output, flowContext.pcScope, gotoStmt.Payload);
                        context.Write(output, $")");
                    }
                    context.WriteLine(output, ");");

                    context.WriteLine(output, $"{flowContext.pcScope.PathConstraintVar} = Guard.constFalse();");
                    SetFlagsForPossibleReturn(context, output, flowContext);

                    break;

                case RaiseStmt raiseStmt:
                    // TODO: Add type checking for the payload!
                    context.WriteLine(output, "// NOTE (TODO): We currently perform no typechecking on the payload!");

                    context.Write(output, $"outcome.raiseGuardedEvent({flowContext.pcScope.PathConstraintVar}, ");
                    WriteExpr(context, output, flowContext.pcScope, raiseStmt.PEvent);
                    if (raiseStmt.Payload.Count > 0)
                    {
                        // TODO: Determine how multi-payload raise statements are supposed to work
                        Debug.Assert(raiseStmt.Payload.Count == 1);
                        context.Write(output, ", new UnionVS(");
                        WriteExpr(context, output, flowContext.pcScope, raiseStmt.Payload[0]);
                        context.Write(output, ")");
                    }
                    context.WriteLine(output, ");");


                    context.WriteLine(output, $"{flowContext.pcScope.PathConstraintVar} = Guard.constFalse();");
                    SetFlagsForPossibleReturn(context, output, flowContext);

                    break;

                case PrintStmt printStmt:
                    context.Write(output, "PSymLogger.log(");
                    WriteExpr(context, output, flowContext.pcScope, printStmt.Message);
                    context.WriteLine(output, ".toString());");
                    break;

                case BreakStmt breakStmt:
                    Debug.Assert(flowContext.loopScope.HasValue);
                    context.WriteLine(output, $"{flowContext.loopScope.Value.LoopExitsList}.add({flowContext.pcScope.PathConstraintVar});");

                    if (flowContext.branchScope.HasValue)
                    {
                        context.WriteLine(output, $"{flowContext.branchScope.Value.JumpedOutFlag} = true;");
                    }

                    context.WriteLine(output, $"{flowContext.pcScope.PathConstraintVar} = Guard.constFalse();");
                    break;

                case CompoundStmt compoundStmt:
                    // Used to determine the number of closing braces to add at the end of the block
                    var nestedEarlyExitCheckCount = 0;

                    foreach (var subStmt in compoundStmt.Statements)
                    {
                        WriteStmt(function, context, output, flowContext, subStmt);
                        context.WriteLine(output);

                        if (MustJumpOut(subStmt))
                            break;

                        if (CanJumpOut(subStmt))
                        {
                            context.WriteLine(output, $"if (!{flowContext.pcScope.PathConstraintVar}.isFalse()) {{");
                            nestedEarlyExitCheckCount++;
                        }
                    }

                    for (var i = 0; i < nestedEarlyExitCheckCount; i++)
                    {
                        context.WriteLine(output, "}");
                    }

                    break;

                case WhileStmt whileStmt:
                    if (!(whileStmt.Condition is BoolLiteralExpr) && ((BoolLiteralExpr)whileStmt.Condition).Value)
                    {
                        throw new ArgumentOutOfRangeException("While statement condition should always be transformed to constant 'true' during IR simplification.");
                    }

                    var loopContext = ControlFlowContext.FreshLoopContext(context);

                    /* Prologue */
                    context.WriteLine(output, $"java.util.List<Guard> {loopContext.loopScope.Value.LoopExitsList} = new java.util.ArrayList<>();");
                    context.WriteLine(output, $"boolean {loopContext.loopScope.Value.LoopEarlyReturnFlag} = false;");
                    context.WriteLine(output, $"Guard {loopContext.pcScope.PathConstraintVar} = {flowContext.pcScope.PathConstraintVar};");

                    /* Loop body */
                    context.WriteLine(output, $"while (!{loopContext.pcScope.PathConstraintVar}.isFalse()) {{");
                    WriteStmt(function, context, output, loopContext, whileStmt.Body);
                    context.WriteLine(output, "}");

                    /* Epilogue */
                    context.WriteLine(output, $"if ({loopContext.loopScope.Value.LoopEarlyReturnFlag}) {{");
                    context.WriteLine(output, $"{flowContext.pcScope.PathConstraintVar} = Guard.orMany({loopContext.loopScope.Value.LoopExitsList});");
                    if (flowContext.branchScope.HasValue)
                    {
                        context.WriteLine(output, $"{flowContext.branchScope.Value.JumpedOutFlag} = true;");
                    }
                    context.WriteLine(output, "}");

                    break;

                case IfStmt ifStmt:
                    /* Prologue */

                    var condTemp = context.FreshTempVar();
                    Debug.Assert(ifStmt.Condition.Type.IsSameTypeAs(PrimitiveType.Bool));
                    context.Write(output, $"{GetSymbolicType(PrimitiveType.Bool)} {condTemp} = ");
                    WriteExpr(context, output, flowContext.pcScope, ifStmt.Condition);
                    context.WriteLine(output, ";");

                    var thenContext = flowContext.FreshBranchSubContext(context);
                    var elseContext = flowContext.FreshBranchSubContext(context);

                    context.WriteLine(output, $"Guard {thenContext.pcScope.PathConstraintVar} = BooleanVS.getTrueGuard({condTemp});");
                    context.WriteLine(output, $"Guard {elseContext.pcScope.PathConstraintVar} = BooleanVS.getFalseGuard({condTemp});");

                    context.WriteLine(output, $"boolean {thenContext.branchScope.Value.JumpedOutFlag} = false;");
                    context.WriteLine(output, $"boolean {elseContext.branchScope.Value.JumpedOutFlag} = false;");

                    /* Body */

                    context.WriteLine(output, $"if (!{thenContext.pcScope.PathConstraintVar}.isFalse()) {{");
                    context.WriteLine(output, "// 'then' branch");
                    WriteStmt(function, context, output, thenContext, ifStmt.ThenBranch);
                    context.WriteLine(output, "}");

                    if (!(ifStmt.ElseBranch is null))
                    {
                        context.WriteLine(output, $"if (!{elseContext.pcScope.PathConstraintVar}.isFalse()) {{");
                        context.WriteLine(output, "// 'else' branch");
                        WriteStmt(function, context, output, elseContext, ifStmt.ElseBranch);
                        context.WriteLine(output, "}");
                    }

                    /* Epilogue */

                    context.WriteLine(output, $"if ({thenContext.branchScope.Value.JumpedOutFlag} || {elseContext.branchScope.Value.JumpedOutFlag}) {{");
                    context.WriteLine(output, $"{flowContext.pcScope.PathConstraintVar} = {thenContext.pcScope.PathConstraintVar}.or({elseContext.pcScope.PathConstraintVar});");

                    if (flowContext.branchScope.HasValue)
                    {
                        context.WriteLine(output, $"{flowContext.branchScope.Value.JumpedOutFlag} = true;");
                    }

                    context.WriteLine(output, "}");

                    break;

                case FunCallStmt funCallStmt:
                    WriteFunCallStmt(context, output, flowContext, funCallStmt.Function, funCallStmt.ArgsList);
                    break;

                case CtorStmt ctorStmt:
                    WriteCtorExpr(context, output, flowContext.pcScope, ctorStmt.Interface, ctorStmt.Arguments);
                    context.WriteLine(output, ";");
                    break;

                case SendStmt sendStmt:
                    context.Write(output, $"{CompilationContext.EffectCollectionVar}.send({flowContext.pcScope.PathConstraintVar}, ");
                    WriteExpr(context, output, flowContext.pcScope, sendStmt.MachineExpr);
                    context.Write(output, ", ");
                    WriteExpr(context, output, flowContext.pcScope, sendStmt.Evt);
                    context.Write(output, ", ");
                    if (sendStmt.Arguments.Count == 0)
                        context.Write(output, "null");
                    else if (sendStmt.Arguments.Count == 1)
                    {
                        context.Write(output, "new UnionVS(");
                        WriteExpr(context, output, flowContext.pcScope, sendStmt.Arguments[0]);
                        context.Write(output, ")");
                    }
                    else
                        throw new NotImplementedException("Send statements with more than one payload argument are not supported");
                    context.WriteLine(output, ");");
                    break;

                case InsertStmt insertStmt:
                {
                    var isMap = PLanguageType.TypeIsOfKind(insertStmt.Variable.Type, TypeKind.Map);
                    var isSet = PLanguageType.TypeIsOfKind(insertStmt.Variable.Type, TypeKind.Set);
                    PLanguageType keyType = null;
                    PLanguageType elementType = null;
                    if (isMap) {
                        keyType = ((MapType) insertStmt.Variable.Type.Canonicalize()).KeyType;
                        elementType = ((MapType) insertStmt.Variable.Type.Canonicalize()).ValueType;
                    } else if (isSet) {
                        elementType = ((SetType) insertStmt.Variable.Type.Canonicalize()).ElementType;
                    } else {
                        elementType = ((SequenceType) insertStmt.Variable.Type.Canonicalize()).ElementType;
                    }

                    WriteWithLValueMutationContext(
                        context,
                        output,
                        flowContext.pcScope,
                        insertStmt.Variable,
                        true,
                        (structureTemp) =>
                        {
                            context.Write(output, $"{structureTemp} = ");
                            WriteExpr(context, output, flowContext.pcScope, insertStmt.Variable);
                            if (isMap)
                                context.Write(output, $".add(");
                            else
                                context.Write(output, $".insert(");

                            {
                                var castPrefixKey = "";
                                if (keyType != null) {
                                    castPrefixKey = GetInlineCastPrefix(insertStmt.Index.Type, keyType, context, flowContext.pcScope);
                                }
                                context.Write(output, castPrefixKey);
                                WriteExpr(context, output, flowContext.pcScope, insertStmt.Index);
                                if (castPrefixKey != "") context.Write(output, ")");
                                context.Write(output, ", ");
                            }

                            {
                                var castPrefix = "";
                                if (elementType != null) {
                                    castPrefix = GetInlineCastPrefix(insertStmt.Value.Type, elementType, context, flowContext.pcScope);
                                }
                                context.Write(output, castPrefix);
                                WriteExpr(context, output, flowContext.pcScope, insertStmt.Value);
                                if (castPrefix != "") context.Write(output, ")");
                            }

                            context.WriteLine(output, ");");
                        }
                    );

                    break;
                }

                case AddStmt addStmt:
                {
                    WriteWithLValueMutationContext(
                        context,
                        output,
                        flowContext.pcScope,
                        addStmt.Variable,
                        true,
                        (structureTemp) =>
                        {
                            context.Write(output, $"{structureTemp} = ");
                            WriteExpr(context, output, flowContext.pcScope, addStmt.Variable);
                            context.Write(output, $".add(");
                            WriteExpr(context, output, flowContext.pcScope, addStmt.Value);
                            context.WriteLine(output, ");");
                        }
                    );

                    break;
                }

                case RemoveStmt removeStmt:
                {
                    var isMap = PLanguageType.TypeIsOfKind(removeStmt.Variable.Type, TypeKind.Map);
                    var isSet = PLanguageType.TypeIsOfKind(removeStmt.Variable.Type, TypeKind.Set);

                    WriteWithLValueMutationContext(
                        context,
                        output,
                        flowContext.pcScope,
                        removeStmt.Variable,
                        true,
                        (structureTemp) =>
                        {
                            context.Write(output, $"{structureTemp} = ");
                            WriteExpr(context, output, flowContext.pcScope, removeStmt.Variable);

                            if (isMap || isSet)
                                context.Write(output, $".remove(");
                            else
                                context.Write(output, $".removeAt(");

                            WriteExpr(context, output, flowContext.pcScope, removeStmt.Value);
                            context.WriteLine(output, ");");
                        }
                    );
                    break;
                }
                case AnnounceStmt announceStmt:
                    context.Write(output, $"{CompilationContext.SchedulerVar}.announce(");
                    WriteExpr(context, output, flowContext.pcScope, announceStmt.PEvent);
                    context.Write(output, ", ");
                    if (announceStmt.Payload == null)
                        context.Write(output, "null");
                    else {
                        context.Write(output, "new UnionVS(");
                        WriteExpr(context, output, flowContext.pcScope, announceStmt.Payload);
                        context.Write(output, ")");
                    }
                    context.WriteLine(output, ");");
                    break;
                case ReceiveSplitStmt splitStmt:
                    var signature = splitStmt.Cont.Signature;
                    context.WriteLine(output, $"this.receive(\"{context.GetContinuationName(splitStmt.Cont)}\", {flowContext.pcScope.PathConstraintVar});");
                    break;
                default:
                    throw new NotImplementedException($"Statement type '{stmt.GetType().Name}' is not supported, found in {function.Name}");
            }
        }

        private void WriteContinuation(CompilationContext context, StringWriter output, Continuation continuation)
        {
            var voidReturn = continuation.Signature.ReturnType.IsSameTypeAs(PrimitiveType.Null);
            if (!voidReturn)
            {
                throw new NotImplementedException($"Receive statement in a function with non-void return type is not supported. Found in function named {continuation.ParentFunction.Name}.");
            }

            context.Write(output, $"void clear_{context.GetContinuationName(continuation)}() ");
            context.WriteLine(output, "{");
            foreach (var param in continuation.StoreParameters)
            {
                context.WriteLine(output, $"{GetSymbolicType(param.Type, true)} {CompilationContext.GetVar(param.Name)} = {GetDefaultValueNoGuard(context, param.Type)};");
            }
            context.WriteLine(output, "}");
            var rootPCScope = context.FreshPathConstraintScope();

            var continuationName = context.GetContinuationName(continuation);

            context.WriteLine(output, $"Guard "); // return type
            context.Write(output, continuationName);

            context.WriteLine(output, $"(");
            context.WriteLine(output, $"Guard {rootPCScope.PathConstraintVar},");
            context.Write(output, $"EventBuffer {CompilationContext.EffectCollectionVar}");
            context.WriteLine(output, ",");
            context.Write(output, "EventHandlerReturnReason outcome");
            context.WriteLine(output, ",");
            var messageName = $"{continuationName}_msg";
            context.WriteLine(output, $"Message {messageName}");
            context.WriteLine(output);
            context.Write(output, ") ");

            context.WriteLine(output, "{");

            var funcContext = ControlFlowContext.FreshFuncContext(context, rootPCScope);
            var continuationLocalParams = new HashSet<string>();
            foreach (var local in continuation.LocalParameters)
            {
                continuationLocalParams.Add(local.Name);
                context.Write(output, $"{GetSymbolicType(local.Type, true)} {CompilationContext.GetVar(local.Name)}");
                context.WriteLine(output, $"= {CompilationContext.GetVar(continuation.StoreForLocal[local].Name)}.restrict({rootPCScope.PathConstraintVar});");
            }
            var idx = 0;
            context.WriteLine(output, $"Guard deferGuard = {rootPCScope.PathConstraintVar};");
            foreach (var (key, value) in continuation.Cases)
            {
                var args = new List<IPExpr>();
                context.WriteLine(output, $"Guard cond_{idx} = {messageName}.getEvent().getGuardFor({key.Name});");
                context.WriteLine(output, $"Message {messageName}_{idx} = {messageName}.restrict(cond_{idx});");
                context.WriteLine(output, $"if (!{messageName}_{idx}.isEmptyVS())");
                context.WriteLine(output, "{");
                context.WriteLine(output, $"{CompilationContext.EffectCollectionVar}.unblock({messageName}_{idx});");
                context.WriteLine(output, $"deferGuard = deferGuard.and(cond_{idx}.not());");
                var caseScope = context.FreshPathConstraintScope();
                context.WriteLine(output, $"Guard {caseScope.PathConstraintVar} = {rootPCScope.PathConstraintVar}.and(cond_{idx});");
                var caseContext = ControlFlowContext.FreshFuncContext(context, caseScope);
                if (value.Signature.Parameters.Count > 0)
                {
                    if (value.Signature.Parameters.Count > 1)
                    {
                        throw new NotImplementedException($"Too many parameters ({value.Signature.Parameters.Count}) in receive case");
                    }
                    var arg =value.Signature.Parameters[0];
                    var argValue = new Variable($"{arg.Name}_payload", continuation.SourceLocation, VariableRole.Param);
                    argValue.Type = PrimitiveType.Any;
                    context.WriteLine(output, $"UnionVS var_{arg.Name}_payload = {messageName}_{idx}.restrict({caseScope.PathConstraintVar}).getPayload();");
                    var assignMsg = new AssignStmt(continuation.SourceLocation, new VariableAccessExpr(continuation.SourceLocation, arg), new VariableAccessExpr(continuation.SourceLocation, argValue));
                    context.WriteLine(output, $"{GetSymbolicType(arg.Type)} {CompilationContext.GetVar(arg.Name)} = {GetDefaultValue(context, caseScope, arg.Type)};");
                    WriteStmt(continuation, context, output, caseContext, assignMsg);
                }
                foreach (var local in value.LocalVariables)
                {
                    if (!continuationLocalParams.Contains(local.Name))
                    {
                        context.WriteLine(output, $"{GetSymbolicType(local.Type)} {CompilationContext.GetVar(local.Name)} = {GetDefaultValue(context, caseScope, local.Type)};");
                    }
                }
                WriteStmt(continuation, context, output, caseContext, value.Body);
                context.WriteLine(output, "}");
                idx++;
            }
            context.WriteLine(output, $"if (!deferGuard.isFalse())");
            context.WriteLine(output, "{");
            context.Write(output, $"for (GuardedValue<Event> e : {messageName}.restrict(deferGuard).getEvent().getGuardedValues())");
            context.WriteLine(output, "{");
            context.WriteLine(output, $"new DeferEventHandler(e.getValue()).handleEvent(e.getGuard(), this, {messageName}.restrict(e.getGuard()).getPayload(), outcome);");
            context.WriteLine(output, "}");
            context.WriteLine(output, "}");
            if (continuation.After != null)
            {
                var afterCaseScope = context.FreshPathConstraintScope();
                context.WriteLine(output, $"Guard {afterCaseScope.PathConstraintVar} = {rootPCScope.PathConstraintVar}.and(deferGuard.not());");
                context.WriteLine(output, $"if (!{afterCaseScope.PathConstraintVar}.isFalse())");
                context.WriteLine(output, "{");
                var afterCaseContext = ControlFlowContext.FreshFuncContext(context, afterCaseScope);
                WriteStmt(continuation, context, output, afterCaseContext, continuation.After);
                context.WriteLine(output, "}");
            }
            context.WriteLine(output, "return deferGuard;");
            context.WriteLine(output, "}");
            context.WriteLine(output);
            context.ReturnType = null;
        }

        private void CheckIsSupportedAssignment(PLanguageType valueType, PLanguageType locationType)
        {
            var valueIsMachineRef = valueType.IsSameTypeAs(PrimitiveType.Machine) || valueType is PermissionType;
            var locationIsMachineRef = locationType.IsSameTypeAs(PrimitiveType.Machine) || locationType is PermissionType;

            if (valueIsMachineRef && locationIsMachineRef)
                return;

            if (locationType.IsSameTypeAs(PrimitiveType.Any) || valueType.IsSameTypeAs(PrimitiveType.Any))
                return;

            if (locationType.IsSameTypeAs(PrimitiveType.Data) || valueType.IsSameTypeAs(PrimitiveType.Data))
                return;

            valueType = valueType.Canonicalize();
            locationType = locationType.Canonicalize();
            if ((valueType is NamedTupleType) && locationType is NamedTupleType)
            {
                var valueTupleType = (NamedTupleType) valueType;
                var locationTupleType = (NamedTupleType) locationType;

                if (valueTupleType.Fields.Count != locationTupleType.Fields.Count)
                    throw new NotImplementedException(
                        $"Cannot yet handle assignment to variable of type {locationType.CanonicalRepresentation} " +
                        $"from value of type {valueType.CanonicalRepresentation}");

                for(var i = 0; i < valueTupleType.Fields.Count; i++)
                {
                    if (!valueTupleType.Fields[i].Name.Equals(locationTupleType.Fields[i].Name))
                    {
                        throw new NotImplementedException(
                            $"Cannot yet handle assignment to variable of type {locationType.CanonicalRepresentation} " +
                            $"from value of type {valueType.CanonicalRepresentation}");
                    }
                    CheckIsSupportedAssignment(valueTupleType.Types[i], locationTupleType.Types[i]);
                }
                return;
            }

            if ((valueType is TupleType) && locationType is TupleType)
            {
                var valueTupleType = (TupleType) valueType;
                var locationTupleType = (TupleType) locationType;

                if (valueTupleType.Types.Count != locationTupleType.Types.Count)
                    throw new NotImplementedException(
                        $"Cannot yet handle assignment to variable of type {locationType.CanonicalRepresentation} " +
                        $"from value of type {valueType.CanonicalRepresentation}");

                for(var i = 0; i < valueTupleType.Types.Count; i++)
                {
                    CheckIsSupportedAssignment(valueTupleType.Types[i], locationTupleType.Types[i]);
                }
                return;
            }

            if (!locationType.IsAssignableFrom(valueType))
            {
                throw new NotImplementedException(
                    $"Cannot yet handle assignment to variable of type {locationType.CanonicalRepresentation} " +
                    $"from value of type {valueType.CanonicalRepresentation}");
            }
        }

        private string GetInlineCastPrefix(PLanguageType valueType, PLanguageType locationType, CompilationContext context, PathConstraintScope pcScope) {
            if (valueType.Equals(locationType))
            {
                return "";
            }

            var valueIsMachineRef = valueType.IsSameTypeAs(PrimitiveType.Machine) || valueType is PermissionType;
            var locationIsMachineRef = locationType.IsSameTypeAs(PrimitiveType.Machine) || locationType is PermissionType;

            if (valueIsMachineRef && locationIsMachineRef)
                return "";

            if (locationType.IsSameTypeAs(PrimitiveType.Any) || locationType.IsSameTypeAs(PrimitiveType.Data)) {
                //return $"new UnionVS ({pcScope.PathConstraintVar}, {GetDefaultValueNoGuard(context, valueType)}.getClass(), ";
                return $"ValueSummary.castToAny({pcScope.PathConstraintVar}, ";
            }
            else if (valueType.IsSameTypeAs(PrimitiveType.Any) || valueType.IsSameTypeAs(PrimitiveType.Data))
            {
                return $"({GetSymbolicType(locationType)}) ValueSummary.castFromAny({pcScope.PathConstraintVar}, {GetDefaultValueNoGuard(context, locationType)}, ";
            }

            valueType = valueType.Canonicalize();
            locationType = locationType.Canonicalize();
            if ((valueType is NamedTupleType) && locationType is NamedTupleType)
            {
                return "";
            }

            if ((valueType is TupleType) && locationType is TupleType)
            {
                return "";
            }

            PLanguageType locationElementType = null;
            PLanguageType valueElementType = null;
            var isMap = PLanguageType.TypeIsOfKind(locationType, TypeKind.Map);
            var isSet = PLanguageType.TypeIsOfKind(locationType, TypeKind.Set);
            var isSeq = PLanguageType.TypeIsOfKind(locationType, TypeKind.Sequence);
            if (isMap)
            {
                locationElementType = ((MapType) locationType).ValueType;
                if (PLanguageType.TypeIsOfKind(valueType, TypeKind.Map))
                {
                    valueElementType = ((MapType) valueType).ValueType;
                }
            }
            else if (isSet)
            {
                locationElementType = ((SetType) locationType).ElementType;
                if (PLanguageType.TypeIsOfKind(valueType, TypeKind.Set))
                {
                    valueElementType = ((SetType) valueType).ElementType;
                }
            }
            else if (isSeq)
            {
                locationElementType = ((SequenceType) locationType).ElementType;
                if (PLanguageType.TypeIsOfKind(valueType, TypeKind.Sequence))
                {
                    valueElementType = ((SequenceType) valueType).ElementType;
                }
            }
            if (locationElementType != null && valueElementType != null)
            {
                if (locationElementType.IsSameTypeAs(PrimitiveType.Any) || locationElementType.IsSameTypeAs(PrimitiveType.Data))
                {
                    //return $"new UnionVS ({pcScope.PathConstraintVar}, {GetDefaultValueNoGuard(context, valueType)}.getClass(), ";
                    return $"({GetSymbolicType(locationType)}) ValueSummary.castToAnyCollection({pcScope.PathConstraintVar}, ";
                }
                else if (valueElementType.IsSameTypeAs(PrimitiveType.Any) || valueElementType.IsSameTypeAs(PrimitiveType.Data))
                {
                    return $"({GetSymbolicType(locationType)}) ValueSummary.castFromAnyCollection({pcScope.PathConstraintVar}, {GetDefaultValueNoGuard(context, locationElementType)}, ";
                }
            }

            if (locationType.IsAssignableFrom(valueType))
            {
                return "";
            }

            throw new NotImplementedException(
                $"Cannot yet handle casting to variable of type {locationType.CanonicalRepresentation} " +
                $"from value of type {valueType.CanonicalRepresentation}");
        }

        private void WriteForeignFunCallStmt(CompilationContext context, StringWriter output, ControlFlowContext flowContext, Function function, IReadOnlyList<IPExpr> args, IPExpr dest=null)
        {
            var returnConvention = GetReturnConvention(function);
            string returnTemp = null;
            switch (returnConvention)
            {
                case FunctionReturnConvention.RETURN_VALUE:
                    returnTemp = context.FreshTempVar();
                    context.Write(output, $"{GetSymbolicType(function.Signature.ReturnType)} {returnTemp} = ({GetSymbolicType(function.Signature.ReturnType)})");
                    break;
                case FunctionReturnConvention.RETURN_VOID:
                    break;
                default:
                    throw new NotImplementedException("Cannot handle foreign function calls that can exit or return BDDs");
            }

/*
            string lambdaArgs = "";
            for (int i = 0; i < args.Count(); i++)
            {
                if (i > 0)
                {
                    lambdaArgs = lambdaArgs + ",";
                }
                lambdaArgs = lambdaArgs + $"x{i}";
            }
*/

            context.Write(output, $"ForeignFunctionInvoker.invoke({flowContext.pcScope.PathConstraintVar}, ");
            switch (returnConvention)
            {
                case FunctionReturnConvention.RETURN_VALUE:
                    context.Write(output, $"{GetDefaultValueNoGuard(context, function.Signature.ReturnType)}, x -> ");
                    // context.Write(output, $"{GetGenericSymbolicType(function.Signature.ReturnType)}.class, x -> ");
                    context.Write(output, "{ return ");
                    context.Write(output, $"wrapper__{context.GetNameForDecl(function)}(x);");
                    context.Write(output, " }");
                    break;
                case FunctionReturnConvention.RETURN_VOID:
                    context.Write(output, $"x -> wrapper__{context.GetNameForDecl(function)}(x)");
                    break;
                default:
                    throw new NotImplementedException("Cannot handle foreign function calls that can exit or return BDDs");
            }
            // context.Write(output, $"({lambdaArgs}) -> wrapper__{context.GetNameForDecl(function)}({lambdaArgs})");
            for (var i = 0; i < args.Count(); i++)
            {
                var param = args.ElementAt(i);
                context.Write(output, ", ");
                WriteExpr(context, output, flowContext.pcScope, param);
            }
            context.WriteLine(output, ");");

            switch (returnConvention)
            {
                case FunctionReturnConvention.RETURN_VALUE:
                    if (dest != null)
                        WriteWithLValueMutationContext(context, output, flowContext.pcScope, dest, false, (lhs) => context.WriteLine(output, $"{lhs} = {returnTemp};"));
                    break;
                case FunctionReturnConvention.RETURN_VOID:
                    Debug.Assert(dest == null);
                    break;
                default:
                    throw new NotImplementedException("Cannot handle foreign function calls that can exit or return BDDs");
            }
        }

        private void WriteFunCallStmt(CompilationContext context, StringWriter output, ControlFlowContext flowContext, Function function, IReadOnlyList<IPExpr> args, IPExpr dest=null)
        {
            // var isAsync = function.CanReceive == true;
            // if (isAsync && !(function is WhileFunction))
            // {
            //     throw new NotImplementedException($"Calls to async methods not yet supported in {function.Name}");
            // }
            if (function.IsForeign)
            {
                WriteForeignFunCallStmt(context, output, flowContext, function, args, dest);
                return;
            }

            var returnConvention = GetReturnConvention(function);
            string returnTemp = null;
            switch (returnConvention)
            {
                case FunctionReturnConvention.RETURN_VALUE:
                case FunctionReturnConvention.RETURN_VALUE_OR_EXIT:
                    returnTemp = context.FreshTempVar();
                    context.Write(output, $"{GetSymbolicType(function.Signature.ReturnType)} {returnTemp} = ");
                    break;
/*
                case FunctionReturnConvention.RETURN_VALUE_OR_EXIT:
                    returnTemp = context.FreshTempVar();
                    context.Write(output, $"{GetSymbolicType(function.Signature.ReturnType)} {returnTemp} = ");
                    break;
*/
                case FunctionReturnConvention.RETURN_GUARD:
                    returnTemp = context.FreshTempVar();
                    context.Write(output, $"Guard {returnTemp} = ");
                    break;
                case FunctionReturnConvention.RETURN_VOID:
                    break;
            }

            context.Write(output, $"{context.GetNameForDecl(function)}({flowContext.pcScope.PathConstraintVar}, {CompilationContext.EffectCollectionVar}");

            if (function.CanChangeState)
                context.Write(output, ", outcome");

            else if (function.CanRaiseEvent)
                context.Write(output, ", outcome");


            for (var i = 0; i < args.Count(); i++)
            {
                var param = args.ElementAt(i);
                var castToAny = GetSymbolicType(function.Signature.Parameters.ElementAt(i).Type, true) == "UnionVS" &&
                                GetSymbolicType(param.Type, true) != "UnionVS";
                context.Write(output, ", ");
                if (castToAny)
                {
                    context.Write(output, "new UnionVS(");
                }
                WriteExpr(context, output, flowContext.pcScope, param);
                if (castToAny) {
                    context.Write(output, ")");
                }
            }

            context.WriteLine(output, ");");

            switch (returnConvention)
            {
                case FunctionReturnConvention.RETURN_VALUE:
                case FunctionReturnConvention.RETURN_VALUE_OR_EXIT:
                    if (dest != null)
                        WriteWithLValueMutationContext(context, output, flowContext.pcScope, dest, false, (lhs) => context.WriteLine(output, $"{lhs} = {returnTemp};"));
                    break;
                case FunctionReturnConvention.RETURN_VOID:
                    Debug.Assert(dest == null);
                    break;
/*
                case FunctionReturnConvention.RETURN_VALUE_OR_EXIT:
                    context.WriteLine(output, $"{flowContext.pcScope.PathConstraintVar} = {returnTemp}.getNewPc();");

                    // Conservatively set control flow flags.
                    // It is always safe to set these flags to true, because they exist only as a performance optimization.
                    // In the future, we may want to optimize this to be more precise.
                    SetFlagsForPossibleReturn(context, output, flowContext);

                    if (dest != null)
                        WriteWithLValueMutationContext(context, output, flowContext.pcScope, dest, false, (lhs) => context.WriteLine(output, $"{lhs} = {returnTemp}.getValue();"));
                    break;
*/
                case FunctionReturnConvention.RETURN_GUARD:
                    context.WriteLine(output, $"{flowContext.pcScope.PathConstraintVar} = {returnTemp};");

                    // Conservatively set control flow flags.
                    // It is always safe to set these flags to true, because they exist only as a performance optimization.
                    // In the future, we may want to optimize this to be more precise.
                    SetFlagsForPossibleReturn(context, output, flowContext);

                    Debug.Assert(dest == null);
                    break;
            }
        }

        private void WriteWithLValueMutationContext(
            CompilationContext context,
            StringWriter output,
            PathConstraintScope pcScope,
            IPExpr lvalue,
            bool needOrigValue,
            Action<string> writeMutator)
        {
            PLanguageType elementType;
            switch (lvalue)
            {
                case MapAccessExpr mapAccessExpr:
                    elementType = mapAccessExpr.Type;
                    var indexExpr = mapAccessExpr.IndexExpr;
                    var indexType = indexExpr.Type;

                    WriteWithLValueMutationContext(
                        context,
                        output,
                        pcScope,
                        mapAccessExpr.MapExpr,
                        true,
                        mapTemp =>
                        {
                            var elementTemp = context.FreshTempVar();
                            var indexTemp = context.FreshTempVar();

                            context.Write(output, $"{GetSymbolicType(indexType)} {indexTemp} = ");
                            WriteExpr(context, output, pcScope, indexExpr);
                            context.WriteLine(output, ";");

                            context.Write(output, $"{GetSymbolicType(elementType)} {elementTemp}");
                            if (needOrigValue)
                            {
                                context.Write(output, $" = {mapTemp}.getOrDefault({indexTemp}, ");
                                context.Write(output, GetDefaultValue(context, pcScope, elementType));
                                context.WriteLine(output, ");");
                            }
                            else
                            {
                                context.WriteLine(output, ";");
                            }

                            writeMutator(elementTemp);

                            context.Write(output, $"{mapTemp} = {mapTemp}.put(");

                            PLanguageType keyType = null;
                            PLanguageType valueType = null;
                            keyType = ((MapType) mapAccessExpr.MapExpr.Type.Canonicalize()).KeyType;
                            valueType = ((MapType) mapAccessExpr.MapExpr.Type.Canonicalize()).ValueType;

                            {
                                var castPrefixKey = "";
                                if (keyType != null) {
                                    castPrefixKey = GetInlineCastPrefix(indexType, keyType, context, pcScope);
                                }
                                context.Write(output, castPrefixKey);
                                context.Write(output, $"{indexTemp}");
                                if (castPrefixKey != "") context.Write(output, ")");
                                context.Write(output, ", ");
                            }

                            {
                                var castPrefix = "";
                                if (valueType != null) {
                                    castPrefix = GetInlineCastPrefix(elementType, valueType, context, pcScope);
                                }
                                context.Write(output, castPrefix);
                                context.Write(output, $"{elementTemp}");
                                if (castPrefix != "") context.Write(output, ")");
                            }

                            context.WriteLine(output, ");");
                        }
                    );
                    break;

                case NamedTupleAccessExpr namedTupleAccessExpr:
                    WriteWithLValueMutationContext(
                        context,
                        output,
                        pcScope,
                        namedTupleAccessExpr.SubExpr,
                        true,
                        namedTupleTemp =>
                        {
                            var fieldTemp = context.FreshTempVar();
                            var fieldType = GetSymbolicType(namedTupleAccessExpr.Type);
                            context.Write(output, $"{fieldType} {fieldTemp}");
                            if (needOrigValue)
                            {
                                context.Write(output, $" = ({fieldType})");
                                var namedPrefix = GetInlineCastPrefix(namedTupleAccessExpr.Entry.Type, namedTupleAccessExpr.Type, context, pcScope);
                                context.Write(output, namedPrefix);
                                context.Write(output, $"{namedTupleTemp}.getField(\"{namedTupleAccessExpr.FieldName}\");");
                                if (namedPrefix != "") context.Write(output, ")");
                                context.WriteLine(output, ";");
                            }
                            else
                            {
                                context.WriteLine(output, ";");
                            }

                            writeMutator(fieldTemp);

                            context.WriteLine(
                                output,
                                $"{namedTupleTemp} = {namedTupleTemp}.setField(" +
                                $"\"{namedTupleAccessExpr.FieldName}\", " +
                                $"{fieldTemp});");
                        }
                    );
                    break;

                case TupleAccessExpr tupleAccessExpr:
                    WriteWithLValueMutationContext(
                        context,
                        output,
                        pcScope,
                        tupleAccessExpr.SubExpr,
                        true,
                        tupleTemp =>
                        {
                            var fieldTemp = context.FreshTempVar();
                            var fieldType = GetSymbolicType(tupleAccessExpr.Type);
                            context.Write(output, $"{fieldType} {fieldTemp}");
                            if (needOrigValue)
                            {
                                context.Write(output, $" = ({fieldType})");
                                var tuplePrefix = GetInlineCastPrefix((tupleAccessExpr.SubExpr.Type as TupleType).Types[tupleAccessExpr.FieldNo], tupleAccessExpr.Type, context, pcScope);
                                context.Write(output, tuplePrefix);
                                context.Write(output, $"{tupleTemp}.getField({tupleAccessExpr.FieldNo})");
                                if (tuplePrefix != "") context.Write(output, ")");
                                context.WriteLine(output, ";");
                            }
                            else
                            {
                                context.Write(output, ";");
                            }

                            writeMutator(fieldTemp);

                            context.WriteLine(
                                output,
                                $"{tupleTemp} = {tupleTemp}.setField(" +
                                $"{tupleAccessExpr.FieldNo}," +
                                $"{fieldTemp});");
                        }
                    );
                    break;

                case SeqAccessExpr seqAccessExpr:
                    elementType = seqAccessExpr.Type;

                    WriteWithLValueMutationContext(
                        context,
                        output,
                        pcScope,
                        seqAccessExpr.SeqExpr,
                        true,
                        seqTemp =>
                        {
                            var elementTemp = context.FreshTempVar();
                            var indexTemp = context.FreshTempVar();

                            context.Write(output, $"{GetSymbolicType(PrimitiveType.Int)} {indexTemp} = ");
                            WriteExpr(context, output, pcScope, seqAccessExpr.IndexExpr);
                            context.WriteLine(output, ";");

                            context.Write(output, $"{GetSymbolicType(elementType)} {elementTemp}");
                            if (needOrigValue)
                            {
                                context.WriteLine(output, $" = {seqTemp}.get({indexTemp});");
                            }
                            else
                            {
                                context.WriteLine(output, ";");
                            }

                            writeMutator(elementTemp);

                            context.Write(output, $"{seqTemp} = {seqTemp}.set({indexTemp}, ");

                            PLanguageType valueType = null;
                            valueType = ((SequenceType) seqAccessExpr.SeqExpr.Type.Canonicalize()).ElementType;

                            {
                                var castPrefix = "";
                                if (valueType != null) {
                                    castPrefix = GetInlineCastPrefix(elementType, valueType, context, pcScope);
                                }
                                context.Write(output, castPrefix);
                                context.Write(output, $"{elementTemp}");
                                if (castPrefix != "") context.Write(output, ")");
                            }

                            context.WriteLine(output, ");");
                        }
                    );
                    break;

                case SetAccessExpr setAccessExpr:
                    elementType = setAccessExpr.Type;

                    WriteWithLValueMutationContext(
                        context,
                        output,
                        pcScope,
                        setAccessExpr.SetExpr,
                        true,
                        setTemp =>
                        {
                            var elementTemp = context.FreshTempVar();
                            var indexTemp = context.FreshTempVar();

                            context.Write(output, $"{GetSymbolicType(PrimitiveType.Int)} {indexTemp} = ");
                            WriteExpr(context, output, pcScope, setAccessExpr.IndexExpr);
                            context.WriteLine(output, ";");

                            context.Write(output, $"{GetSymbolicType(elementType)} {elementTemp}");
                            if (needOrigValue)
                            {
                                context.WriteLine(output, $" = {setTemp}.get({indexTemp});");
                            }
                            else
                            {
                                context.WriteLine(output, ";");
                            }

                            writeMutator(elementTemp);

                            context.WriteLine(output, $"{setTemp} = {setTemp}.set({indexTemp}, {elementTemp});");
                        }
                    );
                    break;
                case VariableAccessExpr variableAccessExpr:
                    var name = variableAccessExpr.Variable.Name;
                    var type = variableAccessExpr.Variable.Type;

                    var unguarded = CompilationContext.GetVar(name);

                    var guardedTemp = context.FreshTempVar();

                    context.Write(output, $"{GetSymbolicType(variableAccessExpr.Type)} {guardedTemp}");

                    if (needOrigValue)
                    {
                        context.WriteLine(output, $" = {unguarded}.restrict({pcScope.PathConstraintVar});    ");
                    }
                    else
                    {
                        context.WriteLine(output, ";");
                    }

                    writeMutator(guardedTemp);

                    context.WriteLine(output,
                        $"{unguarded} = " +
                        $"{unguarded}.updateUnderGuard({pcScope.PathConstraintVar}, " +
                        $"{guardedTemp});");

                    break;

                default:
                    throw new ArgumentOutOfRangeException($"Expression type '{lvalue.GetType().Name}' is not an lvalue");
            }
        }

        private static string TransformPrintMessage(string message)
        {
            var sb = new StringBuilder();
            for (var i = 0; i < message.Length; i++)
            {
                if (message[i] == '\'')
                {
                    sb.Append("''");
                }
                else if (message[i] == '{')
                {
                    if (i + 1 == message.Length)
                    {
                        throw new ArgumentException("unmatched opening brace", nameof(message));
                    }

                    if (message[i + 1] == '{')
                    {
                        // handle "{{"
                        i++;
                        sb.Append("'{'");
                    }
                    else if (char.IsDigit(message[i + 1]))
                    {
                        sb.Append("{");
                        while (++i < message.Length && '0' <= message[i] && message[i] <= '9')
                        {
                            sb.Append(message[i]);
                        }

                        if (i == message.Length || message[i] != '}')
                        {
                            throw new ArgumentException("unmatched opening brace in position expression",
                                nameof(message));
                        }
                        sb.Append("}");
                    }
                    else
                    {
                        throw new ArgumentException("opening brace not followed by digits", nameof(message));
                    }
                }
                else if (message[i] == '}')
                {
                    if (i + 1 == message.Length || message[i + 1] != '}')
                    {
                        throw new ArgumentException("unmatched closing brace", nameof(message));
                    }

                    // handle "}}"
                    sb.Append("'}'");
                    i++;
                }
                else
                {
                    sb.Append(message[i]);
                }
            }

            return sb.ToString();
        }

        private void WriteExpr(CompilationContext context, StringWriter output, PathConstraintScope pcScope, IPExpr expr)
        {
            PLanguageType elementType = null;
            switch (expr)
            {
                case CloneExpr cloneExpr:
                    WriteExpr(context, output, pcScope, cloneExpr.Term);
                    break;
                case UnaryOpExpr unaryOpExpr:
                    var lambdaTemp = context.FreshTempVar();
                    context.Write(output, "(");
                    WriteExpr(context, output, pcScope, unaryOpExpr.SubExpr);
                    context.Write(output, $").apply(({lambdaTemp}) -> {UnOpToStr(unaryOpExpr.Operation)}{lambdaTemp})");
                    break;
                case BinOpExpr binOpExpr:
                    var isPrimitive = binOpExpr.Lhs.Type.Canonicalize() is PrimitiveType && binOpExpr.Rhs.Type.Canonicalize() is PrimitiveType;
                    var isEnum = binOpExpr.Lhs.Type.Canonicalize() is EnumType && binOpExpr.Rhs.Type.Canonicalize() is EnumType;
                    var isEquality = binOpExpr.Operation == BinOpType.Eq || binOpExpr.Operation == BinOpType.Neq;
                    var isString = isPrimitive && binOpExpr.Lhs.Type.IsSameTypeAs(PrimitiveType.String);

                    if (!(isPrimitive || isEquality))
                    {
                        var str = $"lhs type: {binOpExpr.Lhs}, rhs type: {binOpExpr.Rhs}" ;
                        throw new NotImplementedException("Binary operations are currently only supported between primitive types and enums | " + str);
                    }

                    if (isEquality)
                    {
                        if (binOpExpr.Operation == BinOpType.Neq)
                        {
                            context.Write(output, "(");
                        }
                        WriteExpr(context, output, pcScope, binOpExpr.Lhs);
                        context.Write(output, ".symbolicEquals(");

                        {
                            var castPrefix = "";
                            castPrefix = GetInlineCastPrefix(binOpExpr.Rhs.Type, binOpExpr.Lhs.Type, context, pcScope);
                            context.Write(output, castPrefix);
                            WriteExpr(context, output, pcScope, binOpExpr.Rhs);
                            if (castPrefix != "") context.Write(output, ")");
                        }

                        context.Write(output, $", {pcScope.PathConstraintVar})");
                        if (binOpExpr.Operation == BinOpType.Neq)
                        {
                            context.Write(output, ").apply(x -> !x)");
                        }
                    }
                    else
                    {
                        var lhsLambdaTemp = context.FreshTempVar();
                        var rhsLambdaTemp = context.FreshTempVar();

                        context.Write(output, "(");
                        WriteExpr(context, output, pcScope, binOpExpr.Lhs);
                        context.Write(output, ").apply(");
                        if (binOpExpr.Rhs is NullLiteralExpr)
                            context.Write(output, $"{GetDefaultValue(context, pcScope, binOpExpr.Lhs.Type)}.");
                        else WriteExpr(context, output, pcScope, binOpExpr.Rhs);
                        string lambda;
                        if (binOpExpr.Operation == BinOpType.Eq)
                            lambda = $"{lhsLambdaTemp}.equals({rhsLambdaTemp})";
                        else if (binOpExpr.Operation == BinOpType.Neq)
                            lambda = $"!{lhsLambdaTemp}.equals({rhsLambdaTemp})";
                        else if (isString)
                            lambda = BinOpForStringToStr(binOpExpr.Operation, $"{lhsLambdaTemp}", $"{rhsLambdaTemp}");
                        else
                            lambda = $"{lhsLambdaTemp} {BinOpToStr(binOpExpr.Operation)} {rhsLambdaTemp}";
                        context.Write(
                            output,
                            $", ({lhsLambdaTemp}, {rhsLambdaTemp}) -> " +
                            lambda +
                            ")"
                        );
                    }
                    break;
                case BoolLiteralExpr boolLiteralExpr:
                {
                    var unguarded = $"new { GetSymbolicType(PrimitiveType.Bool) }" + $"({boolLiteralExpr.Value})".ToLower();
                    var guarded = $"{unguarded}.restrict({pcScope.PathConstraintVar})";
                    context.Write(output, guarded);
                    break;
                }
                case CastExpr castExpr:
                    if (castExpr.SubExpr is NullLiteralExpr)
                    {
                        context.Write(output,  GetDefaultValue(context, pcScope, castExpr.Type));
                    } else
                    {
                        var castPrefix = GetInlineCastPrefix(castExpr.SubExpr.Type, castExpr.Type, context, pcScope);
                        context.Write(output, castPrefix);
                        WriteExpr(context, output, pcScope, castExpr.SubExpr);
                        if (castPrefix != "") context.Write(output, ")");
                    }
                    break;
                case CoerceExpr coerceExpr:
                    switch (coerceExpr.Type.Canonicalize())
                    {
                        case PrimitiveType oldType when oldType.IsSameTypeAs(PrimitiveType.Float):
                            context.Write(output, "(");
                            WriteExpr(context, output, pcScope, coerceExpr.SubExpr);
                            context.Write(output, $").apply(x -> x.floatValue())");
                            break;
                        case PrimitiveType oldType when oldType.IsSameTypeAs(PrimitiveType.Int):
                            context.Write(output, "(");
                            WriteExpr(context, output, pcScope, coerceExpr.SubExpr);
                            context.Write(output, $").apply(x -> x.intValue())");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(
                                @"unexpected coercion operation to:" + coerceExpr.Type.CanonicalRepresentation);
                    }
                    break;
                case DefaultExpr defaultExpr:
                    context.Write(output, GetDefaultValue(context, pcScope, defaultExpr.Type));
                    break;
                case FloatLiteralExpr floatLiteralExpr:
                {
                    var unguarded = $"new { GetSymbolicType(PrimitiveType.Float) }({floatLiteralExpr.Value}f)";
                    var guarded = $"{unguarded}.restrict({pcScope.PathConstraintVar})";
                    context.Write(output, guarded);
                    break;
                }
                case IntLiteralExpr intLiteralExpr:
                {
                    var unguarded = $"new { GetSymbolicType(PrimitiveType.Int) }({intLiteralExpr.Value})";
                    var guarded = $"{unguarded}.restrict({pcScope.PathConstraintVar})";
                    context.Write(output, guarded);
                    break;
                }
                case KeysExpr keyExpr:
                    var keyArgType = (MapType) keyExpr.Expr.Type.Canonicalize();
                    WriteExpr(context, output, pcScope, keyExpr.Expr);
                    context.Write(output, $".getKeys().restrict({pcScope.PathConstraintVar})");
                    break;
                case ValuesExpr valuesExpr:
                    WriteExpr(context, output, pcScope, valuesExpr.Expr);
                    context.Write(output, $".getValues().restrict({pcScope.PathConstraintVar})");
                    break;
                case MapAccessExpr mapAccessExpr:
                    WriteExpr(context, output, pcScope, mapAccessExpr.MapExpr);
                    context.Write(output, ".get(");
                    // context.Write(output, ".getOrDefault(");
                    WriteExpr(context, output, pcScope, mapAccessExpr.IndexExpr);
                    // context.Write(output, ", ");
                    // context.Write(output, GetDefaultValue(context, pcScope, mapAccessExpr.Type));
                    context.Write(output, ")");
                    break;
                case SeqAccessExpr seqAccessExpr:
                    WriteExpr(context, output, pcScope, seqAccessExpr.SeqExpr);
                    context.Write(output, ".get(");
                    WriteExpr(context, output, pcScope, seqAccessExpr.IndexExpr);
                    context.Write(output, ")");
                    break;
                case SetAccessExpr setAccessExpr:
                    WriteExpr(context, output, pcScope, setAccessExpr.SetExpr);
                    context.Write(output, ".get(");
                    WriteExpr(context, output, pcScope, setAccessExpr.IndexExpr);
                    context.Write(output, ")");
                    break;
                case NamedTupleAccessExpr namedTupleAccessExpr:
                    context.Write(output, $"(({GetSymbolicType(namedTupleAccessExpr.Type)})(");
                    var prefix = GetInlineCastPrefix(namedTupleAccessExpr.Entry.Type, namedTupleAccessExpr.Type, context, pcScope);
                    context.Write(output, prefix);
                    context.Write(output, "(");
                    WriteExpr(context, output, pcScope, namedTupleAccessExpr.SubExpr);
                    context.Write(output, $").getField(\"{namedTupleAccessExpr.FieldName}\")))");
                    if (prefix != "") context.Write(output, ")");
                    break;
                case ThisRefExpr thisRefExpr:
                    context.Write(
                        output,
                        $"new PrimitiveVS<Machine>(this).restrict(" +
                        $"{pcScope.PathConstraintVar})");
                    break;
                case TupleAccessExpr tupleAccessExpr:
                    context.Write(output, $"({GetSymbolicType(tupleAccessExpr.Type)})(");
                    var tupleType = (tupleAccessExpr.SubExpr.Type.Canonicalize() as TupleType);
                    var tuplePrefix = GetInlineCastPrefix(tupleType.Types[tupleAccessExpr.FieldNo], tupleAccessExpr.Type, context, pcScope);
                    context.Write(output, "(");
                    context.Write(output, tuplePrefix);
                    WriteExpr(context, output, pcScope, tupleAccessExpr.SubExpr);
                    context.Write(output, $").getField({tupleAccessExpr.FieldNo}))");
                    if (tuplePrefix != "") context.Write(output, ")");
                    break;
                case NamedTupleExpr namedTupleExpr:
                    context.Write(output, "new NamedTupleVS(");
                    var fields = (namedTupleExpr.Type.Canonicalize() as NamedTupleType).Fields;
                    var nttype = namedTupleExpr.Type as NamedTupleType;
                    for (var i = 0; i < namedTupleExpr.TupleFields.Count; i++)
                    {
                        var fieldName = fields[i].Name;
                        var field = namedTupleExpr.TupleFields[i];
                        context.Write(output, $"\"{fieldName}\", ");
                        var castExpr = new CastExpr(field.SourceLocation, field, nttype.Types[i]);
                        WriteExpr(context, output, pcScope, castExpr);
                        if (i + 1 != namedTupleExpr.TupleFields.Count)
                            context.Write(output, ", ");
                    }
                    context.Write(output, ")");
                    break;
                case UnnamedTupleExpr unnamedTupleExpr:
                    context.Write(output, "new TupleVS(");
                    var ttype = (TupleType) unnamedTupleExpr.Type;
                    for (var i = 0; i < unnamedTupleExpr.TupleFields.Count; i++)
                    {
                        var castExpr = new CastExpr(unnamedTupleExpr.SourceLocation, unnamedTupleExpr.TupleFields[i], ttype.Types[i]);
                        WriteExpr(context, output, pcScope, castExpr);
                        if (i + 1 != unnamedTupleExpr.TupleFields.Count)
                            context.Write(output, ", ");
                    }
                    context.Write(output, ")");
                    break;
                case EnumElemRefExpr enumElemRefExpr:
                {
                    var unguarded = $"new { GetSymbolicType(PrimitiveType.Int) }({enumElemRefExpr.Value.Value} /* enum {enumElemRefExpr.Type.OriginalRepresentation} elem {enumElemRefExpr.Value.Name} */)";
                    var guarded = $"{unguarded}.restrict({pcScope.PathConstraintVar})";
                    context.Write(output, guarded);
                    break;
                }
                case EventRefExpr eventRefExpr:
                {
                    var unguarded = $"new { GetSymbolicType(PrimitiveType.Event) }({context.GetNameForDecl(eventRefExpr.Value)})";
                    var guarded = $"{unguarded}.restrict({pcScope.PathConstraintVar})";
                    context.Write(output, guarded);
                    break;
                }
                case VariableAccessExpr variableAccessExpr:
                    context.Write(output,
                        $"{CompilationContext.GetVar(variableAccessExpr.Variable.Name)}.restrict(" +
                        $"{pcScope.PathConstraintVar})");
                    break;
                case FunCallExpr funCallExpr:
                    throw new InvalidOperationException("Compilation of call expressions should be handled as part of assignment statements");
                case ContainsExpr containsExpr:
                    var isMap = PLanguageType.TypeIsOfKind(containsExpr.Collection.Type, TypeKind.Map);
                    var isSet = PLanguageType.TypeIsOfKind(containsExpr.Collection.Type, TypeKind.Set);
                    if (isMap) {
                        elementType = ((MapType) containsExpr.Collection.Type.Canonicalize()).KeyType;
                    } else if (isSet) {
                        elementType = ((SetType) containsExpr.Collection.Type.Canonicalize()).ElementType;
                    } else {
                        elementType = ((SequenceType) containsExpr.Collection.Type.Canonicalize()).ElementType;
                    }

                    WriteExpr(context, output, pcScope, containsExpr.Collection);

                    if (isMap)
                        context.Write(output, ".containsKey(");
                    else
                        context.Write(output, ".contains(");

                {
                    var castPrefix = "";
                    if (elementType != null) {
                        castPrefix = GetInlineCastPrefix(containsExpr.Item.Type, elementType, context, pcScope);
                    }
                    context.Write(output, castPrefix);
                    WriteExpr(context, output, pcScope, containsExpr.Item);
                    if (castPrefix != "") context.Write(output, ")");
                }
                    context.Write(output, ")");
                    break;
                case CtorExpr ctorExpr:
                    WriteCtorExpr(context, output, pcScope, ctorExpr.Interface, ctorExpr.Arguments);
                    break;
                case NondetExpr _:
                case FairNondetExpr _:
                    context.Write(output, $"{CompilationContext.SchedulerVar}.getNextBoolean({pcScope.PathConstraintVar})");
                    break;
                case ChooseExpr chooseExpr:
                    if (chooseExpr.SubExpr == null)
                    {
                        context.Write(output, $"({CompilationContext.SchedulerVar}.getNextBoolean({pcScope.PathConstraintVar}))");
                        return;
                    }
                    switch (chooseExpr.SubExpr.Type.Canonicalize())
                    {
                        case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Int):
                            context.Write(output, $"{CompilationContext.SchedulerVar}.getNextInteger(");
                            WriteExpr(context, output, pcScope, chooseExpr.SubExpr);
                            context.Write(output, $", {pcScope.PathConstraintVar})");
                            break;
                        case SequenceType sequenceType:
                            context.Write(output, $"({GetSymbolicType(sequenceType.ElementType)}) {CompilationContext.SchedulerVar}.getNextElement(");
                            WriteExpr(context, output, pcScope, chooseExpr.SubExpr);
                            context.Write(output, $", {pcScope.PathConstraintVar})");
                            break;
                        case SetType setType:
                            context.Write(output, $"({GetSymbolicType(setType.ElementType)}) {CompilationContext.SchedulerVar}.getNextElement(");
                            WriteExpr(context, output, pcScope, chooseExpr.SubExpr);
                            context.Write(output, $", {pcScope.PathConstraintVar})");
                            break;
                        case MapType mapType:
                            context.Write(output, $"({GetSymbolicType(mapType.KeyType)}) {CompilationContext.SchedulerVar}.getNextElement(");
                            WriteExpr(context, output, pcScope, chooseExpr.SubExpr);
                            context.Write(output, $", {pcScope.PathConstraintVar})");
                            break;
                        default:
                            throw new NotImplementedException($"Cannot handle choose on expressions of type {chooseExpr.SubExpr.Type}.");
                    }
                    break;
                case SizeofExpr sizeOfExpr:
                    WriteExpr(context, output, pcScope, sizeOfExpr.Expr);
                    context.Write(output, ".size()");
                    break;
                case StringExpr stringExpr:
                    var baseString = stringExpr.BaseString;
                    if (stringExpr.Args.Count != 0)
                    {
                        baseString = TransformPrintMessage(baseString);
                    }
                    context.Write(output, $"new { GetSymbolicType(PrimitiveType.String) }(StringVS.formattedStringVS({pcScope.PathConstraintVar}, \"{baseString}\"");
                    foreach(var arg in stringExpr.Args)
                    {
                        context.Write(output, ", ");
                        WriteExpr(context, output, pcScope, arg);
                    }
                    context.Write(output, "))");
                    context.Write(output, $".restrict({pcScope.PathConstraintVar})");
                    break;
                case NullLiteralExpr nullLiteralExpr:
                    context.Write(output, "null");
                    break;
                default:
                    context.Write(output, $"/* Skipping expr '{expr.GetType().Name}' */");
                    break;
            }
        }

        private void WriteCtorExpr(CompilationContext context, StringWriter output, PathConstraintScope pcScope, Interface ctorInterface, IReadOnlyList<IPExpr> ctorArguments)
        {
            // TODO: Is it safe to take an interface's name and treat it as if it were a machine's name?
            context.Write(
                output,
                $"{CompilationContext.EffectCollectionVar}.create(" +
                $"{pcScope.PathConstraintVar}, " +
                $"{CompilationContext.SchedulerVar}, " +
                $"{context.GetNameForDecl(ctorInterface)}.class, ");

            if (ctorArguments.Count == 1)
            {
                Debug.Assert(ctorArguments.Count == 1);
                context.Write(output, "new UnionVS (");
                WriteExpr(context, output, pcScope, ctorArguments[0]);
                context.Write(output, "), ");
            }
            else if (ctorArguments.Count > 1)
            {
                context.Write(output, "new UnionVS (");
                context.Write(output, "new TupleVS (");
                for (var i = 0; i < ctorArguments.Count; i++)
                {
                    WriteExpr(context, output, pcScope, ctorArguments[i]);
                    if (i != ctorArguments.Count - 1) {
                        context.Write(output, ", ");
                    }
                }
                context.Write(output, ")), ");
            }

            context.Write(
                output,
                $"(i) -> new {context.GetNameForDecl(ctorInterface)}(i))");
        }

        // TODO: This is copied from PSharpCodeGenerator.cs.  Should we factor this out into some common location?
        private string BinOpToStr(BinOpType binOpType)
        {
            switch (binOpType)
            {
                case BinOpType.Add:
                    return "+";
                case BinOpType.Sub:
                    return "-";
                case BinOpType.Mul:
                    return "*";
                case BinOpType.Div:
                    return "/";
                case BinOpType.Mod:
                    return "%";
                case BinOpType.Lt:
                    return "<";
                case BinOpType.Le:
                    return "<=";
                case BinOpType.Gt:
                    return ">";
                case BinOpType.Ge:
                    return ">=";
                case BinOpType.And:
                    return "&&";
                case BinOpType.Or:
                    return "||";
                case BinOpType.Eq:
                    return "==";
                case BinOpType.Neq:
                    return "!=";
                default:
                    throw new ArgumentOutOfRangeException(nameof(binOpType), binOpType, null);
            }
        }

        private string BinOpForStringToStr(BinOpType binOpType, string lhs, string rhs)
        {
            switch (binOpType)
            {
                case BinOpType.Add:
                    return $"{lhs}.concat({rhs})";
                case BinOpType.Lt:
                    return $"{lhs}.compareTo({rhs}) < 0";
                case BinOpType.Le:
                    return $"{lhs}.compareTo({rhs}) <= 0";
                case BinOpType.Gt:
                    return $"{lhs}.compareTo({rhs}) > 0";
                case BinOpType.Ge:
                    return $"{lhs}.compareTo({rhs}) >= 0";
                default:
                    throw new ArgumentOutOfRangeException(nameof(binOpType), binOpType, null);
            }
        }

        // TODO: This is copied from PSharpCodeGenerator.cs.  Should we refactor this into some common location?
        private static string UnOpToStr(UnaryOpType operation)
        {
            switch (operation)
            {
                case UnaryOpType.Negate:
                    return "-";
                case UnaryOpType.Not:
                    return "!";
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation));
            }
        }

        private IPExpr convertToStringExpr(IPExpr expr)
        {
            switch (expr.Type)
            {
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.String):
                    return expr;
                default:
                    var newListArgs = new List<IPExpr>();
                    newListArgs.Add(expr);
                    return new StringExpr(expr.SourceLocation, "{0}", newListArgs);
            }
        }

        private string GetConcreteBoxedType(PLanguageType type)
        {
            switch (type.Canonicalize())
            {
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Bool):
                    return "Boolean";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Int):
                    return "Integer";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Float):
                    return "Float";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Event):
                    return "Event";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Any):
                    return "String";
                case DataType _:
                    return "String";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.String):
                    return "String";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Machine):
                case PermissionType _:
                    return "Machine";
                case TypeDefType typeDefType:
                    return $"{typeDefType.CanonicalRepresentation}";
                case ForeignType foreignType:
                    return $"{foreignType.CanonicalRepresentation}";
                case NamedTupleType _:
                    return "String";
                case TupleType _:
                    return "String";
                case EnumType _:
                    return "Integer";
                default:
                    return "Object";
            }
        }

        private string GetConcreteForeignBoxedType(PLanguageType type)
        {
            switch (type.Canonicalize())
            {
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Bool):
                    return "PBool";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Int):
                    return "PInt";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Float):
                    return "PFloat";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.String):
                    return "PString";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Machine):
                case PermissionType _:
                    return "PMachineValue";
                case ForeignType foreignType:
                    return foreignType.CanonicalRepresentation;
                case SequenceType _:
                    return "PSeq";
                case MapType _:
                    return "PMap";
                case NamedTupleType _:
                    return "PNamedTuple";
                case TupleType _:
                    return "PTuple";
                case EnumType _:
                    return "PEnum";
                default:
                    return "Object";
            }
        }

        private string GetGenericSymbolicType(PLanguageType type) {
            switch (type.Canonicalize())
            {
                case PrimitiveType _:
                    return "PrimitiveVS";
                case PermissionType _:
                    return "PrimVS";
                case TypeDefType _:
                    return "PrimitiveVS";
                case ForeignType _:
                    return "PrimitiveVS";
                case SequenceType _:
                    return "ListVS";
                case MapType _:
                    return "MapVS";
                case NamedTupleType _:
                    return "NamedTupleVS";
                case TupleType _:
                    return "TupleVS";
                case EnumType _:
                    return "PrimitiveVS /* enum {enumType.OriginalRepresentation} */";
                case SetType _:
                    return "SetVS";
                default:
                    throw new NotImplementedException($"Symbolic type '{type.OriginalRepresentation}' not supported");
            }
        }


        private string GetSymbolicType(PLanguageType type, bool isVar = false)
        {
            switch (type.Canonicalize())
            {
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Bool):
                    return "PrimitiveVS<Boolean>";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Int):
                    return "PrimitiveVS<Integer>";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Float):
                    return "PrimitiveVS<Float>";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Null):
                    if (isVar)
                        throw new NotImplementedException("Variables of type 'null' not yet supported");
                    else
                        return "void";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Event):
                    return "PrimitiveVS<Event>";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Any):
                    return "UnionVS";
                case DataType _:
                    return "UnionVS";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.String):
                    return "PrimitiveVS<String>";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Machine):
                case PermissionType _:
                    return "PrimitiveVS<Machine>";
                case TypeDefType typeDefType:
                    return $"PrimitiveVS<{typeDefType.CanonicalRepresentation}>";
                case ForeignType foreignType:
                    return $"PrimitiveVS<{foreignType.CanonicalRepresentation}>";
                case SequenceType sequenceType:
                    return $"ListVS<{GetSymbolicType(sequenceType.ElementType, true)}>";
                case MapType mapType:
                    return $"MapVS<" +
                           $"{GetConcreteBoxedType(mapType.KeyType)}, " +
                           $"{GetSymbolicType(mapType.KeyType, true)}, " +
                           $"{GetSymbolicType(mapType.ValueType, true)}>";
                case NamedTupleType _:
                    return "NamedTupleVS";
                case TupleType _:
                    return "TupleVS";
                case SetType setType:
                    return $"SetVS<{GetSymbolicType(setType.ElementType, true)}>";
                case EnumType enumType:
                    return $"PrimitiveVS<Integer> /* enum {enumType.OriginalRepresentation} */";
                default:
                    throw new NotImplementedException($"Symbolic type '{type.OriginalRepresentation}' not supported");
            }
        }

        private string GetDefaultValue(CompilationContext context, PathConstraintScope pcScope, PLanguageType type)
        {
            var unguarded = GetDefaultValueNoGuard(context, type);
            var guarded = $"{unguarded}.restrict({pcScope.PathConstraintVar})";
            return guarded;
        }

        private string GetDefaultValueNoGuard(CompilationContext context, PLanguageType type) {
            switch (type.Canonicalize())
            {
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Bool):
                    return $"new {GetSymbolicType(type)}(false)";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Int):
                    return $"new {GetSymbolicType(type)}(0)";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Float):
                    return $"new {GetSymbolicType(type)}(0.0f)";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Event):
                    return $"new {GetSymbolicType(type)}((Event) null)";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Any):
                    return $"new {GetSymbolicType(type)}()";
                case DataType _:
                    return $"new {GetSymbolicType(type)}()";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.String):
                    return $"new {GetSymbolicType(type)}(\"\")";
                case ForeignType foreignType:
                    return $"new {GetSymbolicType(type)}()";
                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Machine):
                case PermissionType _:
                    return $"new {GetSymbolicType(type)}((Machine) null)";
                case SequenceType _:
                    return $"new {GetSymbolicType(type)}(Guard.constTrue())";
                case MapType _:
                    return $"new {GetSymbolicType(type)}(Guard.constTrue())";
                case EnumType enumType:
                    return $"new {GetSymbolicType(type)}({enumType.EnumDecl.Values.Min(elem => elem.Value)})";
                case NamedTupleType namedTupleType:
                {
                    var allFieldDefaults = new List<string>();
                    foreach (var field in namedTupleType.Fields)
                    {
                        var fieldDefault = GetDefaultValueNoGuard(context, field.Type);
                        allFieldDefaults.Add($"\"{field.Name}\", {fieldDefault}");
                    }
                    return $"new {GetSymbolicType(type)}({string.Join(", ", allFieldDefaults)})";
                }
                case TupleType tupleType:
                {
                    var allFieldDefaults = new List<string>();
                    foreach (var field in tupleType.Types)
                    {
                        var fieldDefault = GetDefaultValueNoGuard(context, field);
                        allFieldDefaults.Add(fieldDefault);
                    }
                    return $"new {GetSymbolicType(type)}({string.Join(", ", allFieldDefaults)})";
                }
                case SetType _:
                    return $"new {GetSymbolicType(type)}(Guard.constTrue())";
                default:
                    throw new NotImplementedException($"Default value for symbolic type '{type.OriginalRepresentation}' not supported");
            }
        }

        private void WriteSourcePrologue(CompilationContext context, StringWriter output)
        {
            context.WriteLine(output, "package psym.model;");
            context.WriteLine(output);
            context.WriteLine(output, "import psym.commandline.*;");
            context.WriteLine(output, "import psym.runtime.*;");
            context.WriteLine(output, "import psym.runtime.logger.*;");
            context.WriteLine(output, "import psym.runtime.machine.*;");
            context.WriteLine(output, "import psym.runtime.machine.buffer.*;");
            context.WriteLine(output, "import psym.runtime.machine.eventhandlers.*;");
            context.WriteLine(output, "import psym.runtime.machine.events.*;");
            context.WriteLine(output, "import psym.runtime.scheduler.*;");
            context.WriteLine(output, "import psym.runtime.values.*;");
            context.WriteLine(output, "import psym.utils.*;");
            context.WriteLine(output, "import psym.utils.serialize.*;");
            context.WriteLine(output, "import psym.valuesummary.*;");
            context.WriteLine(output, "import java.util.List;");
            context.WriteLine(output, "import java.util.ArrayList;");
            context.WriteLine(output, "import java.util.Map;");
            context.WriteLine(output, "import java.util.HashMap;");
            context.WriteLine(output, "import java.util.function.Consumer;");
            context.WriteLine(output, "import java.util.function.Function;");
            context.WriteLine(output, "import java.text.MessageFormat;");
            context.WriteLine(output, "import lombok.Generated;");
            context.WriteLine(output);
            context.WriteLine(output, $"public class {context.MainClassName} implements Program {{");
            context.WriteLine(output);
        }

        private void WriteSourceEpilogue(CompilationContext context, StringWriter output)
        {
            context.WriteLine(output, "}");
        }
    }
}