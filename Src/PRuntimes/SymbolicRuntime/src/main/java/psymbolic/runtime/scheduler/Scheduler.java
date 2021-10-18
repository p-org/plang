package psymbolic.runtime.scheduler;

import psymbolic.commandline.Assert;
import psymbolic.commandline.PSymConfiguration;
import psymbolic.commandline.Program;
import psymbolic.runtime.*;
import psymbolic.runtime.logger.TraceLogger;
import psymbolic.runtime.machine.Machine;
import psymbolic.runtime.statistics.SearchStats;
import psymbolic.valuesummary.*;
import psymbolic.valuesummary.bdd.BDDEngine;

import java.util.*;
import java.util.function.Function;
import java.util.stream.Collectors;


public class Scheduler implements SymbolicSearch {

    protected SearchStats searchStats = new SearchStats();

    /** The scheduling choices made */
    public final Schedule schedule;

    PSymConfiguration configuration;

    /** List of all machines along any path constraints */
    final List<Machine> machines;

    /** How many instances of each Machine there are */
    final Map<Class<? extends Machine>, PrimitiveVS<Integer>> machineCounters;

    /** The machine to start with */
    private Machine start;

    /** The map from events to listening monitors */
    private Map<Event, List<Monitor>> listeners;

    /** List of monitors instances */
    private List<Monitor> monitors;

    /** Map of what not to interleave */
    private Map<Event, Set<Event>> interleaveMap;

    /** Use the interleave map (if false) or not (if true) */
    private boolean alwaysInterleaveNonAsync = false;

    /** Get whether to intersect with receiver queue semantics
     * @return whether to intersect with receiver queue semantics
     */
    public boolean addReceiverQueueSemantics() { return configuration.isAddReceiverQueueSemantics(); }

    /** Receiver queue semantics tracker */
    private ReceiverQueue receiverQueue = new ReceiverQueue();

    /** Get receiver queue semantics tracker */
    public ReceiverQueue getReceiverQueue() { return receiverQueue; }

    /** Current depth of exploration */
    private int depth = 0;
    /** Whether or not search is done */
    private boolean done = false;

    int choiceDepth = 0;

    /** Reset scheduler state
     */
    public void reset() {
        depth = 0;
        choiceDepth = 0;
        done = false;
        machineCounters.clear();
        machines.clear();
        receiverQueue = new ReceiverQueue();
    }

    /** Find out whether symbolic execution is finished
     * @return Whether or not there are more steps to run
     */
    public boolean isDone() {
        return done || depth == configuration.getDepthBound();
    }

    /** Get current depth
     * @return current depth
     */
    public int getDepth() { return depth; }

    /** Make new schedule
     * @return A new Schedule instance */
    public Schedule getNewSchedule() {
        return new Schedule();
    }

    /** Get the schedule
     * @return The schedule
     */
    public Schedule getSchedule() { return schedule; }

    /** Make a new Scheduler
     * @param machines The machines initially in the Scheduler
     */
    public Scheduler(PSymConfiguration config, Machine... machines) {

        this.configuration = config;
        this.schedule = getNewSchedule();
        this.machines = new ArrayList<>();
        this.machineCounters = new HashMap<>();
        this.interleaveMap = InterleaveMap.getMap();

        for (Machine machine : machines) {
            this.machines.add(machine);
            if (this.machineCounters.containsKey(machine.getClass())) {
                this.machineCounters.put(machine.getClass(),
                        IntegerVS.add(this.machineCounters.get(machine.getClass()), 1));
            } else {
                this.machineCounters.put(machine.getClass(), new PrimitiveVS<>(1));
            }
            TraceLogger.onCreateMachine(Guard.constTrue(), machine);
            machine.setScheduler(this);
            schedule.makeMachine(machine, Guard.constTrue());
        }
    }

    public List<PrimitiveVS> getNextIntegerChoices(PrimitiveVS<Integer> bound, Guard pc) {
        List<PrimitiveVS> choices = new ArrayList<>();
        for (int i = 0; i < IntegerVS.maxValue(bound); i++) {
            Guard cond = IntegerVS.lessThan(i, bound).getGuardFor(true);
            choices.add(new PrimitiveVS<>(i).restrict(cond).restrict(pc));
        }
        return choices;
    }

    public PrimitiveVS<Integer> getNextInteger(List<PrimitiveVS> candidateIntegers) {
        PrimitiveVS<Integer> choices = (PrimitiveVS<Integer>) NondetUtil.getNondetChoice(candidateIntegers);
        schedule.addRepeatInt(choices, choiceDepth);
        choiceDepth++;
        return choices;
    }

    @Override
    public PrimitiveVS<Integer> getNextInteger(PrimitiveVS<Integer> bound, Guard pc) {
        return getNextInteger(getNextIntegerChoices(bound, pc));
    }

