// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PChecker.Exceptions;
using PChecker.SystematicTesting.Operations;
using PChecker.SystematicTesting.Strategies;
using PChecker.SystematicTesting.Traces;
using Debug = PChecker.IO.Debugging.Debug;

namespace PChecker.SystematicTesting
{
    /// <summary>
    /// Implements a scheduler that serializes and schedules controlled operations.
    /// </summary>
    internal sealed class OperationScheduler
    {
        /// <summary>
        /// The checkerConfiguration used by the scheduler.
        /// </summary>
        private readonly CheckerConfiguration CheckerConfiguration;

        /// <summary>
        /// The controlled runtime.
        /// </summary>
        private readonly ControlledRuntime Runtime;

        /// <summary>
        /// The scheduling strategy used for program exploration.
        /// </summary>
        private readonly ISchedulingStrategy Strategy;

        /// <summary>
        /// Map from unique ids to asynchronous operations.
        /// </summary>
        private readonly ConcurrentDictionary<ulong, AsyncOperation> OperationMap;

        /// <summary>
        /// Map from ids of tasks that are controlled by the runtime to operations.
        /// </summary>
        internal readonly ConcurrentDictionary<int, AsyncOperation> ControlledTaskMap;

        /// <summary>
        /// The program schedule trace.
        /// </summary>
        internal ScheduleTrace ScheduleTrace;

        /// <summary>
        /// The scheduler completion source.
        /// </summary>
        private readonly TaskCompletionSource<bool> CompletionSource;

        /// <summary>
        /// Checks if the scheduler is running.
        /// </summary>
        internal bool IsRunning { get; private set; }

        /// <summary>
        /// The currently scheduled asynchronous operation.
        /// </summary>
        internal AsyncOperation ScheduledOperation { get; private set; }

        /// <summary>
        /// Number of scheduled steps.
        /// </summary>
        internal int ScheduledSteps => Strategy.GetScheduledSteps();

        /// <summary>
        /// Checks if the schedule has been fully explored.
        /// </summary>
        internal bool HasFullyExploredSchedule { get; private set; }

        /// <summary>
        /// True if a bug was found.
        /// </summary>
        internal bool BugFound { get; private set; }

