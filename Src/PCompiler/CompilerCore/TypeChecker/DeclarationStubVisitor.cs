using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using Plang.Compiler.TypeChecker.AST;
using Plang.Compiler.TypeChecker.AST.Declarations;
using Plang.Compiler.Util;

namespace Plang.Compiler.TypeChecker
{
    internal class DeclarationStubVisitor : PParserBaseVisitor<object>
    {
        private readonly ParseTreeProperty<IPDecl> nodesToDeclarations;
        private readonly StackProperty<Scope> scope;

        private DeclarationStubVisitor(
            Scope globalScope,
            ParseTreeProperty<IPDecl> nodesToDeclarations)
        {
            this.nodesToDeclarations = nodesToDeclarations;
            scope = new StackProperty<Scope>(globalScope);
        }

        private Scope CurrentScope => scope.Value;

        public static void PopulateStubs(
            Scope globalScope,
            PParser.ProgramContext context,
            ParseTreeProperty<IPDecl> nodesToDeclarations)
        {
            var visitor = new DeclarationStubVisitor(globalScope, nodesToDeclarations);
            visitor.Visit(context);
        }

        #region Events

        public override object VisitEventDecl(PParser.EventDeclContext context)
        {
            var symbolName = context.name.GetText();
            var decl = CurrentScope.Put(symbolName, context);
            nodesToDeclarations.Put(context, decl);
            CurrentScope.UniversalEventSet.AddEvent(decl);
            return null;
        }

        #endregion Events

        #region Delays
        
        public override object VisitDelayDecl(PParser.DelayDeclContext context)
        {
            var symbolName = context.ev.GetText();
            nodesToDeclarations.Put(context, new Delay(context.ev.GetText(), context));
            return null;
        }
        
        #endregion
        
        #region Event sets

        public override object VisitEventSetDecl(PParser.EventSetDeclContext context)
        {
            var symbolName = context.name.GetText();
            var decl = CurrentScope.Put(symbolName, context);
            nodesToDeclarations.Put(context, decl);
            return null;
        }

        #endregion Event sets

        #region Interfaces

        public override object VisitInterfaceDecl(PParser.InterfaceDeclContext context)
        {
            var symbolName = context.name.GetText();
            var decl = CurrentScope.Put(symbolName, context);
            nodesToDeclarations.Put(context, decl);
            return null;
        }

        #endregion Interfaces

        private object VisitChildrenWithNewScope(IHasScope decl, IRuleNode context)
        {
            using (scope.NewContext(CurrentScope.MakeChildScope()))
            {
                decl.Scope = CurrentScope;
                return VisitChildren(context);
            }
        }

        #region Typedefs

        public override object VisitPTypeDef(PParser.PTypeDefContext context)
        {
            var symbolName = context.name.GetText();
            var typeDef = CurrentScope.Put(symbolName, context);
            nodesToDeclarations.Put(context, typeDef);
            return null;
        }

        public override object VisitForeignTypeDef(PParser.ForeignTypeDefContext context)
        {
            var symbolName = context.name.GetText();
            var typeDef = CurrentScope.Put(symbolName, context);
            nodesToDeclarations.Put(context, typeDef);
            return null;
        }

        #endregion Typedefs

        #region Enum typedef

        public override object VisitEnumTypeDefDecl(PParser.EnumTypeDefDeclContext context)
        {
            var symbolName = context.name.GetText();
            var pEnum = CurrentScope.Put(symbolName, context);
            nodesToDeclarations.Put(context, pEnum);
            return VisitChildren(context);
        }

        public override object VisitEnumElem(PParser.EnumElemContext context)
        {
            var symbolName = context.name.GetText();
            var elem = CurrentScope.Put(symbolName, context);
            nodesToDeclarations.Put(context, elem);
            return null;
        }

        public override object VisitNumberedEnumElem(PParser.NumberedEnumElemContext context)
        {
            var symbolName = context.name.GetText();
            var elem = CurrentScope.Put(symbolName, context);
            nodesToDeclarations.Put(context, elem);
            return null;
        }

