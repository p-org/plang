package psym.runtime.scheduler;

import java.util.*;
import java.util.concurrent.TimeoutException;
import java.util.function.Function;
import java.util.stream.Collectors;
import lombok.Getter;
import psym.runtime.*;
import psym.runtime.Program;
import psym.runtime.logger.TraceLogger;
import psym.runtime.machine.Machine;
import psym.runtime.machine.Monitor;
import psym.runtime.machine.State;
import psym.runtime.machine.events.Event;
import psym.runtime.machine.events.Message;
import psym.runtime.scheduler.search.symmetry.SymmetryTracker;
import psym.runtime.statistics.SearchStats;
import psym.utils.Assert;
import psym.utils.exception.BugFoundException;
import psym.utils.random.NondetUtil;
import psym.valuesummary.*;

public abstract class Scheduler implements SchedulerInterface {
  @Getter
  /** List of all machines along any path constraints */
  protected final List<Machine> machines;
  /** Set of machines along current schedule */
  protected final SortedSet<Machine> currentMachines;
  /** Search statistics */
  protected final SearchStats searchStats = new SearchStats();
  /** Program */
  @Getter private final Program program;
  /** The scheduling choices made */
  public Schedule schedule;
  /** Whether final result is set or not */
  public boolean isFinalResult = false;
  /** How many instances of each Machine there are */
  protected Map<Class<? extends Machine>, PrimitiveVS<Integer>> machineCounters;
  /** Whether or not search is done */
  protected Guard done = Guard.constFalse();

  /** Choice depth */
  protected int choiceDepth = 0;
  /** Current depth of exploration */
  protected int depth = 0;
  /** Flag whether current step is a create or sync machine step */
  protected Boolean stickyStep = true;
  /** Flag whether current execution finished */
  protected Guard allMachinesHalted = Guard.constFalse();
  /** Flag whether check for liveness at the end */
  protected boolean terminalLivenessEnabled = true;
  /** List of monitors instances */
  List<Monitor> monitors;
  /** The machine to start with */
  private Machine start;
  /** The map from events to listening monitors */
  private Map<Event, List<Monitor>> listeners;

  /**
   * Make a new Scheduler
   *
   * @param machines The machines initially in the Scheduler
   */
  protected Scheduler(Program p, Machine... machines) {
    program = p;
    this.schedule = getNewSchedule(PSymGlobal.getSymmetryTracker());
    this.machines = new ArrayList<>();
    this.currentMachines = new TreeSet<>();
    this.machineCounters = new HashMap<>();

    for (Machine machine : machines) {
      this.machines.add(machine);
      this.currentMachines.add(machine);
      if (this.machineCounters.containsKey(machine.getClass())) {
        this.machineCounters.put(
            machine.getClass(), IntegerVS.add(this.machineCounters.get(machine.getClass()), 1));
      } else {
        this.machineCounters.put(machine.getClass(), new PrimitiveVS<>(1));
      }
      TraceLogger.onCreateMachine(Guard.constTrue(), machine);
      machine.setScheduler(this);
      schedule.makeMachine(machine, Guard.constTrue());
    }
  }

  public abstract void doSearch() throws TimeoutException, InterruptedException;

  public abstract void resumeSearch() throws TimeoutException, InterruptedException;

  protected abstract void performSearch() throws TimeoutException;

  protected abstract void step() throws TimeoutException;

  public abstract PrimitiveVS<Machine> allocateMachine(
          Guard pc,
          Class<? extends Machine> machineType,
          Function<Integer, ? extends Machine> constructor);

  public abstract PrimitiveVS<Machine> getNextSchedulingChoice();

  /**
   * Find out whether symbolic execution is done
   *
   * @return Whether or not there are more steps to run
   */
  public boolean isDone() {
    return done.isTrue() || depth == PSymGlobal.getConfiguration().getMaxStepBound();
  }

  /**
   * Get current depth
   *
   * @return current depth
   */
  public int getDepth() {
    return depth;
  }

  /**
   * Get current choice depth
   *
   * @return current choice depth
   */
  public int getChoiceDepth() {
    return choiceDepth;
  }

