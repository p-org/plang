using System.Collections.Generic;
using Antlr4.Runtime;
using Plang.Compiler.TypeChecker.AST.Declarations;

namespace Plang.Compiler.TypeChecker.AST.Statements
{
    public class SendStmt : IPStmt
    {
        public SendStmt(ParserRuleContext sourceLocation, IPExpr machineExpr, IPExpr evt,
            IReadOnlyList<IPExpr> arguments, IPExpr delayDistribution = null)
        {
            SourceLocation = sourceLocation;
            MachineExpr = machineExpr;
            Evt = evt;
            Arguments = arguments;
            DelayDistribution = delayDistribution;
        }

        public IPExpr MachineExpr { get; }
        public IPExpr Evt { get; }
        public IReadOnlyList<IPExpr> Arguments { get; }

        public IPExpr DelayDistribution { get;  }
        public ParserRuleContext SourceLocation { get; }
    }
}