        #endregion Enum typedef

        #region Machines

        public override object VisitImplMachineDecl(PParser.ImplMachineDeclContext context)
        {
            var symbolName = context.name.GetText();
            var decl = CurrentScope.Put(symbolName, context);
            nodesToDeclarations.Put(context, decl);
            return VisitChildrenWithNewScope(decl, context);
        }

        public override object VisitSpecMachineDecl(PParser.SpecMachineDeclContext context)
        {
            var symbolName = context.name.GetText();
            var decl = CurrentScope.Put(symbolName, context);
            nodesToDeclarations.Put(context, decl);
            return VisitChildrenWithNewScope(decl, context);
        }

        public override object VisitVarDecl(PParser.VarDeclContext context)
        {
            foreach (var varName in context.idenList()._names)
            {
                var decl = CurrentScope.Put(varName.GetText(), varName, VariableRole.Field);
                nodesToDeclarations.Put(varName, decl);
            }

            return null;
        }

        public override object VisitStateDecl(PParser.StateDeclContext context)
        {
            var symbolName = context.name.GetText();
            var decl = CurrentScope.Put(symbolName, context);
            nodesToDeclarations.Put(context, decl);
            return null;
        }

        #endregion Machines

        #region Functions

        public override object VisitPFunDecl(PParser.PFunDeclContext context)
        {
            var symbolName = context.name.GetText();
            var decl = CurrentScope.Put(symbolName, context);
            nodesToDeclarations.Put(context, decl);
            return VisitChildrenWithNewScope(decl, context);
        }

        public override object VisitFunParam(PParser.FunParamContext context)
        {
            var symbolName = context.name.GetText();
            var decl = CurrentScope.Put(symbolName, context, VariableRole.Param);
            nodesToDeclarations.Put(context, decl);
            return null;
        }

        public override object VisitFunctionBody(PParser.FunctionBodyContext context)
        {
            return null;
        }

        public override object VisitForeignFunDecl(PParser.ForeignFunDeclContext context)
        {
            var symbolName = context.name.GetText();
            var decl = CurrentScope.Put(symbolName, context);
            decl.Scope = CurrentScope.MakeChildScope();
            nodesToDeclarations.Put(context, decl);
            return VisitChildrenWithNewScope(decl, context);
        }

        #endregion Functions

        #region Module System

        public override object VisitNamedModuleDecl([NotNull] PParser.NamedModuleDeclContext context)
        {
            var symbolName = context.name.GetText();
            var decl = CurrentScope.Put(symbolName, context);
            nodesToDeclarations.Put(context, decl);
            return null;
        }

        public override object VisitSafetyTestDecl([NotNull] PParser.SafetyTestDeclContext context)
        {
            var symbolName = context.testName.GetText();
            var decl = CurrentScope.Put(symbolName, context);
            if (decl != null)
            {
                decl.Main = context.mainMachine?.GetText();
                nodesToDeclarations.Put(context, decl);
            }
            return null;
        }

        public override object VisitRefinementTestDecl([NotNull] PParser.RefinementTestDeclContext context)
        {
            var symbolName = context.testName.GetText();
            var decl = CurrentScope.Put(symbolName, context);
            decl.Main = context.mainMachine?.GetText();
            nodesToDeclarations.Put(context, decl);
            return null;
        }

        public override object VisitImplementationDecl([NotNull] PParser.ImplementationDeclContext context)
        {
            var symbolName = context.implName.GetText();
            var decl = CurrentScope.Put(symbolName, context);
            decl.Main = context.mainMachine?.GetText();
            nodesToDeclarations.Put(context, decl);
            return null;
        }

        #endregion Module System

        #region Tree clipping expressions

        public override object VisitKeywordExpr(PParser.KeywordExprContext context)
        {
            return null;
        }

        public override object VisitSeqAccessExpr(PParser.SeqAccessExprContext context)
        {
            return null;
        }