        /// <summary>
        /// Bug report.
        /// </summary>
        internal string BugReport { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="OperationScheduler"/> class.
        /// </summary>
        internal OperationScheduler(ControlledRuntime runtime, ISchedulingStrategy strategy,
            ScheduleTrace trace, CheckerConfiguration checkerConfiguration)
        {
            CheckerConfiguration = checkerConfiguration;
            Runtime = runtime;
            Strategy = strategy;
            OperationMap = new ConcurrentDictionary<ulong, AsyncOperation>();
            ControlledTaskMap = new ConcurrentDictionary<int, AsyncOperation>();
            ScheduleTrace = trace;
            CompletionSource = new TaskCompletionSource<bool>();
            IsRunning = true;
            BugFound = false;
            HasFullyExploredSchedule = false;
        }

        /// <summary>
        /// Schedules the next enabled operation.
        /// </summary>
        internal void ScheduleNextEnabledOperation(AsyncOperationType type)
        {
            var taskId = Task.CurrentId;

            // TODO: figure out if this check is still needed.
            // If the caller is the root task, then return.
            if (taskId != null && taskId == Runtime.RootTaskId)
            {
                return;
            }

            AsyncOperation current = ScheduledOperation;
            if (!IsRunning)
            {
                // TODO: check if this stop is needed.
                Stop();

                if (current.Status != AsyncOperationStatus.Completed)
                {
                    // If scheduler is not running, throw exception to force terminate the current operation.
                    throw new ExecutionCanceledException();
                }
            }

            if (current.Status != AsyncOperationStatus.Completed)
            {
                // Checks if concurrency not controlled by the runtime was used.
                CheckNoExternalConcurrencyUsed();
            }

            // Checks if the scheduling steps bound has been reached.
            CheckIfSchedulingStepsBoundIsReached();

            // Update the operation type.
            current.Type = type;

            if (CheckerConfiguration.IsProgramStateHashingEnabled)
            {
                // Update the current operation with the hashed program state.
                current.HashedProgramState = Runtime.GetHashedProgramState();
            }

            // Get and order the operations by their id.
            var ops = OperationMap.Values.Where(op => op.Id < ulong.MaxValue).OrderBy(op => op.Id);

            // Try enable any operation that is currently waiting, but has its dependencies already satisfied.
            foreach (var op in ops)
            {
                if (op is AsyncOperation machineOp)
                {
                    machineOp.TryEnable();
                    Debug.WriteLine("<ScheduleDebug> Operation '{0}' has status '{1}'.", op.Id, op.Status);
                }
            }

            if (!Strategy.GetNextOperation(current, ops, out var next))
            {
                var op = GetOperationWithId<TaskOperation>(ulong.MaxValue);
                if (op?.Status is AsyncOperationStatus.Enabled)
                {
                    next = op;
                }
                else
                {
                    // Checks if the program has deadlocked.
                    CheckIfProgramHasDeadlocked(ops.Select(op => op as AsyncOperation));

                    Debug.WriteLine("<ScheduleDebug> Schedule explored.");
                    HasFullyExploredSchedule = true;
                    Stop();

                    if (current.Status != AsyncOperationStatus.Completed)
                    {
                        // The schedule is explored so throw exception to force terminate the current operation.
                        throw new ExecutionCanceledException();
                    }
                }
            }
            ScheduledOperation = next as AsyncOperation;
            ScheduleTrace.AddSchedulingChoice(next.Id);

            Debug.WriteLine($"<ScheduleDebug> Scheduling the next operation of '{next.Name}'.");

            if (current != next)
            {
                current.IsActive = false;
                lock (next)
                {
                    ScheduledOperation.IsActive = true;
                    Monitor.PulseAll(next);
                }

                lock (current)
                {
                    if (!current.IsHandlerRunning)
                    {
                        return;
                    }

                    if (!ControlledTaskMap.ContainsKey(Task.CurrentId.Value))
                    {
                        ControlledTaskMap.TryAdd(Task.CurrentId.Value, current);
                        Debug.WriteLine("<ScheduleDebug> Operation '{0}' is associated with task '{1}'.", current.Id, Task.CurrentId);
                    }

                    while (!current.IsActive)
                    {
                        Debug.WriteLine("<ScheduleDebug> Sleeping the operation of '{0}' on task '{1}'.", current.Name, Task.CurrentId);
                        Monitor.Wait(current);
                        Debug.WriteLine("<ScheduleDebug> Waking up the operation of '{0}' on task '{1}'.", current.Name, Task.CurrentId);
                    }

                    if (current.Status != AsyncOperationStatus.Enabled)
                    {
                        throw new ExecutionCanceledException();
                    }
                }
            }
        }

        /// <summary>
        /// Returns the next nondeterministic boolean choice.
        /// </summary>
        internal bool GetNextNondeterministicBooleanChoice(int maxValue)
        {
            if (!IsRunning)
            {
                // If scheduler is not running, throw exception to force terminate the caller.
                throw new ExecutionCanceledException();
            }

            // Checks if concurrency not controlled by the runtime was used.
            CheckNoExternalConcurrencyUsed();

            // Checks if the scheduling steps bound has been reached.
            CheckIfSchedulingStepsBoundIsReached();

            if (!Strategy.GetNextBooleanChoice(ScheduledOperation, maxValue, out var choice))
            {
                Debug.WriteLine("<ScheduleDebug> Schedule explored.");
                Stop();
                throw new ExecutionCanceledException();
            }

            ScheduleTrace.AddNondeterministicBooleanChoice(choice);
            return choice;
        }

        /// <summary>
        /// Returns the next nondeterministic integer choice.
        /// </summary>
        internal int GetNextNondeterministicIntegerChoice(int maxValue)
        {
            if (!IsRunning)
            {
                // If scheduler is not running, throw exception to force terminate the caller.
                throw new ExecutionCanceledException();
            }

            // Checks if concurrency not controlled by the runtime was used.
            CheckNoExternalConcurrencyUsed();

            // Checks if the scheduling steps bound has been reached.
            CheckIfSchedulingStepsBoundIsReached();

            if (!Strategy.GetNextIntegerChoice(ScheduledOperation, maxValue, out var choice))
            {
                Debug.WriteLine("<ScheduleDebug> Schedule explored.");
                Stop();
                throw new ExecutionCanceledException();
            }

            ScheduleTrace.AddNondeterministicIntegerChoice(choice);
            return choice;
        }

        /// <summary>
        /// Registers the specified asynchronous operation.
        /// </summary>
        /// <param name="op">The operation to register.</param>
        /// <returns>True if the operation was successfully registered, else false if it already exists.</returns>
        internal bool RegisterOperation(AsyncOperation op)
        {
            if (OperationMap.Count == 0)
            {
                ScheduledOperation = op;
            }

            return OperationMap.TryAdd(op.Id, op);
        }

        /// <summary>
        /// Schedules the specified asynchronous operation to execute on the task with the given id.
        /// </summary>
        /// <param name="op">The operation to schedule.</param>
        /// <param name="taskId">The id of the task to be used to execute the operation.</param>
        internal void ScheduleOperation(AsyncOperation op, int taskId)
        {
            Debug.WriteLine($"<ScheduleDebug> Scheduling operation '{op.Name}' to execute on task '{taskId}'.");
            ControlledTaskMap.TryAdd(taskId, op);
        }

        /// <summary>
        /// Starts the execution of the specified asynchronous operation.
        /// </summary>
        /// <param name="op">The operation to start executing.</param>
        internal static void StartOperation(AsyncOperation op)
        {
            Debug.WriteLine($"<ScheduleDebug> Starting the operation of '{op.Name}' on task '{Task.CurrentId}'.");

            lock (op)
            {
                op.IsHandlerRunning = true;
                Monitor.PulseAll(op);
                while (!op.IsActive)
                {
                    Debug.WriteLine($"<ScheduleDebug> Sleeping the operation of '{op.Name}' on task '{Task.CurrentId}'.");
                    Monitor.Wait(op);
                    Debug.WriteLine($"<ScheduleDebug> Waking up the operation of '{op.Name}' on task '{Task.CurrentId}'.");
                }

                if (op.Status != AsyncOperationStatus.Enabled)
                {
                    throw new ExecutionCanceledException();
                }
            }
        }

        /// <summary>
        /// Waits for the specified asynchronous operation to start executing.
        /// </summary>
        /// <param name="op">The operation to wait.</param>
        internal void WaitOperationStart(AsyncOperation op)
        {
            lock (op)
            {
                if (OperationMap.Count == 1)
                {
                    op.IsActive = true;
                    Monitor.PulseAll(op);
                }
                else
                {
                    while (!op.IsHandlerRunning)
                    {
                        Monitor.Wait(op);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the <see cref="IAsyncOperation"/> associated with the specified
        /// unique id, or null if no such operation exists.
        /// </summary>
        [DebuggerStepThrough]
        internal TAsyncOperation GetOperationWithId<TAsyncOperation>(ulong id)
            where TAsyncOperation : IAsyncOperation
        {
            if (OperationMap.TryGetValue(id, out var op) &&
                op is TAsyncOperation expected)
            {
                return expected;
            }

            return default;
        }

        /// <summary>
        /// Gets the <see cref="IAsyncOperation"/> that is executing on the current
        /// synchronization context, or null if no such operation is executing.
        /// </summary>
        [DebuggerStepThrough]
        internal TAsyncOperation GetExecutingOperation<TAsyncOperation>()
            where TAsyncOperation : IAsyncOperation
        {
            if (Task.CurrentId.HasValue &&
                ControlledTaskMap.TryGetValue(Task.CurrentId.Value, out var op) &&
                op is TAsyncOperation expected)
            {
                return expected;
            }

            return default;
        }

        /// <summary>
        /// Returns all registered operations.
        /// </summary>
        /// <remarks>
        /// This operation is thread safe because the systematic testing
        /// runtime serializes the execution.
        /// </remarks>
        internal IEnumerable<IAsyncOperation> GetRegisteredOperations() => OperationMap.Values;

        /// <summary>
        /// Returns the enabled operation ids.
        /// </summary>
        internal HashSet<ulong> GetEnabledOperationIds()
        {
            var enabledSchedulableIds = new HashSet<ulong>();
            foreach (var machineInfo in OperationMap.Values)
            {
                if (machineInfo.Status is AsyncOperationStatus.Enabled)
                {
                    enabledSchedulableIds.Add(machineInfo.Id);
                }
            }

            return enabledSchedulableIds;
        }

        /// <summary>
        /// Returns a test report with the scheduling statistics.
        /// </summary>
        internal TestReport GetReport()
        {
            var report = new TestReport(CheckerConfiguration);

            if (BugFound)
            {
                report.NumOfFoundBugs++;
                report.BugReports.Add(BugReport);
            }

            if (Strategy.IsFair())
            {
                report.NumOfExploredFairSchedules++;
                report.TotalExploredFairSteps += ScheduledSteps;

                if (report.MinExploredFairSteps < 0 ||
                    report.MinExploredFairSteps > ScheduledSteps)
                {
                    report.MinExploredFairSteps = ScheduledSteps;
                }

                if (report.MaxExploredFairSteps < ScheduledSteps)
                {
                    report.MaxExploredFairSteps = ScheduledSteps;
                }

                if (Strategy.HasReachedMaxSchedulingSteps())
                {
                    report.MaxFairStepsHitInFairTests++;
                }

                if (ScheduledSteps >= report.CheckerConfiguration.MaxUnfairSchedulingSteps)
                {
                    report.MaxUnfairStepsHitInFairTests++;
                }
            }
            else
            {
                report.NumOfExploredUnfairSchedules++;

                if (Strategy.HasReachedMaxSchedulingSteps())
                {
                    report.MaxUnfairStepsHitInUnfairTests++;
                }
            }

            return report;
        }

        /// <summary>
        /// Checks that no task that is not controlled by the runtime is currently executing.
        /// </summary>
#if !DEBUG
        [DebuggerHidden]
#endif
        internal void CheckNoExternalConcurrencyUsed()
        {
            if (!Task.CurrentId.HasValue || !ControlledTaskMap.ContainsKey(Task.CurrentId.Value))
            {
                NotifyAssertionFailure(string.Format(CultureInfo.InvariantCulture,
                    "Uncontrolled task '{0}' invoked a runtime method. Please make sure to avoid using concurrency APIs " +
                    "(e.g. 'Task.Run', 'Task.Delay' or 'Task.Yield' from the 'System.Threading.Tasks' namespace) inside " +
                    "state machine handlers or controlled tasks. If you are using external libraries that are executing concurrently, " +
                    "you will need to mock them during testing.",
                    Task.CurrentId.HasValue ? Task.CurrentId.Value.ToString() : "<unknown>"));
            }
        }

        /// <summary>
        /// Checks for a deadlock. This happens when there are no more enabled operations,
        /// but there is one or more blocked operations that are waiting to receive an event
        /// or for a task to complete.
        /// </summary>
#if !DEBUG
        [DebuggerHidden]
#endif
        private void CheckIfProgramHasDeadlocked(IEnumerable<AsyncOperation> ops)
        {
            var blockedOnReceiveOperations = ops.Where(op => op.Status is AsyncOperationStatus.BlockedOnReceive).ToList();
            var blockedOnWaitOperations = ops.Where(op => op.Status is AsyncOperationStatus.BlockedOnWaitAll ||
                                                          op.Status is AsyncOperationStatus.BlockedOnWaitAny).ToList();
            var blockedOnResourceSynchronization = ops.Where(op => op.Status is AsyncOperationStatus.BlockedOnResource).ToList();
            if (blockedOnReceiveOperations.Count == 0 &&
                blockedOnWaitOperations.Count == 0 &&
                blockedOnResourceSynchronization.Count == 0)
            {
                return;
            }

            var message = "Deadlock detected.";
            if (blockedOnReceiveOperations.Count > 0)
            {
                for (var i = 0; i < blockedOnReceiveOperations.Count; i++)
                {
                    message += string.Format(CultureInfo.InvariantCulture, " {0}", blockedOnReceiveOperations[i].Name);
                    if (i == blockedOnReceiveOperations.Count - 2)
                    {
                        message += " and";
                    }
                    else if (i < blockedOnReceiveOperations.Count - 1)
                    {
                        message += ",";
                    }
                }

                message += blockedOnReceiveOperations.Count == 1 ? " is " : " are ";
                message += "waiting to receive an event, but no other controlled tasks are enabled.";
            }

            if (blockedOnWaitOperations.Count > 0)
            {
                for (var i = 0; i < blockedOnWaitOperations.Count; i++)
                {
                    message += string.Format(CultureInfo.InvariantCulture, " {0}", blockedOnWaitOperations[i].Name);
                    if (i == blockedOnWaitOperations.Count - 2)
                    {
                        message += " and";
                    }
                    else if (i < blockedOnWaitOperations.Count - 1)
                    {
                        message += ",";
                    }
                }

                message += blockedOnWaitOperations.Count == 1 ? " is " : " are ";
                message += "waiting for a task to complete, but no other controlled tasks are enabled.";
            }

            if (blockedOnResourceSynchronization.Count > 0)
            {
                for (var i = 0; i < blockedOnResourceSynchronization.Count; i++)
                {
                    message += string.Format(CultureInfo.InvariantCulture, " {0}", blockedOnResourceSynchronization[i].Name);
                    if (i == blockedOnResourceSynchronization.Count - 2)
                    {
                        message += " and";
                    }
                    else if (i < blockedOnResourceSynchronization.Count - 1)
                    {
                        message += ",";
                    }
                }

                message += blockedOnResourceSynchronization.Count == 1 ? " is " : " are ";
                message += "waiting to acquire a resource that is already acquired, ";
                message += "but no other controlled tasks are enabled.";
            }

            NotifyAssertionFailure(message);
        }

        /// <summary>
        /// Checks if the scheduling steps bound has been reached. If yes,
        /// it stops the scheduler and kills all enabled machines.
        /// </summary>
#if !DEBUG
        [DebuggerHidden]
#endif
        internal void CheckIfSchedulingStepsBoundIsReached()
        {
            if (Strategy.HasReachedMaxSchedulingSteps())
            {
                var bound = Strategy.IsFair() ? CheckerConfiguration.MaxFairSchedulingSteps :
                    CheckerConfiguration.MaxUnfairSchedulingSteps;
                var message = $"Scheduling steps bound of {bound} reached.";

                if (CheckerConfiguration.ConsiderDepthBoundHitAsBug)
                {
                    NotifyAssertionFailure(message);
                }
                else
                {
                    Debug.WriteLine($"<ScheduleDebug> {message}");
                    Stop();

                    if (ScheduledOperation.Status != AsyncOperationStatus.Completed)
                    {
                        // The schedule is explored so throw exception to force terminate the current operation.
                        throw new ExecutionCanceledException();
                    }
                }
            }
        }

        /// <summary>
        /// Notify that an assertion has failed.
        /// </summary>
#if !DEBUG
        [DebuggerHidden]
#endif
        internal void NotifyAssertionFailure(string text, bool killTasks = true, bool cancelExecution = true)
        {
            if (!BugFound)
            {
                BugReport = text;

                Runtime.LogWriter.LogAssertionFailure($"<ErrorLog> {text}");
                var trace = new StackTrace();
                Runtime.RaiseOnFailureEvent(new AssertionFailureException(text));
                Runtime.LogWriter.LogStrategyDescription(CheckerConfiguration.SchedulingStrategy,
                    Strategy.GetDescription());

                BugFound = true;
            }

            if (killTasks)
            {
                Stop();
            }

            if (cancelExecution)
            {
                throw new ExecutionCanceledException();
            }
        }

        /// <summary>
        /// Waits until the scheduler terminates.
        /// </summary>
        internal Task WaitAsync() => CompletionSource.Task;

        /// <summary>
        /// Stops the scheduler.
        /// </summary>
        private void Stop()
        {
            IsRunning = false;
            KillRemainingOperations();

            // Check if the completion source is completed. If not synchronize on
            // it (as it can only be set once) and set its result.
            if (!CompletionSource.Task.IsCompleted)
            {
                lock (CompletionSource)
                {
                    if (!CompletionSource.Task.IsCompleted)
                    {
                        CompletionSource.SetResult(true);
                    }
                }
            }
        }

        /// <summary>
        /// Kills any remaining operations at the end of the schedule.
        /// </summary>
        private void KillRemainingOperations()
        {
            foreach (var operation in OperationMap.Values)
            {
                // This casting is always safe.
                var op = operation as AsyncOperation;
                op.IsActive = true;
                op.Status = AsyncOperationStatus.Canceled;

                if (op.IsHandlerRunning)
                {
                    lock (op)
                    {
                        Monitor.PulseAll(op);
                    }
                }
            }
        }
    }
}