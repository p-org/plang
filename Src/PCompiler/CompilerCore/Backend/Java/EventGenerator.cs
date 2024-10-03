using System.Collections.Generic;
using System.Linq;
using Plang.Compiler.TypeChecker.AST.Declarations;

namespace Plang.Compiler.Backend.Java
{
    internal class EventGenerator : JavaSourceGenerator {

        internal EventGenerator(ICompilerConfiguration job, string filename) : base(job, filename)
        {
        }

        /// <summary>
        /// Generates Java code for a given compilation job's used events.
        ///
        /// We only emit code for events that are actually used in Monitors (i.e. they
        /// appear in at least one Monitor's `observes` set.)
        /// </summary>
        protected override void GenerateCodeImpl()
        {
            WriteLine($"public class {Constants.EventNamespaceName} {{");
            foreach (var e in monitoredEvents(GlobalScope.Machines))
            {
                WriteEventDecl(e);
                WriteLine();
            }
            WriteLine("}");
        }


        private IEnumerable<PEvent> monitoredEvents(IEnumerable<Machine> machines)
        {
            var events = new HashSet<PEvent>();

            foreach (var m in machines.Where(m => m.IsSpec))
            {
                foreach (var e in m.Observes.Events)
                {
                    events.Add(e);
                }
            }

            return events;
        }

        private void WriteEventDecl(PEvent e)
        {
            var eventName = Names.GetNameForDecl(e);
            var argType = Types.JavaTypeFor(e.PayloadType);

            var payloadType = argType.TypeName;
            var payloadRefType = argType.ReferenceTypeName;

            WriteLine($"public static class {eventName} extends {Constants.PEventsClass}<{payloadRefType}> implements Serializable {{");

            var hasPayload = !(argType is TypeManager.JType.JVoid);
            if (hasPayload)
            {
                WriteLine($"public {eventName}({payloadType} p) {{ this.payload = p; }}");
                WriteLine($"private {payloadType} payload; ");
                WriteLine($"public {payloadRefType} getPayload() {{ return payload; }}");
            }
            else
            {
                WriteLine($"public {eventName}() {{ }}");
                WriteLine($"public Void getPayload() {{ return null; }}");
            }

            WriteLine();
            WriteLine("@Override");
            Write("public String toString() {");
            if (hasPayload)
            {
                WriteLine($" return \"{eventName}[\" + payload + \"]\"; }}");
            }
            else
            {
                WriteLine($" return \"{eventName}\"; }}");
            }

            WriteLine($"}} // {eventName}");
        }
    }
}