        public override object VisitNamedTupleAccessExpr(PParser.NamedTupleAccessExprContext context)
        {
            return null;
        }

        public override object VisitPrimitiveExpr(PParser.PrimitiveExprContext context)
        {
            return null;
        }

        public override object VisitBinExpr(PParser.BinExprContext context)
        {
            return null;
        }

        public override object VisitUnaryExpr(PParser.UnaryExprContext context)
        {
            return null;
        }

        public override object VisitTupleAccessExpr(PParser.TupleAccessExprContext context)
        {
            return null;
        }

        public override object VisitUnnamedTupleExpr(PParser.UnnamedTupleExprContext context)
        {
            return null;
        }

        public override object VisitFunCallExpr(PParser.FunCallExprContext context)
        {
            return null;
        }

        public override object VisitCastExpr(PParser.CastExprContext context)
        {
            return null;
        }

        public override object VisitCtorExpr(PParser.CtorExprContext context)
        {
            return null;
        }

        public override object VisitParenExpr(PParser.ParenExprContext context)
        {
            return null;
        }

        public override object VisitNamedTupleExpr(PParser.NamedTupleExprContext context)
        {
            return null;
        }

        public override object VisitExpr(PParser.ExprContext context)
        {
            return null;
        }

        #endregion Tree clipping expressions

        #region Tree clipping non-receive (containing) statements

        public override object VisitRemoveStmt(PParser.RemoveStmtContext context)
        {
            return null;
        }

        public override object VisitPrintStmt(PParser.PrintStmtContext context)
        {
            return null;
        }

        public override object VisitSendStmt(PParser.SendStmtContext context)
        {
            return null;
        }

        public override object VisitCtorStmt(PParser.CtorStmtContext context)
        {
            return null;
        }

        public override object VisitAssignStmt(PParser.AssignStmtContext context)
        {
            return null;
        }

        public override object VisitAddStmt(PParser.AddStmtContext context)
        {
            return null;
        }

        public override object VisitInsertStmt(PParser.InsertStmtContext context)
        {
            return null;
        }

        public override object VisitAnnounceStmt(PParser.AnnounceStmtContext context)
        {
            return null;
        }

        public override object VisitRaiseStmt(PParser.RaiseStmtContext context)
        {
            return null;
        }

        public override object VisitFunCallStmt(PParser.FunCallStmtContext context)
        {
            return null;
        }

        public override object VisitNoStmt(PParser.NoStmtContext context)
        {
            return null;
        }


        public override object VisitGotoStmt(PParser.GotoStmtContext context)
        {
            return null;
        }

        public override object VisitAssertStmt(PParser.AssertStmtContext context)
        {
            return null;
        }

        public override object VisitReturnStmt(PParser.ReturnStmtContext context)
        {
            return null;
        }

        #endregion Tree clipping non-receive (containing) statements

        #region Tree clipping types

        public override object VisitSeqType(PParser.SeqTypeContext context)
        {
            return null;
        }

        public override object VisitNamedType(PParser.NamedTypeContext context)
        {
            return null;
        }

        public override object VisitTupleType(PParser.TupleTypeContext context)
        {
            return null;
        }

        public override object VisitNamedTupleType(PParser.NamedTupleTypeContext context)
        {
            return null;
        }

        public override object VisitPrimitiveType(PParser.PrimitiveTypeContext context)
        {
            return null;
        }

        public override object VisitMapType(PParser.MapTypeContext context)
        {
            return null;
        }

        public override object VisitSetType(PParser.SetTypeContext context)
        {
            return null;
        }

        public override object VisitType(PParser.TypeContext context)
        {
            return null;
        }

        public override object VisitIdenTypeList(PParser.IdenTypeListContext context)
        {
            return null;
        }

        public override object VisitIdenType(PParser.IdenTypeContext context)
        {
            return null;
        }

        public override object VisitTypeDefDecl(PParser.TypeDefDeclContext context)
        {
            return null;
        }

        #endregion Tree clipping types
    }
}