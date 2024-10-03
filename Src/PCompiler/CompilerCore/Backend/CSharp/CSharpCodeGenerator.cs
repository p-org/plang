using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Plang.Compiler.Backend.ASTExt;
using Plang.Compiler.TypeChecker;
using Plang.Compiler.TypeChecker.AST;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.TypeChecker.AST.Expressions;
using Plang.Compiler.TypeChecker.AST.Statements;
using Plang.Compiler.TypeChecker.AST.States;
using Plang.Compiler.TypeChecker.Types;

namespace Plang.Compiler.Backend.CSharp
{
    public class CSharpCodeGenerator : ICodeGenerator
    {

        /// <summary>
        /// This compiler has a compilation stage.
        /// </summary>
        public bool HasCompilationStage => true;

        private int _sendEventIndex = 0;

        public void Compile(ICompilerConfiguration job)
        {
            var csprojName = $"{job.ProjectName}.csproj";
            var csprojPath = Path.Combine(job.OutputDirectory.FullName, csprojName);
            var mainFilePath = Path.Combine(job.OutputDirectory.FullName, "Test.cs");
            var stdout = "";
            var stderr = "";

            // create the .csproj file
            var csprojTemplate = Constants.csprojTemplate;
            csprojTemplate = csprojTemplate.Replace("-directory-",
                Path.GetRelativePath(job.ProjectRootPath.FullName, job.OutputDirectory.FullName));

            string foreignInclude = "";
            var foreignFiles = job.InputForeignFiles.Where(x => x.EndsWith(".cs"));
            if (foreignFiles.Any())
            {
                foreignInclude += "  <ItemGroup>\n";
                foreach (var fileName in foreignFiles)
                {
                    foreignInclude += $"    <Compile Include=\"{fileName}\"/>\n";
                }
                foreignInclude += "  </ItemGroup>";
            }
            csprojTemplate = csprojTemplate.Replace("-foreign-include-", foreignInclude);

            string assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            csprojTemplate = csprojTemplate.Replace("-assembly-path-", assemblyPath);

            File.WriteAllText(csprojPath, csprojTemplate);

            // if the Main file does not exist then create the file
            if (!File.Exists(mainFilePath))
            {
                var mainCode = Constants.mainCode.Replace("-projectName-", job.ProjectName);
                File.WriteAllText(mainFilePath, mainCode);
            }
            else
            {
                job.Output.WriteInfo("Reusing existing " + mainFilePath);
            }

            // compile the csproj file
            var args = new[] { "build --no-incremental -c Release", csprojName };

            var exitCode = Compiler.RunWithOutput(job.OutputDirectory.FullName, out stdout, out stderr, "dotnet", args);
            if (exitCode != 0)
            {
                throw new TranslationException($"Compiling generated C# code FAILED!\n" + $"{stdout}\n" + $"{stderr}\n");
            }
            else
            {
                job.Output.WriteInfo($"{stdout}");
            }
        }

        public IEnumerable<CompiledFile> GenerateCode(ICompilerConfiguration job, Scope globalScope)
        {
            var context = new CompilationContext(job);
            var csharpSource = GenerateSource(context, globalScope);
            return new List<CompiledFile> { csharpSource };
        }

        private CompiledFile GenerateSource(CompilationContext context, Scope globalScope)
        {
            var source = new CompiledFile(context.FileName);

            WriteSourcePrologue(context, source.Stream);

            // write the top level declarations
            foreach (var decl in globalScope.AllDecls)
            {
                WriteDecl(context, source.Stream, decl);
            }

            // write the interface declarations
            WriteInitializeInterfaces(context, source.Stream, globalScope.Interfaces);

            // write the enum declarations
            WriteInitializeEnums(context, source.Stream, globalScope.Enums);

            WriteSourceEpilogue(context, source.Stream);

            return source;
        }

        private void WriteInitializeInterfaces(CompilationContext context, StringWriter output,
            IEnumerable<Interface> interfaces)
        {
            WriteNameSpacePrologue(context, output);

            //create the interface declarations
            var ifaces = interfaces.ToList();
            foreach (var iface in ifaces)
            {
                context.WriteLine(output, $"public class {context.Names.GetNameForDecl(iface)} : PMachineValue {{");
                context.WriteLine(output,
                    $"public {context.Names.GetNameForDecl(iface)} (ActorId machine, List<string> permissions) : base(machine, permissions) {{ }}");
                context.WriteLine(output, "}");
                context.WriteLine(output);
            }

            //initialize the interfaces
            context.WriteLine(output, "public partial class PHelper {");
            context.WriteLine(output, "public static void InitializeInterfaces() {");
            context.WriteLine(output, "PInterfaces.Clear();");
            foreach (var iface in ifaces)
            {
                context.Write(output, $"PInterfaces.AddInterface(nameof({context.Names.GetNameForDecl(iface)})");
                foreach (var ev in iface.ReceivableEvents.Events)
                {
                    context.Write(output, ", ");
                    context.Write(output, $"nameof({context.Names.GetNameForDecl(ev)})");
                }

                context.WriteLine(output, ");");
            }

            context.WriteLine(output, "}");
            context.WriteLine(output, "}");
            context.WriteLine(output);

            WriteNameSpaceEpilogue(context, output);
        }

        private void WriteSourcePrologue(CompilationContext context, StringWriter output)
        {
            context.WriteLine(output, "using PChecker;");
            context.WriteLine(output, "using PChecker.Actors;");
            context.WriteLine(output, "using PChecker.Actors.Events;");
            context.WriteLine(output, "using PChecker.Runtime;");
            context.WriteLine(output, "using PChecker.Specifications;");
            context.WriteLine(output, "using System;");
            context.WriteLine(output, "using System.Runtime;");
            context.WriteLine(output, "using System.Collections.Generic;");
            context.WriteLine(output, "using System.Linq;");
            context.WriteLine(output, "using System.IO;");
            context.WriteLine(output, "using PChecker.PRuntime.Values;");
            context.WriteLine(output, "using PChecker.PRuntime;");
            context.WriteLine(output, "using PChecker.PRuntime.Exceptions;");
            context.WriteLine(output, "using System.Threading;");
            context.WriteLine(output, "using System.Threading.Tasks;");
            context.WriteLine(output);
            context.WriteLine(output, "#pragma warning disable 162, 219, 414, 1998");
            context.WriteLine(output, $"namespace PImplementation");
            context.WriteLine(output, "{");
            context.WriteLine(output, "}");
        }

        private void WriteSourceEpilogue(CompilationContext context, StringWriter output)
        {
            context.WriteLine(output, "#pragma warning restore 162, 219, 414");
        }

        private void WriteNameSpacePrologue(CompilationContext context, StringWriter output)
        {
            context.WriteLine(output, $"namespace PImplementation");
            context.WriteLine(output, "{");
        }

        private void WriteNameSpaceEpilogue(CompilationContext context, StringWriter output)
        {
            context.WriteLine(output, "}");
        }

        private void WriteDecl(CompilationContext context, StringWriter output, IPDecl decl)
        {
            string declName;
            switch (decl)
            {
                case Function function:
                    if (!function.IsForeign)
                    {
                        context.WriteLine(output, $"namespace PImplementation");
                        context.WriteLine(output, "{");
                        context.WriteLine(output, $"public static partial class {context.GlobalFunctionClassName}");
                        context.WriteLine(output, "{");
                        WriteFunction(context, output, function);
                        context.WriteLine(output, "}");
                        context.WriteLine(output, "}");
                    }

                    break;

                case PEvent pEvent:
                    if (!pEvent.IsBuiltIn)
                    {
                        WriteEvent(context, output, pEvent);
                    }

                    break;

                case Machine machine:
                    if (machine.IsSpec)
                    {
                        WriteMonitor(context, output, machine);
                    }
                    else
                    {
                        WriteMachine(context, output, machine);
                    }

                    break;

                case PEnum _:
                    break;

                case TypeDef typeDef:
                    if (typeDef.Type is ForeignType foreignType)
                    {
                        WriteForeignType(context, output, foreignType);
                    }

                    break;

                case Implementation impl:
                    WriteImplementationDecl(context, output, impl);
                    break;

                case SafetyTest safety:
                    WriteSafetyTestDecl(context, output, safety);
                    break;

                case Interface _:
                    break;

                case EnumElem _:
                    break;

                default:
                    declName = context.Names.GetNameForDecl(decl);
                    context.WriteLine(output, $"// TODO: {decl.GetType().Name} {declName}");
                    break;
            }
        }