  /**
   * Make new schedule
   *
   * @return A new Schedule instance
   */
  public Schedule getNewSchedule(SymmetryTracker symmetryTracker) {
    return new Schedule(symmetryTracker);
  }

  /**
   * Get the schedule
   *
   * @return The schedule
   */
  public Schedule getSchedule() {
    return schedule;
  }

  public List<PrimitiveVS> getNextIntegerChoices(PrimitiveVS<Integer> bound, Guard pc) {
    List<PrimitiveVS> choices = new ArrayList<>();
    Guard zeroGuard = bound.getGuardFor(0);
    if (!zeroGuard.isFalse()) {
      bound = bound.updateUnderGuard(zeroGuard, new PrimitiveVS<Integer>(1));
    }
    int maxBound = IntegerVS.maxValue(bound);
    if (maxBound > 10000) {
      Guard maxBoundGuard = bound.getGuardFor(maxBound);
      throw new BugFoundException(
              String.format("choose expects a parameter with at most 10,000 choices, got %d choices instead.", maxBound),
              maxBoundGuard);
    }
    for (int i = 0; i < maxBound; i++) {
      Guard cond = IntegerVS.lessThan(i, bound).getGuardFor(true);
      choices.add(new PrimitiveVS<>(i).restrict(cond).restrict(pc));
    }
    return choices;
  }

  public PrimitiveVS<Integer> getNextInteger(List<PrimitiveVS> candidateIntegers) {
    PrimitiveVS<Integer> choices =
        (PrimitiveVS<Integer>) NondetUtil.getNondetChoice(candidateIntegers);
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
    PrimitiveVS<Boolean> choices =
        (PrimitiveVS<Boolean>) NondetUtil.getNondetChoice(candidateBooleans);
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
    int maxSize = IntegerVS.maxValue(size);
    if (maxSize > 10000) {
      Guard maxSizeGuard = size.getGuardFor(maxSize);
      throw new BugFoundException(
              String.format("choose expects a parameter with at most 10,000 choices, got %d choices instead.", maxSize),
              maxSizeGuard);
    }
    PrimitiveVS<Integer> index = new PrimitiveVS<>(0).restrict(size.getUniverse());
    List<ValueSummary> list = new ArrayList<>();
    while (BooleanVS.isEverTrue(IntegerVS.lessThan(index, size))) {
      Guard cond = BooleanVS.getTrueGuard(IntegerVS.lessThan(index, size));
      if (cond.isTrue()) {
        list.add(candidates.get(index).restrict(pc));
      } else {
        list.add(candidates.restrict(cond).get(index).restrict(pc));
      }
      index = IntegerVS.add(index, 1);
    }
    return list;
  }

