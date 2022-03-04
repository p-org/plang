package psymbolic.valuesummary.solvers.sat;

import psymbolic.valuesummary.solvers.SolverLib;
import psymbolic.valuesummary.solvers.SolverType;

public class SatGuard implements SolverLib<SatExpr> {
    private static SatLib satImpl;
    private static SolverType solverType;

    public SatGuard(SolverType st) {
        setSolver(st);
    }

    public static SatLib getSolver() {
        return satImpl;
    }

    public static void setSolver(SolverType type) {
        solverType = type;
        switch(type) {
            case CVC5:	                satImpl = new CVC5Impl();
                break;
            case YICES2:	            satImpl = new YicesImpl();
                break;
            case Z3:	                satImpl = new Z3Impl();
                break;
            case JAVASMT_BOOLECTOR:
            case JAVASMT_CVC4:
            case JAVASMT_MATHSAT5:
            case JAVASMT_PRINCESS:
            case JAVASMT_SMTINTERPOL:
            case JAVASMT_YICES2:
            case JAVASMT_Z3:            satImpl = new JavaSmtImpl(type);
                break;
            default:
                throw new RuntimeException("Unexpected solver configuration of type " + type);
        }
    }

    public boolean isSat(SatExpr formula) {
        return SatExpr.isSat(formula);
    }

    public SatExpr constFalse() {
        return SatExpr.ConstFalse();
    }

    public SatExpr constTrue() {
        return SatExpr.ConstTrue();
    }

    public SatExpr and(SatExpr left, SatExpr right) {
        return SatExpr.And(left, right);
    }

    public SatExpr or(SatExpr left, SatExpr right) {
        return SatExpr.Or(left, right);
    }

    public SatExpr not(SatExpr formula) {
        return SatExpr.Not(formula);
    }

    public SatExpr implies(SatExpr left, SatExpr right) {
        return SatExpr.Or(SatExpr.Not(left), right);
    }

    public SatExpr ifThenElse(SatExpr cond, SatExpr thenClause, SatExpr elseClause) {
        return SatExpr.Or(SatExpr.And(SatExpr.Not(cond), thenClause),
                          SatExpr.And(cond, elseClause));
    }

    public SatExpr newVar() {
        return SatExpr.NewVar();
    }

    public String toString(SatExpr formula) {
        return formula.toString();
    }

    public SatExpr fromString(String s) {
        if (s.equals("false")) {
            return constFalse();
        }
        if (s.equals("true")) {
            return constTrue();
        }
        throw new RuntimeException("Unsupported");
    }

    public int getVarCount() {
        return SatExpr.numVars;
    }

    public int getNodeCount() {
        return SatExpr.table.size();
    }

    public String getStats() {
        return satImpl.getStats();
    }

    public void cleanup() {
        satImpl.cleanup();
    }

    public boolean areEqual(SatExpr left, SatExpr right) {
        return left.equals(right);
    }

}