    public List<PrimitiveVS> getNextBooleanChoices(Guard pc) {
        List<PrimitiveVS> choices = new ArrayList<>();
        choices.add(new PrimitiveVS<>(true).restrict(pc));
        choices.add(new PrimitiveVS<>(false).restrict(pc));
        return choices;
    }

    public PrimitiveVS<Boolean> getNextBoolean(List<PrimitiveVS> candidateBooleans) {
        PrimitiveVS<Boolean> choices = (PrimitiveVS<Boolean>) NondetUtil.getNondetChoice(candidateBooleans);
        schedule.addRepeatBool(choices, choiceDepth);
        choiceDepth++;
        return choices;
    }

    @Override
    public PrimitiveVS<Boolean> getNextBoolean(Guard pc) {
        return getNextBoolean(getNextBooleanChoices(pc));
    }

    public List<ValueSummary> getNextElementChoices(ListVS candidates, Guard pc) {
        PrimitiveVS<Integer> size = candidates.size();
        PrimitiveVS<Integer> index = new PrimitiveVS<>(0).restrict(size.getUniverse());
        List<ValueSummary> list = new ArrayList<>();
        while (BooleanVS.isEverTrue(IntegerVS.lessThan(index, size))) {
            Guard cond = BooleanVS.getTrueGuard(IntegerVS.lessThan(index, size));
            list.add(candidates.get(index).restrict(pc));
            index = IntegerVS.add(index, 1);
        }
        return list;
    }

    public PrimitiveVS<ValueSummary> getNextElementHelper(List<ValueSummary> candidates) {
        PrimitiveVS<ValueSummary> choices = NondetUtil.getNondetChoice(candidates.stream().map(x -> new PrimitiveVS(x).restrict(x.getUniverse())).collect(Collectors.toList()));
        schedule.addRepeatElement(choices, choiceDepth);
        choiceDepth++;
        return choices;
    }

    public ValueSummary getNextElementFlattener(PrimitiveVS<ValueSummary> choices) {
        ValueSummary flattened = null;
        List<ValueSummary> toMerge = new ArrayList<>();
        for (GuardedValue<ValueSummary> guardedValue : choices.getGuardedValues()) {
            if (flattened == null) {
                flattened = guardedValue.getValue().restrict(guardedValue.getGuard());
            } else {
                toMerge.add(guardedValue.getValue().restrict(guardedValue.getGuard()));
            }
        }
        if (flattened == null) {
            flattened = new PrimitiveVS<>();
        } else {
            flattened = flattened.merge(toMerge);
        }
        return flattened;
    }

    @Override
    public ValueSummary getNextElement(ListVS<? extends ValueSummary> s, Guard pc) {
        return getNextElementFlattener(getNextElementHelper(getNextElementChoices(s, pc)));
    }

    @Override
    public ValueSummary getNextElement(SetVS<? extends ValueSummary> s, Guard pc) {
        return getNextElement(s.getElements(), pc);
    }

    /** Start execution with the specified machine
     * @param machine Machine to start execution with */
    public void startWith(Machine machine) {
        if (this.machineCounters.containsKey(machine.getClass())) {
            this.machineCounters.put(machine.getClass(),
                    IntegerVS.add(this.machineCounters.get(machine.getClass()), 1));
        } else {
            this.machineCounters.put(machine.getClass(), new PrimitiveVS<>(1));
        }

        machines.add(machine);
        start = machine;
        TraceLogger.onCreateMachine(Guard.constTrue(), machine);
        machine.setScheduler(this);
        schedule.makeMachine(machine, Guard.constTrue());

        performEffect(
                new Message(
                        Event.createMachine,
                        new PrimitiveVS<>(machine),
                        null
                )
        );
    }

    public void replayStartWith(Machine machine) {
        PrimitiveVS<Machine> machineVS;
        if (this.machineCounters.containsKey(machine.getClass())) {
            machineVS = schedule.getMachine(machine.getClass(), this.machineCounters.get(machine.getClass()));
            this.machineCounters.put(machine.getClass(),
                    IntegerVS.add(this.machineCounters.get(machine.getClass()), 1));
        } else {
            machineVS = schedule.getMachine(machine.getClass(), new PrimitiveVS<>(0));
            this.machineCounters.put(machine.getClass(), new PrimitiveVS<>(1));
        }

        TraceLogger.onCreateMachine(machineVS.getUniverse(), machine);
        machine.setScheduler(this);

        performEffect(
                new Message(
                        Event.createMachine,
                        machineVS,
                        null
                )
        );
    }

