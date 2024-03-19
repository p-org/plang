package pcover.runtime.scheduler;

import lombok.Getter;
import lombok.Setter;
import pcover.runtime.machine.PMachine;
import pcover.values.PValue;

import java.io.Serializable;
import java.util.*;

/**
 * Represents a single (possibly partial) schedule.
 */
public class Schedule implements Serializable {
    private Map<Class<? extends PMachine>, List<PMachine>> createdMachines = new HashMap<>();
    private Set<PMachine> machines = new HashSet<>();

    @Getter
    @Setter
    private List<Choice> choices = new ArrayList<>();
    @Getter
    @Setter
    private int schedulerDepth = 0;
    @Getter
    @Setter
    private int schedulerChoiceDepth = 0;

    /**
     * Constructor
     */
    public Schedule() {
    }

    /**
     * Constructor
     *
     * @param choices         Choice list
     * @param createdMachines Map of machine type to list of created machines
     * @param machines        Set of machines
     */
    private Schedule(
            List<Choice> choices,
            Map<Class<? extends PMachine>, List<PMachine>> createdMachines,
            Set<PMachine> machines) {
        this.choices = new ArrayList<>(choices);
        this.createdMachines = new HashMap<>(createdMachines);
        this.machines = new HashSet<>(machines);
    }

    /**
     * Get a fresh new choice
     *
     * @return New choice object
     */
    public Choice newChoice() {
        return new Choice();
    }

    /**
     * Get the choice at a choice depth
     *
     * @param idx Choice depth
     * @return Choice at depth idx
     */
    public Choice getChoice(int idx) {
        return choices.get(idx);
    }

    /**
     * Set the choice at a choice depth.
     *
     * @param idx    Choice depth
     * @param choice Choice object
     */
    public void setChoice(int idx, Choice choice) {
        choices.set(idx, choice);
    }

    /**
     * Clear choice at a choice depth
     *
     * @param idx Choice depth
     */
    public void clearChoice(int idx) {
        choices.get(idx).clear();
    }

    /**
     * Get the number of backtrack choices in this schedule
     *
     * @return Number of backtrack choices
     */
    public int getNumBacktracksInSchedule() {
        int numBacktracks = 0;
        for (Choice backtrack : choices) {
            if (backtrack.isBacktrackNonEmpty()) {
                numBacktracks++;
            }
        }
        return numBacktracks;
    }

    /**
     * Get the number of backtrack data choices in this schedule
     *
     * @return Number of backtrack data choices
     */
    public int getNumDataBacktracksInSchedule() {
        int numDataBacktracks = 0;
        for (Choice backtrack : choices) {
            if (backtrack.isDataBacktrackNonEmpty()) {
                numDataBacktracks++;
            }
        }
        return numDataBacktracks;
    }

    /**
     * Set the repeat schedule choice at a choice depth.
     *
     * @param choice Machine to set as repeat schedule choice
     * @param idx    Choice depth
     */
    public void setRepeatScheduleChoice(PMachine choice, int idx) {
        if (idx >= choices.size()) {
            choices.add(newChoice());
        }
        choices.get(idx).setRepeatScheduleChoice(choice);
    }

    /**
     * Set the repeat data choice at a choice depth.
     *
     * @param choice PValue to set as repeat data choice
     * @param idx    Choice depth
     */
    public void setRepeatDataChoice(PValue<?> choice, int idx) {
        if (idx >= choices.size()) {
            choices.add(newChoice());
        }
        choices.get(idx).setRepeatDataChoice(choice);
    }

    /**
     * Add backtrack schedule choices at a choice depth.
     *
     * @param machines List of machines to add as backtrack schedule choice
     * @param idx      Choice depth
     */
    public void addBacktrackScheduleChoice(List<PMachine> machines, int idx) {
        if (idx >= choices.size()) {
            choices.add(newChoice());
        }
        for (PMachine choice : machines) {
            choices.get(idx).addBacktrackScheduleChoice(choice);
        }
    }

    /**
     * Add backtrack data choices at a choice depth.
     *
     * @param values List of PValue to add as backtrack data choice
     * @param idx    Choice depth
     */
    public void addBacktrackDataChoice(List<PValue<?>> values, int idx) {
        if (idx >= choices.size()) {
            choices.add(newChoice());
        }
        for (PValue<?> choice : values) {
            choices.get(idx).addBacktrackDataChoice(choice);
        }
    }

    /**
     * Get the repeat schedule choice at a choice depth.
     *
     * @param idx Choice depth
     * @return Repeat schedule choice
     */
    public PMachine getRepeatScheduleChoice(int idx) {
        return choices.get(idx).getRepeatScheduleChoice();
    }

    /**
     * Get the repeat data choice at a choice depth.
     *
     * @param idx Choice depth
     * @return Repeat data choice
     */
    public PValue<?> getRepeatDataChoice(int idx) {
        return choices.get(idx).getRepeatDataChoice();
    }

    /**
     * Get backtrack schedule choices at a choice depth.
     *
     * @param idx Choice depth
     * @return List of machines
     */
    public List<PMachine> getBacktrackScheduleChoice(int idx) {
        return choices.get(idx).getBacktrackScheduleChoice();
    }

    /**
     * Get backtrack data choices at a choice depth.
     *
     * @param idx Choice depth
     * @return List of PValue
     */
    public List<PValue<?>> getBacktrackDataChoice(int idx) {
        return choices.get(idx).getBacktrackDataChoice();
    }

    /**
     * Clear repeat choices at a choice depth
     *
     * @param idx Choice depth
     */
    public void clearRepeat(int idx) {
        choices.get(idx).clearRepeat();
    }

    /**
     * Clear backtrack choices at a choice depth
     *
     * @param idx Choice depth
     */
    public void clearBacktrack(int idx) {
        choices.get(idx).clearBacktrack();
    }

    /**
     * Get the number of choices in the schedule
     *
     * @return Number of choices in the schedule
     */
    public int size() {
        return choices.size();
    }

    /**
     * Add a machine to the schedule.
     *
     * @param machine Machine to add
     */
    public void makeMachine(PMachine machine) {
        createdMachines.getOrDefault(machine.getClass(), new ArrayList<>()).add(machine);
        machines.add(machine);
    }

    /**
     * Check if a machine of a given type and index exists in the schedule.
     *
     * @param type Machine type
     * @param idx  Machine index
     * @return true if machine is in this schedule, false otherwise
     */
    public boolean hasMachine(Class<? extends PMachine> type, int idx) {
        if (!createdMachines.containsKey(type))
            return false;
        return idx < createdMachines.get(type).size();
    }

    /**
     * Get a machine of a given type and index.
     *
     * @param type Machine type
     * @param idx  Machine index
     * @return Machine
     */
    public PMachine getMachine(Class<? extends PMachine> type, int idx) {
        assert (hasMachine(type, idx));
        return createdMachines.get(type).get(idx);
    }
}
