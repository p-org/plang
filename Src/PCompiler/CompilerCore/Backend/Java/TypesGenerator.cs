using System.Collections.Generic;
using System.Linq;
using Plang.Compiler.TypeChecker;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.TypeChecker.Types;

namespace Plang.Compiler.Backend.Java
{
    internal class TypesGenerator : JavaSourceGenerator {

        private static HashSet<NamedTupleType> AllTuples(Scope scope)
        {
            var ret = new HashSet<NamedTupleType>();

            foreach (var t in scope.Tuples)
            {
                ret.Add(t);
            }

            foreach (var t in scope.Functions.SelectMany(f => AllTuples(f.Scope)))
            {
                ret.Add(t);
            }
            foreach (var t in scope.Machines.SelectMany(m => AllTuples(m.Scope)))
            {
                ret.Add(t);
            }

            return ret;
        }

        internal TypesGenerator(ICompilerConfiguration job, string filename) : base(job, filename)
        {
        }

        /// <summary>
        /// Generates Java code for a given compilation job's user-defined types like enums
        /// and tuples.
        /// </summary>
        protected override void GenerateCodeImpl()
        {
            WriteLine($"public class {Constants.TypesNamespaceName} {{");

            if (GlobalScope.Enums.Any())
            {
                WriteLine("/* Enums */");
                WriteLine();
                foreach (var e in GlobalScope.Enums)
                {
                    WriteEnumDecl(e);
                }
                WriteLine();
            }

            var tuples = AllTuples(GlobalScope);
            if (tuples.Any())
            {
                WriteLine("/* Tuples */");
                WriteLine();
                foreach (var t in tuples)
                {
                    WriteNamedTupleDecl(t);
                }
                WriteLine();
            }

            WriteLine("}");
        }

        private void WriteEnumDecl(PEnum e)
        {
            WriteLine($"public enum {e.Name} {{");

            var numFields = e.Values.Count();
            foreach (var (param, sep) in e.Values.Select((pair, i) => (pair, i < numFields - 1? "," : ";")))
            {
                WriteLine($"{param.Name}({param.Value.ToString()}){sep}");
            }

            WriteLine("private final int value;");
            WriteLine($"{e.Name}(int i) {{ value = i; }}");

            WriteLine("}");
        }


        private void WriteNamedTupleDecl(NamedTupleType t)
        {
            // This is a sequence of <type, field name> pairs.
            var fields =
                new List<(TypeManager.JType, string)>();

            // Build up our list of fields.
            foreach (var e in t.Fields)
            {
                var name = e.Name;
                var type = e.Type;

                // In the case where the field type is a typedef, follow
                // the typename resolution until we've found the actual type.
                while (type is TypeDefType tdef)
                {
                    type = tdef.TypeDefDecl.Type;
                }

                var jType = Types.JavaTypeFor(type);

                fields.Add((jType, name));
            }

            var tname = Names.NameForNamedTuple(t);
            WriteLine($"public static class {tname} implements {Constants.PValueClass}<{tname}>, Serializable {{");
            WriteLine($"// {t.CanonicalRepresentation}");

            WriteNamedTupleFields(fields);
            WriteLine();

            WriteNamedTupleConstructors(tname, fields);
            WriteLine();

            WriteNamedTupleEqualityMethods(tname, fields);
            WriteLine();

            WriteNamedTupleToString(tname, fields);
            WriteLine();

            WriteLine($"}} //{tname} class definition");

            WriteLine();
        }

        private void WriteNamedTupleFields(List<(TypeManager.JType, string)> fields)
        {
            foreach (var (jType, fieldName) in fields)
            {
                WriteLine($"public {jType.TypeName} {fieldName};");
            }
        }

        private void WriteNamedTupleConstructors(string tname, List<(TypeManager.JType, string)> fields)
        {
            // Write the default constructor.
            WriteLine($"public {tname}() {{");
            foreach (var (jtype, fieldName) in fields)
            {
                WriteLine($"this.{fieldName} = {jtype.DefaultValue};");

            }
            WriteLine($"}}");
            WriteLine();

            // Write the explicit constructor.
            Write($"public {tname}(");
            foreach (var (sep, (jType, fieldName)) in fields.WithPrefixSep(", "))
            {
                Write($"{sep}{jType.TypeName} {fieldName}");
            }
            WriteLine($") {{");
            foreach (var (_, fieldName) in fields)
            {
                WriteLine($"this.{fieldName} = {fieldName};");
            }
            WriteLine($"}}");
            WriteLine();

            // Write the copy constructor for cloning.
            WriteLine($"public {tname} deepClone() {{");
            Write($"return new {tname}(");
            foreach (var (sep, (jType, fieldName)) in fields.WithPrefixSep(", "))
            {
                Write(sep);
                WriteClone(jType, () => Write(fieldName));
            }
            WriteLine(");");
            WriteLine("} // deepClone()");
            WriteLine();
        }

        private void WriteNamedTupleEqualityMethods(string tname, List<(TypeManager.JType, string)> fields)
        {
            // .equals() implementation: this simply defers to deepEquals() but explicitly overriding it is useful
            // for calling assertEquals() in unit tests, for example.
            WriteLine($"public boolean equals(Object other) {{");
            WriteLine("return (this.getClass() == other.getClass() && ");
            WriteLine($"this.deepEquals(({tname})other)");
            WriteLine(");");
            WriteLine("} // equals()");
            WriteLine();

            // hashCode() implementation.
            WriteLine($"public int hashCode() {{");
            Write($"return Objects.hash(");
            foreach (var (sep, (_, fieldName)) in fields.WithPrefixSep(", "))
            {
                Write($"{sep}{fieldName}");
            }
            WriteLine(");");
            WriteLine("} // hashCode()");
            WriteLine();


            // Deep equality predicate.
            WriteLine($"public boolean deepEquals({tname} other) {{");
            WriteLine("return (true");
            foreach (var (jType, fieldName) in fields)
            {
                Write(" && ");
                WriteLine(jType.IsPrimitive
                    ? $"this.{fieldName} == other.{fieldName}"
                    : $"{Constants.PrtDeepEqualsMethodName}(this.{fieldName}, other.{fieldName})");
            }
            WriteLine(");");
            WriteLine("} // deepEquals()");
            WriteLine();
        }

        private void WriteNamedTupleToString(string tname, List<(TypeManager.JType, string)> fields)
        {
            // Write toString() in the same output style as a Java record.
            WriteLine("public String toString() {");
            WriteLine($"StringBuilder sb = new StringBuilder(\"{tname}\");");

            WriteLine("sb.append(\"[\");");
            foreach (var (sep, (_, fieldName)) in fields.WithPrefixSep(", "))
            {
                WriteLine($"sb.append(\"{sep}{fieldName}=\" + {fieldName});");
            }
            WriteLine("sb.append(\"]\");");

            WriteLine("return sb.toString();");
            WriteLine("} // toString()");
        }

    }
}