        private void WriteMonitor(CompilationContext context, StringWriter output, Machine machine)
        {
            WriteNameSpacePrologue(context, output);

            var declName = context.Names.GetNameForDecl(machine);
            context.WriteLine(output, $"internal partial class {declName} : PMonitor");
            context.WriteLine(output, "{");

            foreach (var field in machine.Fields)
            {
                context.WriteLine(output,
                    $"private {GetCSharpType(field.Type)} {context.Names.GetNameForDecl(field)} = {GetDefaultValue(field.Type)};");
            }

            WriteMonitorConstructor(context, output, machine);

            foreach (var method in machine.Methods)
            {
                WriteFunction(context, output, method);
            }

            foreach (var state in machine.States)
            {
                WriteState(context, output, state);
            }

            context.WriteLine(output, "}");

            WriteNameSpaceEpilogue(context, output);
        }

        private void WriteMonitorConstructor(CompilationContext context, StringWriter output, Machine machine)
        {
            var declName = context.Names.GetNameForDecl(machine);
            context.WriteLine(output, $"static {declName}() {{");
            foreach (var sEvent in machine.Observes.Events)
            {
                context.WriteLine(output, $"observes.Add(nameof({context.Names.GetNameForDecl(sEvent)}));");
            }

            context.WriteLine(output, "}");
            context.WriteLine(output);
        }

        private static void WriteForeignType(CompilationContext context, StringWriter output, ForeignType foreignType)
        {
            // we do not generate code for foreign types
            var declName = foreignType.CanonicalRepresentation;
            context.WriteLine(output, $"// TODO: Implement the Foreign Type {declName}");
        }

        private void WriteSafetyTestDecl(CompilationContext context, StringWriter output, SafetyTest safety)
        {
            WriteNameSpacePrologue(context, output);

            context.WriteLine(output, $"public class {context.Names.GetNameForDecl(safety)} {{");
            WriteInitializeLinkMap(context, output, safety.ModExpr.ModuleInfo.LinkMap);
            WriteInitializeInterfaceDefMap(context, output, safety.ModExpr.ModuleInfo.InterfaceDef);
            WriteInitializeMonitorObserves(context, output, safety.ModExpr.ModuleInfo.MonitorMap.Keys);
            WriteInitializeMonitorMap(context, output, safety.ModExpr.ModuleInfo.MonitorMap);
            WriteTestFunction(context, output, safety.Main);
            context.WriteLine(output, "}");

            WriteNameSpaceEpilogue(context, output);
        }

        private void WriteImplementationDecl(CompilationContext context, StringWriter output, Implementation impl)
        {
            WriteNameSpacePrologue(context, output);

            context.WriteLine(output, $"public class {context.Names.GetNameForDecl(impl)} {{");
            WriteInitializeLinkMap(context, output, impl.ModExpr.ModuleInfo.LinkMap);
            WriteInitializeInterfaceDefMap(context, output, impl.ModExpr.ModuleInfo.InterfaceDef);
            WriteInitializeMonitorObserves(context, output, impl.ModExpr.ModuleInfo.MonitorMap.Keys);
            WriteInitializeMonitorMap(context, output, impl.ModExpr.ModuleInfo.MonitorMap);
            WriteTestFunction(context, output, impl.Main);
            context.WriteLine(output, "}");

            WriteNameSpaceEpilogue(context, output);
        }

        private void WriteInitializeMonitorObserves(CompilationContext context, StringWriter output,
            ICollection<Machine> monitors)
        {
            context.WriteLine(output, "public static void InitializeMonitorObserves() {");
            context.WriteLine(output, "PModule.monitorObserves.Clear();");
            foreach (var monitor in monitors)
            {
                context.WriteLine(output,
                    $"PModule.monitorObserves[nameof({context.Names.GetNameForDecl(monitor)})] = new List<string>();");
                foreach (var ev in monitor.Observes.Events)
                {
                    context.WriteLine(output,
                        $"PModule.monitorObserves[nameof({context.Names.GetNameForDecl(monitor)})].Add(nameof({context.Names.GetNameForDecl(ev)}));");
                }
            }

            context.WriteLine(output, "}");
            context.WriteLine(output);
        }