    @Override
    public void doSearch(Program p) {
        listeners = p.getMonitorMap();
        monitors = new ArrayList<>(p.getMonitorList());
        for (Machine m : p.getMonitorList()) {
            startWith(m);
        }
        Machine target = p.getStart();
        startWith(target);
        start = target;
        while (!isDone()) {
            // ScheduleLogger.log("step " + depth + ", true queries " + Guard.trueQueries + ", false queries " + Guard.falseQueries);
            Assert.prop(depth < configuration.getMaxDepthBound(), "Maximum allowed depth " + configuration.getMaxDepthBound() + " exceeded", this, schedule.getLengthCond(schedule.size()));
            step();
        }
    }

    public List<PrimitiveVS> getNextSenderChoices() {

        // prioritize the create actions
        for (Machine machine : machines) {
            if (!machine.sendBuffer.isEmpty()) {
                Guard initCond = machine.sendBuffer.hasCreateMachineUnderGuard().getGuardFor(true);
                if (!initCond.isFalse()) {
                    PrimitiveVS<Machine> ret = new PrimitiveVS<>(machine).restrict(initCond);
                    return new ArrayList<>(Arrays.asList(ret));
                }
            }
        }

        // prioritize the sync actions i.e. events that are marked as synchronous
        for (Machine machine : machines) {
            if (!machine.sendBuffer.isEmpty()) {
                Guard syncCond = machine.sendBuffer.hasSyncEventUnderGuard().getGuardFor(true);
                if (!syncCond.isFalse()) {
                    PrimitiveVS<Machine> ret = new PrimitiveVS<>(machine).restrict(syncCond);
                    return new ArrayList<>(Arrays.asList(ret));
                }
            }
        }

        // now there are no create machine and sync event actions remaining
        List<PrimitiveVS> candidateSenders = new ArrayList<>();
        List<Message> candidateFirstMessages = new ArrayList<>();

        for (Machine machine : machines) {
            if (!machine.sendBuffer.isEmpty()) {
                Guard canRun = machine.hasHalted().getGuardFor(true).not();
                canRun = canRun.and(machine.sendBuffer.satisfiesPredUnderGuard(x ->
                             BooleanVS.and(BooleanVS.and(x.canRun(), shouldInterleave(candidateFirstMessages, x)), receiverQueueCond(machine, x))).getGuardFor(true));
                if (!canRun.isFalse()) {
                    candidateSenders.add(new PrimitiveVS<>(machine).restrict(canRun));
                    candidateFirstMessages.add(machine.sendBuffer.peek(canRun));
                }
            }
        }

        return candidateSenders;
    }

    private PrimitiveVS<Boolean> receiverQueueCond(Machine src, Message m) {
        if (!addReceiverQueueSemantics()) return new PrimitiveVS<>(true);
        PrimitiveVS<Machine> target = m.getTarget();
        return receiverQueue.guardFor(src, m);
    }

    private PrimitiveVS<Boolean> shouldInterleave(List<Message> candidateMessages, Message m) {
        if (alwaysInterleaveNonAsync) return new PrimitiveVS<>(true);
        PrimitiveVS<Event> event = m.getEvent();
        PrimitiveVS<Set<Event>> doNotInterleave = new PrimitiveVS<>();
        List<PrimitiveVS<Set<Event>>> toMerge = new ArrayList<>();
        for (GuardedValue<Event> e : event.getGuardedValues()) {
            if (interleaveMap.containsKey(e.getValue())) {
                toMerge.add(new PrimitiveVS<>(interleaveMap.get(e.getValue())));
            }
        }
        doNotInterleave = doNotInterleave.merge(toMerge);

        Guard equal = Guard.constFalse();
        for (Message other : candidateMessages) {
            for (GuardedValue<Event> e : other.getEvent().getGuardedValues()) {
                for (GuardedValue<Set<Event>> notToInterleave : doNotInterleave.getGuardedValues()) {
                    for (Event replacement : notToInterleave.getValue()) {
                        if (e.getValue().equals(replacement)) {
                            equal = equal.or(e.getGuard().and(notToInterleave.getGuard()));
                        }
                    }
                }
            }
        }
        return new PrimitiveVS<>(true).restrict(equal.not());
    }

    public PrimitiveVS<Machine> getNextSender(List<PrimitiveVS> candidateSenders) {
        PrimitiveVS<Machine> choices = (PrimitiveVS<Machine>) NondetUtil.getNondetChoice(candidateSenders);
        schedule.addRepeatSender(choices, choiceDepth);
        choiceDepth++;
        return choices;
    }

    public PrimitiveVS<Machine> getNextSender() {
        return getNextSender(getNextSenderChoices());
    }

