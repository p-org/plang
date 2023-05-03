package psym.runtime.scheduler.choiceorchestration;

import lombok.Getter;
import psym.runtime.Event;
import psym.runtime.Message;
import psym.runtime.logger.PSymLogger;
import psym.runtime.machine.Machine;
import psym.runtime.machine.State;
import psym.runtime.scheduler.Scheduler;
import psym.valuesummary.Guard;
import psym.valuesummary.PrimitiveVS;
import psym.valuesummary.ValueSummary;

import java.io.Serializable;
import java.math.BigDecimal;
import java.util.*;

public class ChoiceLearningStats<S, A> implements Serializable {
    @Getter
    private static BigDecimal defaultQValue = BigDecimal.ZERO;
    @Getter
    private static BigDecimal defaultReward = BigDecimal.valueOf(-1);
    @Getter
    private static BigDecimal ALPHA = BigDecimal.valueOf(0.3);
    @Getter
    private static BigDecimal GAMMA = BigDecimal.valueOf(0.7);


    /** State hash corresponding to current environment state */
    @Getter
    private Object programStateHash = null;

    @Getter
    private ChoiceComparator choiceComparator = new ChoiceComparator();
    private ChoiceQTable<S, A> qValues;

    private class ChoiceComparator implements Comparator<ValueSummary>, Serializable {
        public ChoiceComparator() {}

        @Override
        public int compare(ValueSummary lhs, ValueSummary rhs) {
            return getCurrentQvalue(rhs).compareTo(getCurrentQvalue(lhs));
        }
    }


    public ChoiceLearningStats() {
        qValues = new ChoiceQTable();
    }

    public void rewardIteration(ChoiceQTable.ChoiceQTableKey<S, A> stateActions, BigDecimal reward, ChoiceLearningRewardMode rewardMode) {
        switch (rewardMode) {
            case None:
                // do nothing
                break;
            case Fixed:
                reward(stateActions, defaultReward);
                break;
            case Coverage:
                reward(stateActions, reward);
                break;
            default:
                assert (false);
        }
    }

    public void rewardStep(ChoiceQTable.ChoiceQTableKey<S, A> stateActions, int reward) {
//        reward(stateActions, BigDecimal.valueOf(reward));
    }

    private void reward(ChoiceQTable.ChoiceQTableKey<S, A> stateActions, BigDecimal reward) {
        if (reward.equals(getDefaultQValue())) {
            return;
        }
        S state = stateActions.getState();
        ChoiceQTable.ChoiceQStateEntry stateEntry = qValues.get(state);

        for (Class cls: stateActions.getActions().getClasses()) {
            ChoiceQTable.ChoiceQClassEntry classEntry = stateEntry.get(cls);
            BigDecimal maxQ = classEntry.getMaxQ();

            for (A action: stateActions.getActions().get(cls)) {
                BigDecimal oldVal = classEntry.get(action);
                BigDecimal newVal = BigDecimal.valueOf(1).subtract(ALPHA).multiply(oldVal)
                                    .add(ALPHA.multiply(reward.add(GAMMA.multiply(maxQ))));
                classEntry.update(action, newVal);
            }
        }
    }

    public int numQStates() {
        return qValues.size();
    }

    public int numQValues() {
        int result = 0;
        for (S state: qValues.getStates()) {
            ChoiceQTable.ChoiceQStateEntry stateEntry = qValues.get(state);
            for (Object cls: stateEntry.getClasses()) {
                result += stateEntry.get((Class)cls).size();
            }
        }
        return result;
    }

    public void printQTable() {
        PSymLogger.log("--------------------");
        PSymLogger.info("Q Table");
        PSymLogger.log("--------------------");
        PSymLogger.info(String.format("  #QStates = %d", qValues.size()));
        for (S state: qValues.getStates()) {
            ChoiceQTable.ChoiceQStateEntry stateEntry = qValues.get(state);
            String stateStr = String.valueOf(state);
            if (stateStr.length() > 10) {
                stateStr = stateStr.substring(0, 5).concat("...");
            }

            for (Object obj: stateEntry.getClasses()) {
                Class cls = (Class) obj;
                ChoiceQTable.ChoiceQClassEntry classEntry = stateEntry.get(cls);
                if (classEntry.size() <= 1) {
                    continue;
                }
//                PSymLogger.info(String.format("  %s [%s] -> %s", stateStr, cls.getSimpleName(), classEntry));
                Object bestAction = classEntry.getBestAction();
                if (bestAction != null) {
                    BigDecimal maxQ = classEntry.get(bestAction);
                    PSymLogger.info(String.format("  %s [%s] -> %s -> %.10f\t%s", stateStr, cls.getSimpleName(), bestAction, maxQ, classEntry));
                }
            }
        }
    }