        private void WriteInitializeEnums(CompilationContext context, StringWriter output, IEnumerable<PEnum> enums)
        {
            WriteNameSpacePrologue(context, output);
            //initialize the interfaces
            context.WriteLine(output, "public partial class PHelper {");
            context.WriteLine(output, "public static void InitializeEnums() {");
            context.WriteLine(output, "PrtEnum.Clear();");
            foreach (var enumDecl in enums)
            {
                var enumElemNames =
                    $"new [] {{{string.Join(",", enumDecl.Values.Select(e => $"\"{context.Names.GetNameForDecl(e)}\""))}}}";
                var enumElemValues = $"new [] {{{string.Join(",", enumDecl.Values.Select(e => e.Value))}}}";
                context.WriteLine(output, $"PrtEnum.AddEnumElements({enumElemNames}, {enumElemValues});");
            }

            context.WriteLine(output, "}");
            context.WriteLine(output, "}");
            context.WriteLine(output);

            WriteNameSpaceEpilogue(context, output);
        }

        private void WriteTestFunction(CompilationContext context, StringWriter output, string main)
        {
            context.WriteLine(output);
            context.WriteLine(output, "[PChecker.SystematicTesting.Test]");
            context.WriteLine(output, "public static void Execute(IActorRuntime runtime) {");
            context.WriteLine(output, "runtime.RegisterLog(new PLogFormatter());");
            context.WriteLine(output, "runtime.RegisterLog(new PJsonFormatter());");
            context.WriteLine(output, "PModule.runtime = runtime;");
            context.WriteLine(output, "PHelper.InitializeInterfaces();");
            context.WriteLine(output, "PHelper.InitializeEnums();");
            context.WriteLine(output, "InitializeLinkMap();");
            context.WriteLine(output, "InitializeInterfaceDefMap();");
            context.WriteLine(output, "InitializeMonitorMap(runtime);");
            context.WriteLine(output, "InitializeMonitorObserves();");
            context.WriteLine(output,
                $"runtime.CreateActor(typeof(_GodMachine), new _GodMachine.Config(typeof({main})));");
            context.WriteLine(output, "}");
        }

        private void WriteInitializeMonitorMap(CompilationContext context, StringWriter output,
            IDictionary<Machine, IEnumerable<Interface>> monitorMap)
        {
            // compute the reverse map
            var machineMap = new Dictionary<Interface, List<Machine>>();
            foreach (var monitorToInterface in monitorMap)
            {
                foreach (var iface in monitorToInterface.Value)
                {
                    if (!machineMap.ContainsKey(iface))
                    {
                        machineMap[iface] = new List<Machine>();
                    }

                    machineMap[iface].Add(monitorToInterface.Key);
                }
            }

            context.WriteLine(output, "public static void InitializeMonitorMap(IActorRuntime runtime) {");
            context.WriteLine(output, "PModule.monitorMap.Clear();");
            foreach (var machine in machineMap)
            {
                context.WriteLine(output, $"PModule.monitorMap[nameof({context.Names.GetNameForDecl(machine.Key)})] = new List<Type>();");
                foreach (var monitor in machine.Value)
                {
                    context.WriteLine(output,
                        $"PModule.monitorMap[nameof({context.Names.GetNameForDecl(machine.Key)})].Add(typeof({context.Names.GetNameForDecl(monitor)}));");
                }
            }

            foreach (var monitor in monitorMap.Keys)
            {
                context.WriteLine(output, $"runtime.RegisterMonitor<{context.Names.GetNameForDecl(monitor)}>();");
            }

            context.WriteLine(output, "}");
            context.WriteLine(output);
        }

        private void WriteInitializeInterfaceDefMap(CompilationContext context, StringWriter output,
            IDictionary<Interface, Machine> interfaceDef)
        {
            context.WriteLine(output, "public static void InitializeInterfaceDefMap() {");
            context.WriteLine(output, "PModule.interfaceDefinitionMap.Clear();");
            foreach (var map in interfaceDef)
            {
                context.WriteLine(output,
                    $"PModule.interfaceDefinitionMap.Add(nameof({context.Names.GetNameForDecl(map.Key)}), typeof({context.Names.GetNameForDecl(map.Value)}));");
            }

            context.WriteLine(output, "}");
            context.WriteLine(output);
        }

        private void WriteInitializeLinkMap(CompilationContext context, StringWriter output,
            IDictionary<Interface, IDictionary<Interface, Interface>> linkMap)
        {
            context.WriteLine(output, "public static void InitializeLinkMap() {");
            context.WriteLine(output, "PModule.linkMap.Clear();");
            foreach (var creatorInterface in linkMap)
            {
                context.WriteLine(output,
                    $"PModule.linkMap[nameof({context.Names.GetNameForDecl(creatorInterface.Key)})] = new Dictionary<string, string>();");
                foreach (var clinkMap in creatorInterface.Value)
                {
                    context.WriteLine(output,
                        $"PModule.linkMap[nameof({context.Names.GetNameForDecl(creatorInterface.Key)})].Add(nameof({context.Names.GetNameForDecl(clinkMap.Key)}), nameof({context.Names.GetNameForDecl(clinkMap.Value)}));");
                }
            }

            context.WriteLine(output, "}");
            context.WriteLine(output);
        }

        private void WriteEvent(CompilationContext context, StringWriter output, PEvent pEvent)
        {
            WriteNameSpacePrologue(context, output);

            var declName = context.Names.GetNameForDecl(pEvent);

            // initialize the payload type
            var payloadType = GetCSharpType(pEvent.PayloadType, true);
            context.WriteLine(output, $"internal partial class {declName} : PEvent");
            context.WriteLine(output, "{");
            context.WriteLine(output, $"public {declName}() : base(-1) {{}}");
            context.WriteLine(output, $"public {declName}({payloadType} payload, int loc): base(payload, loc)" + "{ }");
            context.WriteLine(output, $"public override IPrtValue Clone() {{ return new {declName}();}}");
            context.WriteLine(output, "}");

            WriteNameSpaceEpilogue(context, output);
        }

        private void WriteMachine(CompilationContext context, StringWriter output, Machine machine)
        {
            WriteNameSpacePrologue(context, output);

            var declName = context.Names.GetNameForDecl(machine);
            context.WriteLine(output, $"internal partial class {declName} : PMachine");
            context.WriteLine(output, "{");

            foreach (var field in machine.Fields)
            {
                context.WriteLine(output,
                    $"private {GetCSharpType(field.Type)} {context.Names.GetNameForDecl(field)} = {GetDefaultValue(field.Type)};");
            }

            //create the constructor event
            var cTorType = GetCSharpType(machine.PayloadType, true);
            context.Write(output, "public class ConstructorEvent : PEvent");
            context.Write(output, "{");
            context.Write(output, $"public ConstructorEvent({cTorType} val, int loc) : base(val, loc) {{ }}");
            context.WriteLine(output, "}");
            context.WriteLine(output);

            context.WriteLine(output,
                $"protected override Event GetConstructorEvent(IPrtValue value) {{ return new ConstructorEvent(({cTorType})value, {_sendEventIndex++}); }}");

            // create the constructor to initialize the sends, creates and receives list
            WriteMachineConstructor(context, output, machine);

            foreach (var method in machine.Methods)
            {
                WriteFunction(context, output, method);
            }

            foreach (var state in machine.States)
            {
                WriteState(context, output, state);
            }

            context.WriteLine(output, "}");

            WriteNameSpaceEpilogue(context, output);
        }

        private static void WriteMachineConstructor(CompilationContext context, StringWriter output, Machine machine)
        {
            var declName = context.Names.GetNameForDecl(machine);
            context.WriteLine(output, $"public {declName}() {{");
            foreach (var sEvent in machine.Sends.Events)
            {
                context.WriteLine(output, $"this.sends.Add(nameof({context.Names.GetNameForDecl(sEvent)}));");
            }

            foreach (var rEvent in machine.Receives.Events)
            {
                context.WriteLine(output, $"this.receives.Add(nameof({context.Names.GetNameForDecl(rEvent)}));");
            }

            foreach (var iCreate in machine.Creates.Interfaces)
            {
                context.WriteLine(output, $"this.creates.Add(nameof({context.Names.GetNameForDecl(iCreate)}));");
            }

            context.WriteLine(output, "}");
            context.WriteLine(output);
        }

        private void WriteState(CompilationContext context, StringWriter output, State state)
        {
            if (state.IsStart && !state.OwningMachine.IsSpec)
            {
                context.WriteLine(output, "[Start]");
                context.WriteLine(output, "[OnEntry(nameof(InitializeParametersFunction))]");
                context.WriteLine(output,
                    $"[OnEventGotoState(typeof(ConstructorEvent), typeof({context.Names.GetNameForDecl(state)}))]");
                context.WriteLine(output, "class __InitState__ : State { }");
                context.WriteLine(output);
            }

            if (state.IsStart && state.OwningMachine.IsSpec)
            {
                context.WriteLine(output, "[Start]");
            }

            if (state.OwningMachine.IsSpec)
            {
                if (state.Temperature == StateTemperature.Cold)
                {
                    context.WriteLine(output, "[Cold]");
                }
                else if (state.Temperature == StateTemperature.Hot)
                {
                    context.WriteLine(output, "[Hot]");
                }
            }

            if (state.Entry != null)
            {
                context.WriteLine(output, $"[OnEntry(nameof({context.Names.GetNameForDecl(state.Entry)}))]");
            }

            var deferredEvents = new List<string>();
            var ignoredEvents = new List<string>();
            foreach (var eventHandler in state.AllEventHandlers)
            {
                var pEvent = eventHandler.Key;
                var stateAction = eventHandler.Value;
                switch (stateAction)
                {
                    case EventDefer _:
                        deferredEvents.Add($"typeof({context.Names.GetNameForDecl(pEvent)})");
                        break;

                    case EventDoAction eventDoAction:
                        var targetDoFunctionName = context.Names.GetNameForDecl(eventDoAction.Target);
                        targetDoFunctionName = eventDoAction.Target.IsAnon ? targetDoFunctionName : $"_{targetDoFunctionName}";
                        context.WriteLine(
                            output,
                            $"[OnEventDoAction(typeof({context.Names.GetNameForDecl(pEvent)}), nameof({targetDoFunctionName}))]");
                        break;

                    case EventGotoState eventGotoState when eventGotoState.TransitionFunction == null:
                        context.WriteLine(
                            output,
                            $"[OnEventGotoState(typeof({context.Names.GetNameForDecl(pEvent)}), typeof({context.Names.GetNameForDecl(eventGotoState.Target)}))]");
                        break;

                    case EventGotoState eventGotoState when eventGotoState.TransitionFunction != null:
                        var targetGotoFunctionName = context.Names.GetNameForDecl(eventGotoState.TransitionFunction);
                        targetGotoFunctionName = eventGotoState.TransitionFunction.IsAnon ? targetGotoFunctionName : $"_{targetGotoFunctionName}";
                        context.WriteLine(
                            output,
                            $"[OnEventGotoState(typeof({context.Names.GetNameForDecl(pEvent)}), typeof({context.Names.GetNameForDecl(eventGotoState.Target)}), nameof({targetGotoFunctionName}))]");
                        break;

                    case EventIgnore _:
                        ignoredEvents.Add($"typeof({context.Names.GetNameForDecl(pEvent)})");
                        break;
                }
            }

            if (deferredEvents.Count > 0)
            {
                context.WriteLine(output, $"[DeferEvents({string.Join(", ", deferredEvents.AsEnumerable())})]");
            }

            if (ignoredEvents.Count > 0)
            {
                context.WriteLine(output, $"[IgnoreEvents({string.Join(", ", ignoredEvents.AsEnumerable())})]");
            }

            if (state.Exit != null)
            {
                context.WriteLine(output, $"[OnExit(nameof({context.Names.GetNameForDecl(state.Exit)}))]");
            }

            context.WriteLine(output, $"class {context.Names.GetNameForDecl(state)} : State");
            context.WriteLine(output, "{");
            context.WriteLine(output, "}");
        }

        private void WriteNamedFunctionWrapper(CompilationContext context, StringWriter output, Function function)
        {
            if (function.Role == FunctionRole.Method || function.Role == FunctionRole.Foreign)
                return;

            var isAsync = function.CanReceive;
            var signature = function.Signature;

            var functionName = context.Names.GetNameForDecl(function);
            var functionParameters = "Event currentMachine_dequeuedEvent";
            var awaitMethod = isAsync ? "await " : "";
            var asyncMethod = isAsync ? "async" : "";
            var returnType = "void";

            if (isAsync)
            {
                returnType = "Task";
            }

            context.WriteLine(output,
                $"public {asyncMethod} {returnType} {$"_{functionName}"}({functionParameters})");

            context.WriteLine(output, "{");

            //add the declaration of currentMachine
            if (function.Owner != null)
            {
                context.WriteLine(output, $"{context.Names.GetNameForDecl(function.Owner)} currentMachine = this;");
            }

            var parameter = function.Signature.Parameters.Any() ? $"({GetCSharpType(function.Signature.ParameterTypes.First())})((PEvent)currentMachine_dequeuedEvent).Payload" : "";
            context.WriteLine(output, $"{awaitMethod}{functionName}({parameter});");
            context.WriteLine(output, "}");
        }

        private void WriteFunction(CompilationContext context, StringWriter output, Function function)
        {
            if (function.IsForeign)
            {
                return;
            }

            var isStatic = function.Owner == null;

            if (!function.IsAnon && !isStatic)
            {
                WriteNamedFunctionWrapper(context, output, function);
            }

            var isAsync = function.CanReceive;
            var signature = function.Signature;

            var staticKeyword = isStatic ? "static " : "";
            var asyncKeyword = isAsync ? "async " : "";
            var returnType = function.Role != FunctionRole.Scenario ? GetCSharpType(signature.ReturnType) : "int";

            if (isAsync)
            {
                returnType = returnType == "void" ? "Task" : $"Task<{returnType}>";
            }

            var functionName = context.Names.GetNameForDecl(function);
            var functionParameters = "";
            if (function.IsAnon)
            {
                functionParameters = "Event currentMachine_dequeuedEvent";
            }
            else if (function.Role == FunctionRole.Scenario)
            {
                functionParameters = "List<Event> events";
            }
            else
            {
                functionParameters = string.Join(
                    ", ",
                    signature.Parameters.Select(param =>
                        $"{GetCSharpType(param.Type)} {context.Names.GetNameForDecl(param)}"));
            }

            if (isStatic && function.Role != FunctionRole.Scenario) // then we need to generate two versions of the function
            {
                // for machine
                var seperator = functionParameters == "" ? "" : ", ";
                var functionParameters_machine = functionParameters + string.Concat(seperator, "PMachine currentMachine");
                context.WriteLine(output,
                    $"public {staticKeyword}{asyncKeyword}{returnType} {functionName}({functionParameters_machine})");
                WriteFunctionBody(context, output, function);

                // for monitor
                if (!(function.CanCreate || function.CanSend || function.IsNondeterministic || function.CanReceive))
                {
                    var functionParameters_monitor = functionParameters + string.Concat(seperator, "PMonitor currentMachine");
                    context.WriteLine(output,
                        $"public {staticKeyword}{asyncKeyword}{returnType} {functionName}({functionParameters_monitor})");
                    WriteFunctionBody(context, output, function);
                }

            }
            else
            {
                context.WriteLine(output,
                    $"public {staticKeyword}{asyncKeyword}{returnType} {functionName}({functionParameters})");
                WriteFunctionBody(context, output, function);
            }

        }

        private void WriteFunctionBody(CompilationContext context, StringWriter output, Function function)
        {
            context.WriteLine(output, "{");

            //add the declaration of currentMachine
            if (function.Owner != null)
            {
                context.WriteLine(output, $"{context.Names.GetNameForDecl(function.Owner)} currentMachine = this;");
            }

            if (function.IsAnon)
            {
                if (function.Signature.Parameters.Any())
                {
                    var param = function.Signature.Parameters.First();
                    context.WriteLine(output,
                        $"{GetCSharpType(param.Type)} {context.Names.GetNameForDecl(param)} = ({GetCSharpType(param.Type)})(gotoPayload ?? ((PEvent)currentMachine_dequeuedEvent).Payload);");
                    context.WriteLine(output, "this.gotoPayload = null;");
                }
            }

            foreach (var local in function.LocalVariables)
            {
                var type = local.Type;
                context.WriteLine(output,
                    $"{GetCSharpType(type, true)} {context.Names.GetNameForDecl(local)} = {GetDefaultValue(type)};");
            }

            if (function.Role != FunctionRole.Scenario)
            {
                foreach (var bodyStatement in function.Body.Statements)
                {
                    WriteStmt(context: context, output: output, function: function, stmt: bodyStatement);
                }
            }
            else
            {
                WriteScenario(context, output, function);
            }


            context.WriteLine(output, "}");
        }

        private void WriteScenario(CompilationContext context, StringWriter output, Function function)
        {
            int numOfStmt = function.Body.Statements.Count + 1;
            context.WriteLine(output, $"int state = {numOfStmt};");
            var eventPredicates = string.Join(" or ", function.Signature.ParameterEvents.Select(it => it.Name));
            context.WriteLine(output, $"events = events.Where(it => it is {eventPredicates}).ToList();");
            WriteConstraintsRecursive(context, output, function, 0, new HashSet<Variable>(), 0);
            context.WriteLine(output, "return state;");
        }

        private void WriteConstraintsRecursive(CompilationContext context, StringWriter output, Function function, int index, HashSet<Variable> visitedVariables, int satisfiedConstraints)
        {
            if (index >= function.Signature.Parameters.Count)
            {
                context.WriteLine(output, "return 1;");
                return;
            }
            var param = function.Signature.Parameters[index];
            var e = function.Signature.ParameterEvents[index];
            visitedVariables.Add(param);
            var start = index == 0 ? "0" : $"i{index - 1} + 1";
            var paramName = context.Names.GetNameForDecl(param);
            context.WriteLine(output, $"for (var i{index} = {start} ; i{index} < events.Count; i{index} ++) " + "{");
            context.WriteLine(output, $"var {paramName}_obj = events[i{index}];");
            context.WriteLine(output, $"if ({paramName}_obj is not {e.Name}) continue;");
            context.WriteLine(output, $"var {paramName} = ((PEvent) {paramName}_obj).Payload;");

            foreach (var bodyStatement in function.Body.Statements)
            {
                if (bodyStatement is ConstraintStmt stmt)
                {
                    var variables = ConstraintVariableCollector.FindVariablesRecursive(stmt.Constraint);
                    if (variables.Contains(param) && visitedVariables.IsSupersetOf(variables))
                    {
                        context.Write(output, $"if (!(");
                        WriteExpr(context, output, stmt.Constraint);
                        context.WriteLine(output, $")) continue;");
                        satisfiedConstraints += 1;
                        context.WriteLine(output, $"state = Math.Min({function.Body.Statements.Count - satisfiedConstraints + 1}, state);");
                    }
                }
            }
            WriteConstraintsRecursive(context, output, function, index + 1, visitedVariables, satisfiedConstraints);
            context.WriteLine(output, "}");
        }


        private void WriteStmt(CompilationContext context, StringWriter output, Function function, IPStmt stmt)
        {
            switch (stmt)
            {
                case AnnounceStmt announceStmt:
                    context.Write(output, "currentMachine.Announce((Event)");
                    WriteExpr(context, output, announceStmt.PEvent);
                    if (announceStmt.Payload != null)
                    {
                        context.Write(output, ", ");
                        WriteExpr(context, output, announceStmt.Payload);
                    }

                    context.WriteLine(output, ");");
                    break;

                case AssertStmt assertStmt:
                    context.Write(output, "currentMachine.TryAssert(");
                    WriteExpr(context, output, assertStmt.Assertion);
                    context.Write(output, ",");
                    context.Write(output, $"\"Assertion Failed: \" + ");
                    WriteExpr(context, output, assertStmt.Message);
                    context.WriteLine(output, ");");
                    //last statement
                    if (FunctionValidator.SurelyReturns(assertStmt))
                    {
                        context.WriteLine(output, "throw new PUnreachableCodeException();");
                    }

                    break;

                case AssignStmt assignStmt:
                    var needCtorAdapter = !assignStmt.Value.Type.IsSameTypeAs(assignStmt.Location.Type)
                                          && !PrimitiveType.Null.IsSameTypeAs(assignStmt.Value.Type)
                                          && !PrimitiveType.Any.IsSameTypeAs(assignStmt.Location.Type);
                    WriteLValue(context, output, assignStmt.Location);
                    context.Write(output, $" = ({GetCSharpType(assignStmt.Location.Type)})(");
                    if (needCtorAdapter)
                    {
                        context.Write(output, $"new {GetCSharpType(assignStmt.Location.Type)}(");
                    }

                    WriteExpr(context, output, assignStmt.Value);
                    if (needCtorAdapter)
                    {
                        if (assignStmt.Location.Type.Canonicalize() is SequenceType seqType)
                        {
                            context.Write(output, $".Cast<{GetCSharpType(seqType.ElementType)}>()");
                        }

                        context.Write(output, ")");
                    }

                    context.WriteLine(output, ");");
                    break;

                case CompoundStmt compoundStmt:
                    context.WriteLine(output, "{");
                    foreach (var subStmt in compoundStmt.Statements)
                    {
                        WriteStmt(context, output, function, subStmt);
                    }

                    context.WriteLine(output, "}");
                    break;

                case CtorStmt ctorStmt:
                    context.Write(output,
                        $"currentMachine.CreateInterface<{context.Names.GetNameForDecl(ctorStmt.Interface)}>(");
                    context.Write(output, "currentMachine");
                    if (ctorStmt.Arguments.Any())
                    {
                        context.Write(output, ", ");
                        if (ctorStmt.Arguments.Count > 1)
                        {
                            //create tuple from rvaluelist
                            context.Write(output, "new PrtTuple(");
                            var septor = "";
                            foreach (var ctorExprArgument in ctorStmt.Arguments)
                            {
                                context.Write(output, septor);
                                WriteExpr(context, output, ctorExprArgument);
                                septor = ",";
                            }

                            context.Write(output, ")");
                        }
                        else
                        {
                            WriteExpr(context, output, ctorStmt.Arguments.First());
                        }
                    }

                    context.WriteLine(output, ");");
                    break;

                case FunCallStmt funCallStmt:
                    var isStatic = funCallStmt.Function.Owner == null;
                    var awaitMethod = funCallStmt.Function.CanReceive ? "await " : "";
                    var globalFunctionClass = isStatic ? $"{context.GlobalFunctionClassName}." : "";
                    context.Write(output,
                        $"{awaitMethod}{globalFunctionClass}{context.Names.GetNameForDecl(funCallStmt.Function)}(");
                    var separator = "";

                    foreach (var param in funCallStmt.ArgsList)
                    {
                        context.Write(output, separator);
                        WriteExpr(context, output, param);
                        separator = ", ";
                    }

                    if (isStatic)
                    {
                        context.Write(output, separator + "currentMachine");
                    }

                    context.WriteLine(output, ");");
                    break;

                case GotoStmt gotoStmt:
                    //last statement
                    context.Write(output, $"currentMachine.TryGotoState<{context.Names.GetNameForDecl(gotoStmt.State)}>(");
                    if (gotoStmt.Payload != null)
                    {
                        WriteExpr(context, output, gotoStmt.Payload);
                    }

                    context.WriteLine(output, ");");
                    context.WriteLine(output, "return;");
                    break;

                case IfStmt ifStmt:
                    context.Write(output, "if (");
                    WriteExpr(context, output, ifStmt.Condition);
                    context.WriteLine(output, ")");
                    WriteStmt(context, output, function, ifStmt.ThenBranch);
                    if (ifStmt.ElseBranch != null && ifStmt.ElseBranch.Statements.Any())
                    {
                        context.WriteLine(output, "else");
                        WriteStmt(context, output, function, ifStmt.ElseBranch);
                    }

                    break;

                case AddStmt addStmt:
                    context.Write(output, "((PrtSet)");
                    WriteExpr(context, output, addStmt.Variable);
                    context.Write(output, ").Add(");
                    WriteExpr(context, output, addStmt.Value);
                    context.WriteLine(output, ");");
                    break;

                case InsertStmt insertStmt:
                    var isMap = PLanguageType.TypeIsOfKind(insertStmt.Variable.Type, TypeKind.Map);
                    var castOp = isMap ? "(PrtMap)" : "(PrtSeq)";
                    context.Write(output, $"({castOp}");
                    WriteExpr(context, output, insertStmt.Variable);
                    if (isMap)
                    {
                        context.Write(output, ").Add(");
                    }
                    else
                    {
                        context.Write(output, ").Insert(");
                    }

                    WriteExpr(context, output, insertStmt.Index);
                    context.Write(output, ", ");
                    WriteExpr(context, output, insertStmt.Value);
                    context.WriteLine(output, ");");
                    break;

                case MoveAssignStmt moveAssignStmt:
                    var upCast = "";
                    if (!moveAssignStmt.FromVariable.Type.IsSameTypeAs(moveAssignStmt.ToLocation.Type))
                    {
                        upCast = $"({GetCSharpType(moveAssignStmt.ToLocation.Type)})";
                        if (PLanguageType.TypeIsOfKind(moveAssignStmt.FromVariable.Type, TypeKind.Enum))
                        {
                            upCast = $"{upCast}(long)";
                        }
                    }

                    WriteLValue(context, output, moveAssignStmt.ToLocation);
                    context.WriteLine(output,
                        $" = {upCast}{context.Names.GetNameForDecl(moveAssignStmt.FromVariable)};");
                    break;

                case NoStmt _:
                    break;

                case PrintStmt printStmt:
                    context.Write(output, $"currentMachine.LogLine(\"\" + ");
                    WriteExpr(context, output, printStmt.Message);
                    context.WriteLine(output, ");");
                    break;

                case RaiseStmt raiseStmt:
                    //last statement
                    context.Write(output, "currentMachine.TryRaiseEvent((Event)");
                    WriteExpr(context, output, raiseStmt.PEvent);
                    if (raiseStmt.Payload.Any())
                    {
                        context.Write(output, ", ");
                        WriteExpr(context, output, raiseStmt.Payload.First());
                    }

                    context.WriteLine(output, ");");
                    context.WriteLine(output, "return;");
                    break;

                case ReceiveStmt receiveStmt:
                    var eventName = context.Names.GetTemporaryName("recvEvent");
                    var eventTypeNames = receiveStmt.Cases.Keys.Select(evt => context.Names.GetNameForDecl(evt))
                        .ToHashSet();
                    eventTypeNames.Add("PHalt"); // halt as a special case for receive
                    var recvArgs = string.Join(", ", eventTypeNames.Select(name => $"typeof({name})"));
                    context.WriteLine(output, $"var {eventName} = await currentMachine.TryReceiveEvent({recvArgs});");
                    context.WriteLine(output, $"switch ({eventName}) {{");
                    // add halt as a special case if doesnt exist
                    if (receiveStmt.Cases.All(kv => kv.Key.Name != "PHalt"))
                    {
                        context.WriteLine(output,"case PHalt _hv: { currentMachine.TryRaiseEvent(_hv); break;} ");

                    }

                    foreach (var (key, value) in receiveStmt.Cases)
                    {
                        var caseName = context.Names.GetTemporaryName("evt");
                        context.WriteLine(output, $"case {context.Names.GetNameForDecl(key)} {caseName}: {{");
                        if (value.Signature.Parameters.FirstOrDefault() is { } caseArg)
                        {
                            context.WriteLine(output,
                                $"{GetCSharpType(caseArg.Type)} {context.Names.GetNameForDecl(caseArg)} = ({GetCSharpType(caseArg.Type)})({caseName}.Payload);");
                        }

                        foreach (var local in value.LocalVariables)
                        {
                            var type = local.Type;
                            context.WriteLine(output,
                                $"{GetCSharpType(type, true)} {context.Names.GetNameForDecl(local)} = {GetDefaultValue(type)};");
                        }

                        foreach (var caseStmt in value.Body.Statements)
                        {
                            WriteStmt(context, output, function, caseStmt);
                        }

                        context.WriteLine(output, "} break;");
                    }

                    context.WriteLine(output, "}");
                    break;

                case RemoveStmt removeStmt:
                {
                    var castOperation = PLanguageType.TypeIsOfKind(removeStmt.Variable.Type, TypeKind.Map)
                        ? "(PrtMap)"
                        : PLanguageType.TypeIsOfKind(removeStmt.Variable.Type, TypeKind.Sequence)
                            ? "(PrtSeq)"
                            : "(PrtSet)";
                    context.Write(output, $"({castOperation}");
                    switch (removeStmt.Variable.Type.Canonicalize())
                    {
                        case MapType _:
                            WriteExpr(context, output, removeStmt.Variable);
                            context.Write(output, ").Remove(");
                            WriteExpr(context, output, removeStmt.Value);
                            context.WriteLine(output, ");");
                            break;

                        case SequenceType _:
                            WriteExpr(context, output, removeStmt.Variable);
                            context.Write(output, ").RemoveAt(");
                            WriteExpr(context, output, removeStmt.Value);
                            context.WriteLine(output, ");");
                            break;

                        case SetType _:
                            WriteExpr(context, output, removeStmt.Variable);
                            context.Write(output, ").Remove(");
                            WriteExpr(context, output, removeStmt.Value);
                            context.WriteLine(output, ");");
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(
                                $"Remove cannot be applied to type {removeStmt.Variable.Type.OriginalRepresentation}");
                    }
                    break;
                }

                case ReturnStmt returnStmt:
                    context.Write(output, "return ");
                    if (returnStmt.ReturnValue != null)
                    {
                        WriteExpr(context, output, returnStmt.ReturnValue);
                    }
                    context.WriteLine(output, ";");
                    break;

                case BreakStmt breakStmt:
                    context.WriteLine(output, "break;");
                    break;

                case ContinueStmt continueStmt:
                    context.WriteLine(output, "continue;");
                    break;

                case SendStmt sendStmt:
                    context.Write(output, "currentMachine.TrySendEvent(");
                    WriteExpr(context, output, sendStmt.MachineExpr);
                    context.Write(output, ", (Event)");
                    WriteExpr(context, output, sendStmt.Evt);

                    if (sendStmt.Arguments.Any())
                    {
                        context.Write(output, ", ");
                        if (sendStmt.Arguments.Count > 1)
                        {
                            //create tuple from rvaluelist
                            var argTypes = string.Join(",",
                                sendStmt.Arguments.Select(a => GetCSharpType(a.Type)));
                            var tupleType = $"PrtTuple";
                            context.Write(output, $"new {tupleType}(");
                            var septor = "";
                            foreach (var ctorExprArgument in sendStmt.Arguments)
                            {
                                context.Write(output, septor);
                                WriteExpr(context, output, ctorExprArgument);
                                septor = ",";
                            }

                            context.Write(output, ")");
                        }
                        else
                        {
                            WriteExpr(context, output, sendStmt.Arguments.First());
                        }
                    }

                    context.WriteLine(output, ");");
                    break;

                case ForeachStmt foreachStmt:
                    var tempVarName = $"__temp_{context.Names.GetNameForDecl(foreachStmt.Item)}";
                    context.Write(output, $"foreach (var {tempVarName} in ");
                    WriteExpr(context, output, foreachStmt.IterCollection);
                    context.WriteLine(output, ") {");
                    context.WriteLine(output, $"{context.Names.GetNameForDecl(foreachStmt.Item)} = ({GetCSharpType(foreachStmt.Item.Type)}) {tempVarName};");
                    WriteStmt(context, output, function, foreachStmt.Body);
                    context.WriteLine(output, "}");
                    break;

                case WhileStmt whileStmt:
                    context.Write(output, "while (");
                    WriteExpr(context, output, whileStmt.Condition);
                    context.WriteLine(output, ")");
                    WriteStmt(context, output, function, whileStmt.Body);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(stmt));
            }
        }

        private void WriteLValue(CompilationContext context, StringWriter output, IPExpr lvalue)
        {
#pragma warning disable CCN0002 // Non exhaustive patterns in switch block
            switch (lvalue)
            {
                case MapAccessExpr mapAccessExpr:
                    context.Write(output, "((PrtMap)");
                    WriteLValue(context, output, mapAccessExpr.MapExpr);
                    context.Write(output, ")[");
                    WriteExpr(context, output, mapAccessExpr.IndexExpr);
                    context.Write(output, "]");
                    break;

                case SetAccessExpr setAccessExpr:
                    context.Write(output, "((PrtSet)");
                    WriteLValue(context, output, setAccessExpr.SetExpr);
                    context.Write(output, ")[");
                    WriteExpr(context, output, setAccessExpr.IndexExpr);
                    context.Write(output, "]");
                    break;

                case NamedTupleAccessExpr namedTupleAccessExpr:
                    if (ExprVisitor.ReservedEventFeilds.ContainsValue(namedTupleAccessExpr.Entry))
                    {
                        var type = GetCSharpType(namedTupleAccessExpr.Entry.Type);
                        context.Write(output, $"(({type}) (");
                        WriteExpr(context, output, namedTupleAccessExpr.SubExpr);
                        context.Write(output, $"_obj).{namedTupleAccessExpr.FieldName})");
                    }
                    else
                    {
                        context.Write(output, "((PrtNamedTuple)");
                        WriteExpr(context, output, namedTupleAccessExpr.SubExpr);
                        context.Write(output, $")[\"{namedTupleAccessExpr.FieldName}\"]");
                    }
                    break;

                case SeqAccessExpr seqAccessExpr:
                    context.Write(output, "((PrtSeq)");
                    WriteLValue(context, output, seqAccessExpr.SeqExpr);
                    context.Write(output, ")[");
                    WriteExpr(context, output, seqAccessExpr.IndexExpr);
                    context.Write(output, "]");
                    break;

                case TupleAccessExpr tupleAccessExpr:
                    context.Write(output, "((PrtTuple)");
                    WriteExpr(context, output, tupleAccessExpr.SubExpr);
                    context.Write(output, $")[{tupleAccessExpr.FieldNo}]");
                    break;

                case VariableAccessExpr variableAccessExpr:
                    context.Write(output, context.Names.GetNameForDecl(variableAccessExpr.Variable));
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(lvalue));
            }
