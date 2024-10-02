using System.Diagnostics;
using Antlr4.Runtime;

namespace Plang.Compiler.TypeChecker.AST.Declarations;

public class Delay : IPDecl
{
    public Delay(string name, ParserRuleContext sourceNode)
    {
        Name = name;
        SourceLocation = sourceNode;
    }

    public ParserRuleContext SourceLocation { get; }

    public string Name { get; }
}