  public PrimitiveVS<ValueSummary> getNextElementHelper(List<ValueSummary> candidates) {
    PrimitiveVS<ValueSummary> choices =
            NondetUtil.getNondetChoice(
                    candidates.stream()
                            .map(x -> new PrimitiveVS(x).restrict(x.getUniverse()))
                            .collect(Collectors.toList()));
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

  protected abstract ValueSummary getNextPrimitiveList(ListVS<? extends ValueSummary> s, Guard pc);

  @Override
  public ValueSummary getNextElement(ListVS<? extends ValueSummary> s, Guard pc) {
    if (s.getItems().size() != 0) {
      if (!(s.getItems().get(0) instanceof PrimitiveVS)) {
        PrimitiveVS<Integer> idx = getNextInteger(s.size(), pc);
        return s.get(idx);
      }
    }
    return getNextPrimitiveList(s, pc);
  }

  @Override
  public ValueSummary getNextElement(SetVS<? extends ValueSummary> s, Guard pc) {
    return getNextElement(s.getElements(), pc);
  }

  @Override
  public ValueSummary getNextElement(
      MapVS<?, ? extends ValueSummary, ? extends ValueSummary> s, Guard pc) {
    return getNextElement(s.getKeys(), pc);
  }

  /**
   * Start execution with the specified machine
   *
   * @param machine Machine to start execution with
   */
  public void startWith(Machine machine) {
    if (this.machineCounters.containsKey(machine.getClass())) {
      this.machineCounters.put(
          machine.getClass(), IntegerVS.add(this.machineCounters.get(machine.getClass()), 1));
    } else {
      this.machineCounters.put(machine.getClass(), new PrimitiveVS<>(1));
    }

    machines.add(machine);
    currentMachines.add(machine);
    start = machine;
    TraceLogger.onCreateMachine(Guard.constTrue(), machine);
    machine.setScheduler(this);
    schedule.makeMachine(machine, Guard.constTrue());

    performEffect(new Message(Event.createMachine, new PrimitiveVS<>(machine), null));
  }

  public void initializeSearch() {
    assert (getDepth() == 0);

    if (PSymGlobal.getConfiguration().isChoiceOrchestrationLearning()) {
      PSymGlobal.getChoiceLearningStats()
          .setProgramStateHash(this, PSymGlobal.getConfiguration().getChoiceLearningStateMode(), null);
    }
    listeners = program.getListeners();
    monitors = new ArrayList<>(program.getMonitors());
    for (Machine m : program.getMonitors()) {
      startWith(m);
    }
    Machine target = program.getStart();
    startWith(target);
    start = target;
  }

  protected void checkLiveness(Guard finished_orig) {
    Guard finished = finished_orig.and(schedule.getFilter());
    if (!finished.isFalse()) {
      for (Monitor m : monitors) {
        PrimitiveVS<State> monitorState = m.getCurrentState().restrict(finished);
        for (GuardedValue<State> entry : monitorState.getGuardedValues()) {
          State s = entry.getValue();
          if (s.isHotState()) {
            Guard g = entry.getGuard();
            if (!allMachinesHalted.isFalse()) {
              Assert.liveness(
                  g.isFalse(),
                  String.format(
                      "Monitor %s detected liveness bug in hot state %s at the end of program execution",
                      m, s),
                  g);
            } else {
              Assert.liveness(
                  g.isFalse(),
                  String.format("Monitor %s detected potential liveness bug in hot state %s", m, s),
                  g);
            }
          }
        }
      }
    }
  }

  protected Message rmBuffer(Machine m, Guard g) {
    return m.getEventBuffer().remove(g);
  }

  protected void removeHalted() {
    // remove messages with halted target
    for (Machine machine : machines) {
      while (!machine.getEventBuffer().isEmpty()) {
        Guard targetHalted =
                machine.getEventBuffer().satisfiesPredUnderGuard(x -> x.targetHalted()).getGuardFor(true);
        if (!targetHalted.isFalse()) {
          rmBuffer(machine, targetHalted);
          continue;
        }
        break;
      }
    }
  }

  public Machine setupNewMachine(
      Guard pc,
      PrimitiveVS<Integer> guardedCount,
      Function<Integer, ? extends Machine> constructor) {
    Machine newMachine = constructor.apply(IntegerVS.maxValue(guardedCount));

    if (!machines.contains(newMachine)) {
      machines.add(newMachine);
    }
    currentMachines.add(newMachine);
    assert (machines.size() >= currentMachines.size());

    TraceLogger.onCreateMachine(pc, newMachine);
    newMachine.setScheduler(this);
    schedule.makeMachine(newMachine, pc);
    return newMachine;
  }

  public void runMonitors(Message event) {
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
    for (Monitor m : monitors) {
      Guard constraint = monitorConstraints.get(m);
      if (!constraint.isFalse()) {
        m.processEventToCompletion(constraint, event.restrict(constraint));
      }
    }
  }

  public void performEffect(Message event) {
    for (GuardedValue<Machine> target : event.getTarget().getGuardedValues()) {
      target
          .getValue()
          .processEventToCompletion(target.getGuard(), event.restrict(target.getGuard()));
    }
  }

  public void announce(PrimitiveVS<Event> names, UnionVS payload) {
    Message event = new Message(names, new PrimitiveVS<>(), payload);
    if (event.hasNullEvent()) {
      throw new RuntimeException(String.format("Machine cannot announce a null event: %s", event));
    }
    runMonitors(event);
  }

  public int getMaxInternalSteps() {
    return PSymGlobal.getConfiguration().getMaxInternalSteps();
  }
}