    public void step() {
        PrimitiveVS<Machine> choices = getNextSender();

        if (choices.isEmptyVS()) {
            TraceLogger.finished(depth);
            done = true;
            return;
        }

        Message effect = null;
        List<Message> effects = new ArrayList<>();
        int numMessages = 0;
        for (GuardedValue<Machine> sender : choices.getGuardedValues()) {
            Machine machine = sender.getValue();
            Guard guard = sender.getGuard();
            Message removed = machine.sendBuffer.remove(guard);
            if (addReceiverQueueSemantics()) receiverQueue.remove(guard, machine, removed.getTarget());
            if (configuration.isCollectStats()) {
                numMessages += Concretizer.getNumConcreteValues(Guard.constTrue(), removed);
            }
            if (effect == null) {
                effect = removed;
            } else {
                effects.add(removed);
            }
        }
        assert effect != null;
        effect = effect.merge(effects);
        TraceLogger.schedule(depth, effect);

        performEffect(effect);


        if (configuration.isCollectStats()) {
          System.out.println("--------------------");
          System.out.println("Memory Stats::");
          Runtime runtime = Runtime.getRuntime();
          long memoryMax = runtime.maxMemory();
          long memoryUsed = runtime.totalMemory() - runtime.freeMemory();
          double memoryUsedPercent = (memoryUsed * 100.0) / memoryMax;
          System.out.println("memoryUsed::" + memoryUsed + ", memoryUsedPercent: " + memoryUsedPercent);
          System.out.println("--------------------");
        }

        // performing node clean-up
        BDDEngine.UnusedNodesCleanUp();
        System.gc();

        // add depth statistics
        if (configuration.isCollectStats()) {
          SearchStats.DepthStats depthStats = new SearchStats.DepthStats(depth, numMessages, Concretizer.getNumConcreteValues(Guard.constTrue(), effect), Concretizer.getNumConcreteValues(Guard.constTrue(), effect.getTarget(), effect.getEvent()));
          searchStats.addDepthStatistics(depth, depthStats);
          //SearchLogger.logDepthStats(depthStats);
          System.out.println("--------------------");
          System.out.println("Collect Stats::");
          System.out.println("Total transitions:: " + depthStats.getNumOfTransitions() + ", Total Merged Transitions (merged same target):: " + depthStats.getNumOfMergedTransitions() + ", Total Transitions Explored:: " + depthStats.getNumOfTransitionsExplored());
          System.out.println("Running Total Transitions:: " + searchStats.getSearchTotal().getNumOfTransitions() + ", Running Total Merged Transitions:: " + searchStats.getSearchTotal().getNumOfMergedTransitions() + ", Running Total Transitions Explored:: " + searchStats.getSearchTotal().getNumOfTransitionsExplored());
          System.out.println("--------------------");
        }
        depth++;
    }

    public PrimitiveVS<Machine> allocateMachine(Guard pc, Class<? extends Machine> machineType,
                                           Function<Integer, ? extends Machine> constructor) {
        if (!machineCounters.containsKey(machineType)) {
            machineCounters.put(machineType, new PrimitiveVS<>(0));
        }
        PrimitiveVS<Integer> guardedCount = machineCounters.get(machineType).restrict(pc);

        Machine newMachine;
        newMachine = constructor.apply(IntegerVS.maxValue(guardedCount));

        if (!machines.contains(newMachine)) {
            machines.add(newMachine);
        }

        TraceLogger.onCreateMachine(pc, newMachine);
        newMachine.setScheduler(this);
        schedule.makeMachine(newMachine, pc);

        guardedCount = IntegerVS.add(guardedCount, 1);
        PrimitiveVS<Integer> mergedCount = machineCounters.get(machineType).updateUnderGuard(pc, guardedCount);
        machineCounters.put(machineType, mergedCount);
        return new PrimitiveVS<>(newMachine).restrict(pc);
    }

    void runMonitors(Message event) {
        Map<Monitor, Guard> monitorConstraints = new HashMap<>();
        for (Monitor m : monitors) {
            monitorConstraints.put(m, Guard.constFalse());
        }
        for (GuardedValue<Event> e : event.getEvent().getGuardedValues()) {
            List<Monitor> listenersForEvent = listeners.get(e.getValue());
            if (listenersForEvent != null) {
                for (Monitor listener : listenersForEvent) {
                    monitorConstraints.computeIfPresent(listener, (k, v) -> v.or(e.getGuard()));
                }
            }
        }
        for (Monitor m: monitors) {
            Guard constraint = monitorConstraints.get(m);
            if (!constraint.isFalse()) {
                m.processEventToCompletion(constraint, event.restrict(constraint));
            }
        }
    }

    void performEffect(Message event) {
        runMonitors(event);
        for (GuardedValue<Machine> target : event.getTarget().getGuardedValues()) {
            target.getValue().processEventToCompletion(target.getGuard(), event.restrict(target.getGuard()));
        }
    }

    public void announce(PrimitiveVS<Event> names, UnionVS payload) {
        Message event = new Message(names, new PrimitiveVS<>(), payload);
        runMonitors(event);
    }

    public int getMaxInternalSteps() {
        return configuration.getMaxInternalSteps();
    }
}