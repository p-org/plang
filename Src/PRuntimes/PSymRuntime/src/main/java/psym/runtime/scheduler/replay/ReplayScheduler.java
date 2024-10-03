package psym.runtime.scheduler.replay;

import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.TimeoutException;
import java.util.function.Function;
import lombok.Getter;
import org.apache.commons.lang3.NotImplementedException;
import psym.runtime.PSymGlobal;
import psym.runtime.Program;
import psym.runtime.logger.*;
import psym.runtime.machine.Machine;
import psym.runtime.machine.events.Message;
import psym.runtime.scheduler.Schedule;
import psym.runtime.scheduler.Scheduler;
import psym.runtime.scheduler.search.symmetry.SymmetryMode;
import psym.utils.Assert;
import psym.utils.exception.BugFoundException;
import psym.utils.exception.LivenessException;
import psym.valuesummary.*;

public class ReplayScheduler extends Scheduler {
  @Getter
  /** Path constraint */
  private final Guard pathConstraint;

  public ReplayScheduler(
      Program p,
      Schedule schedule,
      Guard pc) {
    super(p);
    TraceLogger.enable();
    this.schedule = schedule.guard(pc).getSingleSchedule();
    for (Machine machine : schedule.getMachines()) {
      machine.reset();
    }
    PSymGlobal.getConfiguration().setToReplay();
    pathConstraint = pc;
  }

  /**
   * Read scheduler state from a file
   *
   * @param readFromFile Name of the input file containing the scheduler state
   * @return A scheduler object
   * @throws Exception Throw error if reading fails
   */
  public static ReplayScheduler readFromFile(String readFromFile) throws Exception {
    ReplayScheduler result;
    try {
      PSymLogger.info(".. Reading schedule from file " + readFromFile);
      throw new NotImplementedException("Replaying a schedule is currently unsupported.");
//      PSymLogger.info(".. Successfully read.");
    } catch (NotImplementedException e) {
      e.printStackTrace();
      throw new RuntimeException(".. Failed to read schedule from file " + readFromFile, e);
    }
//    return result;
  }

  @Override
  public void doSearch() throws TimeoutException {
    TraceLogger.logStartReplayCex();
    ScheduleWriter.logHeader();
    TextWriter.logHeader();
    initializeSearch();
    performSearch();
  }

  @Override
  public void resumeSearch() throws InterruptedException {
    throw new InterruptedException("Not implemented");
  }

  @Override
  protected void performSearch() throws TimeoutException {
    while (!isDone()) {
      Assert.prop(
          getDepth() < PSymGlobal.getConfiguration().getMaxStepBound(),
          "Maximum allowed depth " + PSymGlobal.getConfiguration().getMaxStepBound() + " exceeded",
          schedule.getLengthCond(schedule.size()));
      step();
      if (Assert.getFailureType().equals("liveness")) {
        checkLiveness(allMachinesHalted);
      }
    }
    if (Assert.getFailureType().equals("liveness")) {
      checkLiveness(Guard.constTrue());
    }
    if (Assert.getFailureType().equals("cycle")) {
      throw new LivenessException(Assert.getFailureMsg(), Guard.constTrue());
    }
    Assert.prop(
        !PSymGlobal.getConfiguration().isFailOnMaxStepBound() || (getDepth() < PSymGlobal.getConfiguration().getMaxStepBound()),
        "Scheduling steps bound of " + PSymGlobal.getConfiguration().getMaxStepBound() + " reached.",
        schedule.getLengthCond(schedule.size()));
    if (done.isTrue()) {
      searchStats.setIterationCompleted();
    }
  }

  @Override
  public void step() {
    allMachinesHalted = Guard.constFalse();

    removeHalted();

    PrimitiveVS<Machine> schedulingChoices = getNextSchedulingChoice();

    if (schedulingChoices.isEmptyVS()) {
      done = Guard.constTrue();
      SearchLogger.finishedExecution(depth);
    }

    if (done.isTrue()) {
      return;
    }

    List<GuardedValue<Machine>> schedulingChoicesGv = schedulingChoices.getGuardedValues();
    assert (schedulingChoicesGv.size() == 1);

    GuardedValue<Machine> schedulingChoice = schedulingChoicesGv.get(0);
    Machine machine = schedulingChoice.getValue();
    Guard guard = schedulingChoice.getGuard();
    Message removed = rmBuffer(machine, guard);

    Message effect = removed;
    assert effect != null;

    stickyStep = false;
    if (!effect.isCreateMachine().getGuardFor(true).isFalse()
        || !effect.isSyncEvent().getGuardFor(true).isFalse()) {
      stickyStep = true;
    }
    if (!stickyStep) {
      depth++;
    }

    TraceLogger.schedule(depth, effect, machine);

    performEffect(effect);
  }