#pragma warning restore CCN0002 // Non exhaustive patterns in switch block
        }

        private void WriteExpr(CompilationContext context, StringWriter output, IPExpr pExpr)
        {
#pragma warning disable CCN0002 // Non exhaustive patterns in switch block
            switch (pExpr)
            {
                case CloneExpr cloneExpr:
                    WriteClone(context, output, cloneExpr.Term);
                    break;

                case BinOpExpr binOpExpr:
                    //handle eq and noteq differently
                    if (binOpExpr.Operation == BinOpType.Eq || binOpExpr.Operation == BinOpType.Neq)
                    {
                        var negate = binOpExpr.Operation == BinOpType.Neq ? "!" : "";
                        context.Write(output, $"({negate}PrtValues.SafeEquals(");
                        if (PLanguageType.TypeIsOfKind(binOpExpr.Lhs.Type, TypeKind.Enum))
                        {
                            context.Write(output, "PrtValues.Box((long) ((PrtInt)");
                            WriteExpr(context, output, binOpExpr.Lhs);
                            context.Write(output, ")),");
                        }
                        else
                        {
                            WriteExpr(context, output, binOpExpr.Lhs);
                            context.Write(output, ",");
                        }

                        if (PLanguageType.TypeIsOfKind(binOpExpr.Rhs.Type, TypeKind.Enum))
                        {
                            context.Write(output, "PrtValues.Box((long) ((PrtInt)");
                            WriteExpr(context, output, binOpExpr.Rhs);
                            context.Write(output, "))");
                        }
                        else
                        {
                            WriteExpr(context, output, binOpExpr.Rhs);
                        }

                        context.Write(output, "))");
                    }
                    else
                    {
                        context.Write(output, "(");
                        if (PLanguageType.TypeIsOfKind(binOpExpr.Lhs.Type, TypeKind.Enum))
                        {
                            context.Write(output, "(long)");
                        }

                        WriteExpr(context, output, binOpExpr.Lhs);
                        context.Write(output, $") {BinOpToStr(binOpExpr.Operation)} (");
                        if (PLanguageType.TypeIsOfKind(binOpExpr.Rhs.Type, TypeKind.Enum))
                        {
                            context.Write(output, "(long)");
                        }

                        WriteExpr(context, output, binOpExpr.Rhs);
                        context.Write(output, ")");
                    }

                    break;

                case BoolLiteralExpr boolLiteralExpr:
                    context.Write(output, $"((PrtBool){(boolLiteralExpr.Value ? "true" : "false")})");
                    break;

                case CastExpr castExpr:
                    context.Write(output, $"(({GetCSharpType(castExpr.Type)})");
                    WriteExpr(context, output, castExpr.SubExpr);
                    context.Write(output, ")");
                    break;

                case CoerceExpr coerceExpr:
                    switch (coerceExpr.Type.Canonicalize())
                    {
                        case PrimitiveType oldType when oldType.IsSameTypeAs(PrimitiveType.Float):
                        case PrimitiveType oldType1 when oldType1.IsSameTypeAs(PrimitiveType.Int):
                            context.Write(output, "(");
                            WriteExpr(context, output, coerceExpr.SubExpr);
                            context.Write(output, ")");
                            break;

                        case PermissionType _:
                            context.Write(output, "(PInterfaces.IsCoercionAllowed(");
                            WriteExpr(context, output, coerceExpr.SubExpr);
                            context.Write(output, ", ");
                            context.Write(output, $"\"I_{coerceExpr.NewType.CanonicalRepresentation}\") ?");
                            context.Write(output, "new PMachineValue(");
                            context.Write(output, "(");
                            WriteExpr(context, output, coerceExpr.SubExpr);
                            context.Write(output, ").Id, ");
                            context.Write(output,
                                $"PInterfaces.GetPermissions(\"I_{coerceExpr.NewType.CanonicalRepresentation}\")) : null)");
                            break;

                        default:
                            throw new ArgumentOutOfRangeException(
                                @"unexpected coercion operation to:" + coerceExpr.Type.CanonicalRepresentation);
                    }

                    break;

                case ChooseExpr chooseExpr:
                    if (chooseExpr.SubExpr == null)
                    {
                        context.Write(output, "((PrtBool)currentMachine.TryRandomBool())");
                    }
                    else
                    {
                        context.Write(output, $"(({GetCSharpType(chooseExpr.Type)})currentMachine.TryRandom(");
                        WriteExpr(context, output, chooseExpr.SubExpr);
                        context.Write(output, $"))");
                    }
                    break;

                case ContainsExpr containsExpr:
                    var isMap = PLanguageType.TypeIsOfKind(containsExpr.Collection.Type, TypeKind.Map);
                    var isSeq = PLanguageType.TypeIsOfKind(containsExpr.Collection.Type, TypeKind.Sequence);
                    var castOp = isMap ? "(PrtMap)"
                        : isSeq ? "(PrtSeq)"
                        : "(PrtSet)";
                    context.Write(output, "((PrtBool)(");
                    context.Write(output, $"({castOp}");
                    WriteExpr(context, output, containsExpr.Collection);
                    if (isMap)
                    {
                        context.Write(output, ").ContainsKey(");
                    }
                    else
                    {
                        context.Write(output, ").Contains(");
                    }

                    WriteExpr(context, output, containsExpr.Item);
                    context.Write(output, ")))");
                    break;

                case CtorExpr ctorExpr:
                    context.Write(output,
                        $"currentMachine.CreateInterface<{context.Names.GetNameForDecl(ctorExpr.Interface)}>( ");
                    context.Write(output, "currentMachine");
                    if (ctorExpr.Arguments.Any())
                    {
                        context.Write(output, ", ");
                        if (ctorExpr.Arguments.Count > 1)
                        {
                            //create tuple from rvaluelist
                            context.Write(output, "new PrtTuple(");
                            var septor = "";
                            foreach (var ctorExprArgument in ctorExpr.Arguments)
                            {
                                context.Write(output, septor);
                                WriteExpr(context, output, ctorExprArgument);
                                septor = ",";
                            }

                            context.Write(output, ")");
                        }
                        else
                        {
                            WriteExpr(context, output, ctorExpr.Arguments.First());
                        }
                    }

                    context.Write(output, ")");
                    break;

                case DefaultExpr defaultExpr:
                    context.Write(output, GetDefaultValue(defaultExpr.Type));
                    break;

                case EnumElemRefExpr enumElemRefExpr:
                    var enumElem = enumElemRefExpr.Value;
                    context.Write(output, $"(PrtEnum.Get(\"{context.Names.GetNameForDecl(enumElem)}\"))");
                    break;

                case EventRefExpr eventRefExpr:
                    var eventName = context.Names.GetNameForDecl(eventRefExpr.Value);
                    switch (eventName)
                    {
                        case "Halt":
                            context.Write(output, "new PHalt(" + _sendEventIndex++ + ")");
                            break;

                        case "DefaultEvent":
                            context.Write(output, "DefaultEvent.Instance");
                            break;

                        default:
                            var payloadExpr = GetDefaultValue(eventRefExpr.Value.PayloadType);
                            context.Write(output, $"new {eventName}({payloadExpr}, {_sendEventIndex++})");
                            break;
                    }

                    break;

                case FairNondetExpr _:
                    context.Write(output, "((PrtBool)currentMachine.TryRandomBool())");
                    break;

                case FloatLiteralExpr floatLiteralExpr:
                    context.Write(output, $"((PrtFloat){floatLiteralExpr.Value})");
                    break;

                case FunCallExpr funCallExpr:
                    var isStatic = funCallExpr.Function.Owner == null;
                    var awaitMethod = funCallExpr.Function.CanReceive ? "await " : "";
                    var globalFunctionClass = isStatic ? $"{context.GlobalFunctionClassName}." : "";
                    context.Write(output,
                        $"{awaitMethod}{globalFunctionClass}{context.Names.GetNameForDecl(funCallExpr.Function)}(");
                    var separator = "";

                    foreach (var param in funCallExpr.Arguments)
                    {
                        context.Write(output, separator);
                        WriteExpr(context, output, param);
                        separator = ", ";
                    }

                    if (isStatic)
                    {
                        context.Write(output, separator + "currentMachine");
                    }

                    context.Write(output, ")");
                    break;

                case IntLiteralExpr intLiteralExpr:
                    context.Write(output, $"((PrtInt)({intLiteralExpr.Value}))");
                    break;

                case KeysExpr keysExpr:
                    context.Write(output, "(");
                    WriteExpr(context, output, keysExpr.Expr);
                    context.Write(output, ").CloneKeys()");
                    break;

                case NamedTupleExpr namedTupleExpr:
                    var fieldNamesArray = string.Join(",",
                        ((NamedTupleType)namedTupleExpr.Type).Names.Select(n => $"\"{n}\""));
                    fieldNamesArray = $"new string[]{{{fieldNamesArray}}}";
                    context.Write(output, $"(new {GetCSharpType(namedTupleExpr.Type)}({fieldNamesArray}, ");
                    for (var i = 0; i < namedTupleExpr.TupleFields.Count; i++)
                    {
                        if (i > 0)
                        {
                            context.Write(output, ", ");
                        }

                        WriteExpr(context, output, namedTupleExpr.TupleFields[i]);
                    }

                    context.Write(output, "))");
                    break;

                case NondetExpr _:
                    context.Write(output, "((PrtBool)currentMachine.TryRandomBool())");
                    break;

                case NullLiteralExpr _:
                    context.Write(output, "null");
                    break;

                case SizeofExpr sizeofExpr:
                    context.Write(output, "((PrtInt)(");
                    WriteExpr(context, output, sizeofExpr.Expr);
                    context.Write(output, ").Count)");
                    break;

                case StringExpr stringExpr:
                    context.Write(output, $"((PrtString) String.Format(");
                    context.Write(output, $"\"{stringExpr.BaseString}\"");
                    foreach (var arg in stringExpr.Args)
                    {
                        context.Write(output, ",");
                        WriteExpr(context, output, arg);
                    }
                    context.Write(output, "))");
                    break;

                case ThisRefExpr _:
                    context.Write(output, "currentMachine.self");
                    break;

                case UnaryOpExpr unaryOpExpr:
                    context.Write(output, $"{UnOpToStr(unaryOpExpr.Operation)}(");
                    WriteExpr(context, output, unaryOpExpr.SubExpr);
                    context.Write(output, ")");
                    break;

                case UnnamedTupleExpr unnamedTupleExpr:
                    context.Write(output, $"new {GetCSharpType(unnamedTupleExpr.Type)}(");
                    var sep = "";
                    foreach (var field in unnamedTupleExpr.TupleFields)
                    {
                        context.Write(output, sep);
                        WriteExpr(context, output, field);
                        sep = ", ";
                    }

                    context.Write(output, ")");
                    break;

                case ValuesExpr valuesExpr:
                    context.Write(output, "(");
                    WriteExpr(context, output, valuesExpr.Expr);
                    context.Write(output, ").CloneValues()");
                    break;

                case MapAccessExpr _:
                case SetAccessExpr _:
                case NamedTupleAccessExpr _:
                case SeqAccessExpr _:
                case TupleAccessExpr _:
                case VariableAccessExpr _:
                    WriteLValue(context, output, pExpr);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(pExpr), $"type was {pExpr?.GetType().FullName}");
            }