    public BigDecimal getQvalue(S state, Class cls, A action) {
        return qValues.get(state, cls, action);
    }

    public BigDecimal getCurrentQvalue(ValueSummary action) {
        Class cls = getActionClass(action);
        return getQvalue((S) programStateHash, cls, (A) getActionHash(cls, action));
    }

    public int getNumQStates() {
        return qValues.size();
    }

    public static Class getActionClass(ValueSummary action) {
        if (action instanceof PrimitiveVS) {
            PrimitiveVS pv = (PrimitiveVS) action;
            return pv.getValueClass();
        }
        return action.getClass();
    }

    public Object getActionHash(Class cls, ValueSummary action) {
        return action.toString();
    }

    public void setProgramStateHash(Scheduler sch, ChoiceLearningStateMode mode, PrimitiveVS<Machine> lastChoice) {
        switch (mode) {
            case None:
                setProgramHashNone();
                break;
            case SchedulerDepth:
                setProgramHashDepth(sch.getDepth());
                break;
            case LastStep:
                setProgramHashLastStep(lastChoice);
                break;
            case MachineState:
                setProgramHashMachineState(sch);
                break;
            case MachineStateAndLastStep:
                setProgramHashMachineStateAndLastStep(sch, lastChoice);
                break;
            case MachineStateAndEvents:
                setProgramHashMachineStateEvents(sch);
                break;
            case FullState:
                setProgramHashFullState(sch);
                break;
            default:
                assert (false);
        }
    }

    private void setProgramHashNone() {
        programStateHash = 0;
    }

    private void setProgramHashDepth(int depth) {
        programStateHash = depth;
    }

    private void setProgramHashLastStep(PrimitiveVS<Machine> lastChoice) {
        if (lastChoice != null) {
            List<Object> features = new ArrayList<>();
            for (Machine m : lastChoice.getValues()) {
                features.add(m);
                for (State state : m.getCurrentState().getValues()) {
                    features.add(state);
                }
                if (!m.sendBuffer.isEmpty()) {
                    Message msg = m.sendBuffer.peek(Guard.constTrue());
                    for (Machine target : msg.getTarget().getValues()) {
                        features.add(target);
                    }
                    for (Event event : msg.getEvent().getValues()) {
                        features.add(event);
//                        features.add(msg.getPayloadFor(event));
                    }
                }
            }
            programStateHash = features.toString();
        }
    }

    private void setProgramHashMachineState(Scheduler sch) {
        List<Object> features = new ArrayList<>();
        for (Machine m: sch.getMachines()) {
            features.add(m);
            for (State state: m.getCurrentState().getValues()) {
                features.add(state);
            }
        }
        programStateHash = features.toString();
    }

    private void setProgramHashMachineStateAndLastStep(Scheduler sch, PrimitiveVS<Machine> lastChoice) {
        List<Object> features = new ArrayList<>();
        for (Machine m: sch.getMachines()) {
            features.add(m);
            for (State state: m.getCurrentState().getValues()) {
                features.add(state);
            }
        }
        if (lastChoice != null) {
            for (Machine m : lastChoice.getValues()) {
                features.add(m);
                for (State state : m.getCurrentState().getValues()) {
                    features.add(state);
                }
                if (!m.sendBuffer.isEmpty()) {
                    Message msg = m.sendBuffer.peek(Guard.constTrue());
                    for (Machine target : msg.getTarget().getValues()) {
                        features.add(target);
                    }
                    for (Event event : msg.getEvent().getValues()) {
                        features.add(event);
//                        features.add(msg.getPayloadFor(event));
                    }
                }
            }
        }
        programStateHash = features.toString();
    }

    private void setProgramHashMachineStateEvents(Scheduler sch) {
        List<Object> features = new ArrayList<>();
        for (Machine m: sch.getMachines()) {
            features.add(m);
            for (State state: m.getCurrentState().getValues()) {
                features.add(state);
            }
            if (!m.sendBuffer.isEmpty()) {
                Message msg = m.sendBuffer.peek(Guard.constTrue());
                for (Machine target: msg.getTarget().getValues()) {
                    features.add(target);
                }
                for (Event event: msg.getEvent().getValues()) {
                    features.add(event);
//                    features.add(msg.getPayloadFor(event));
                }
            }
        }
        programStateHash = features.toString();
    }

    private void setProgramHashFullState(Scheduler sch) {
        List<Object> features = new ArrayList<>();
        for (Machine m: sch.getMachines()) {
            features.add(m);
            features.add(m.getLocalState());
        }
        programStateHash = features.toString();
    }

}