  @Override
  public PrimitiveVS<Machine> allocateMachine(
          Guard pc,
          Class<? extends Machine> machineType,
          Function<Integer, ? extends Machine> constructor) {
    if (!machineCounters.containsKey(machineType)) {
      machineCounters.put(machineType, new PrimitiveVS<>(0));
    }
    PrimitiveVS<Integer> guardedCount = machineCounters.get(machineType).restrict(pc);

    PrimitiveVS<Machine> allocated;
    assert (schedule.hasMachine(machineType, guardedCount, pc));
    allocated = schedule.getMachine(machineType, guardedCount).restrict(pc);
    for (GuardedValue gv : allocated.getGuardedValues()) {
      Guard g = gv.getGuard();
      Machine m = (Machine) gv.getValue();
      assert (!BooleanVS.isEverTrue(m.hasStarted().restrict(g)));
      TraceLogger.onCreateMachine(pc.and(g), m);
      if (!machines.contains(m)) {
        machines.add(m);
      }
      currentMachines.add(m);
      assert (machines.size() >= currentMachines.size());
      m.setScheduler(this);
      if (PSymGlobal.getConfiguration().getSymmetryMode() != SymmetryMode.None) {
        PSymGlobal.getSymmetryTracker().createMachine(m, g);
      }
    }

    guardedCount = IntegerVS.add(guardedCount, 1);

    PrimitiveVS<Integer> mergedCount =
            machineCounters.get(machineType).updateUnderGuard(pc, guardedCount);
    machineCounters.put(machineType, mergedCount);
    return allocated;
  }

  @Override
  public PrimitiveVS<Machine> getNextSchedulingChoice() {
    PrimitiveVS<Machine> res = schedule.getRepeatSchedulingChoice(choiceDepth);
    if (res.isEmptyVS()) {
      allMachinesHalted = Guard.constTrue();
    }
    choiceDepth++;
    return res;
  }

  @Override
  public PrimitiveVS<Boolean> getNextBoolean(Guard pc) {
    PrimitiveVS<Boolean> res = schedule.getRepeatBool(choiceDepth);
    ScheduleWriter.logBoolean(res);
    choiceDepth++;
    return res;
  }

  @Override
  public PrimitiveVS<Integer> getNextInteger(PrimitiveVS<Integer> bound, Guard pc) {
    int maxBound = IntegerVS.maxValue(bound);
    if (maxBound > 10000) {
      Guard maxBoundGuard = bound.getGuardFor(maxBound);
      throw new BugFoundException(
              String.format("choose expects a parameter with at most 10,000 choices, got %d choices instead.", maxBound),
              maxBoundGuard);
    }
    PrimitiveVS<Integer> res = schedule.getRepeatInt(choiceDepth);
    ScheduleWriter.logInteger(res);
    choiceDepth++;
    return res;
  }

  @Override
  public ValueSummary getNextPrimitiveList(ListVS<? extends ValueSummary> candidates, Guard pc) {
    PrimitiveVS<Integer> size = candidates.size();
    int maxSize = IntegerVS.maxValue(size);
    if (maxSize > 10000) {
      Guard maxSizeGuard = size.getGuardFor(maxSize);
      throw new BugFoundException(
              String.format("choose expects a parameter with at most 10,000 choices, got %d choices instead.", maxSize),
              maxSizeGuard);
    }
    ValueSummary res = getNextElementFlattener(schedule.getRepeatElement(choiceDepth));
    List<GuardedValue<?>> gv = ValueSummary.getGuardedValues(res);
    assert (gv.size() == 1);
    ScheduleWriter.logElement(gv);
    choiceDepth++;
    return res;
  }

  @Override
  public boolean isDone() {
    return super.isDone() || this.getChoiceDepth() >= schedule.size();
  }
}