#pragma warning restore CCN0002 // Non exhaustive patterns in switch block
        }

        private void WriteClone(CompilationContext context, StringWriter output, IExprTerm cloneExprTerm)
        {
            if (!(cloneExprTerm is IVariableRef variableRef))
            {
                WriteExpr(context, output, cloneExprTerm);
                return;
            }

            var varName = context.Names.GetNameForDecl(variableRef.Variable);
            context.Write(output, $"(({GetCSharpType(variableRef.Type)})((IPrtValue){varName})?.Clone())");
        }

        private string GetCSharpType(PLanguageType type, bool isVar = false)
        {
            switch (type.Canonicalize())
            {
                case DataType _:
                    return "IPrtValue";

                case EnumType _:
                    return "PrtInt";

                case ForeignType _:
                    return type.CanonicalRepresentation;

                case MapType _:
                    return "PrtMap";

                case NamedTupleType _:
                    return "PrtNamedTuple";

                case PermissionType _:
                    return "PMachineValue";

                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Any):
                    return "IPrtValue";

                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Bool):
                    return "PrtBool";

                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Int):
                    return "PrtInt";

                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Float):
                    return "PrtFloat";

                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.String):
                    return "PrtString";

                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Event):
                    return "PEvent";

                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Machine):
                    return "PMachineValue";

                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Null):
                    return isVar ? "IPrtValue" : "void";

                case SequenceType _:
                    return "PrtSeq";

                case SetType _:
                    return "PrtSet";

                case TupleType _:
                    return "PrtTuple";

                default:
                    throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        private string GetDefaultValue(PLanguageType returnType)
        {
            switch (returnType.Canonicalize())
            {
                case EnumType enumType:
                    return $"((PrtInt){enumType.EnumDecl.Values.Min(elem => elem.Value)})";

                case MapType mapType:
                    return $"new {GetCSharpType(mapType)}()";

                case SequenceType sequenceType:
                    return $"new {GetCSharpType(sequenceType)}()";

                case SetType setType:
                    return $"new {GetCSharpType(setType)}()";

                case NamedTupleType namedTupleType:
                    var fieldNamesArray = string.Join(",", namedTupleType.Names.Select(n => $"\"{n}\""));
                    fieldNamesArray = $"new string[]{{{fieldNamesArray}}}";
                    var fieldDefaults =
                        string.Join(", ", namedTupleType.Types.Select(t => GetDefaultValue(t)));
                    return $"(new {GetCSharpType(namedTupleType)}({fieldNamesArray},{fieldDefaults}))";

                case TupleType tupleType:
                    var defaultTupleValues =
                        string.Join(", ", tupleType.Types.Select(t => GetDefaultValue(t)));
                    return $"(new {GetCSharpType(tupleType)}({defaultTupleValues}))";

                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Bool):
                    return "((PrtBool)false)";

                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Int):
                    return "((PrtInt)0)";

                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.Float):
                    return "((PrtFloat)0.0)";

                case PrimitiveType primitiveType when primitiveType.IsSameTypeAs(PrimitiveType.String):
                    return "((PrtString)\"\")";

                case PrimitiveType eventType when eventType.IsSameTypeAs(PrimitiveType.Event):
                case PermissionType _:
                case PrimitiveType anyType when anyType.IsSameTypeAs(PrimitiveType.Any):
                case PrimitiveType machineType when machineType.IsSameTypeAs(PrimitiveType.Machine):
                case ForeignType _:
                case PrimitiveType nullType when nullType.IsSameTypeAs(PrimitiveType.Null):
                case DataType _:
                    return "null";

                case null:
                    return "";

                default:
                    throw new ArgumentOutOfRangeException(nameof(returnType));
            }
        }

        private static string UnOpToStr(UnaryOpType operation)
        {
            switch (operation)
            {
                case UnaryOpType.Negate:
                    return "-";

                case UnaryOpType.Not:
                    return "!";

                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
        }

        private static string BinOpToStr(BinOpType binOpType)
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

                default:
                    throw new ArgumentOutOfRangeException(nameof(binOpType), binOpType, null);
            }
        }
    }
}