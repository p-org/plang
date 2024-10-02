// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using PChecker.StateMachines;
using PChecker.StateMachines.Logging;
using PChecker.Coverage;
using PChecker.IO;
using PChecker.IO.Debugging;
using PChecker.IO.Logging;
using PChecker.Random;
using PChecker.Runtime;
using PChecker.SystematicTesting.Strategies;
using PChecker.SystematicTesting.Strategies.Exhaustive;
using PChecker.SystematicTesting.Strategies.Probabilistic;
using PChecker.SystematicTesting.Strategies.Special;
using PChecker.SystematicTesting.Traces;
using PChecker.Utilities;
using Task = PChecker.Tasks.Task;

namespace PChecker.SystematicTesting
{
    /// <summary>
    /// Testing engine that can run a controlled concurrency test using
    /// a specified checkerConfiguration.
    /// </summary>
    public class TestingEngine
    {
        /// <summary>
        /// CheckerConfiguration.
        /// </summary>
        private readonly CheckerConfiguration _checkerConfiguration;

        /// <summary>
        /// The method to test.
        /// </summary>
        private readonly TestMethodInfo TestMethodInfo;

        /// <summary>
        /// Set of callbacks to invoke at the end
        /// of each schedule.
        /// </summary>
        private readonly ISet<Action<int>> PerIterationCallbacks;

        /// <summary>
        /// The program exploration strategy.
        /// </summary>
        internal readonly ISchedulingStrategy Strategy;

        /// <summary>
        /// Random value generator used by the scheduling strategies.
        /// </summary>
        private readonly IRandomValueGenerator RandomValueGenerator;

        /// <summary>
        /// The error reporter.
        /// </summary>
        private readonly ErrorReporter ErrorReporter;

        /// <summary>
        /// The installed logger.
        /// </summary>
        private TextWriter Logger;

        /// <summary>
        /// Contains a single schedule of JSON log output in the case where the IsJsonLogEnabled
        /// checkerConfiguration is specified.
        /// </summary>
        private JsonWriter JsonLogger;

        /// <summary>
        /// Field declaration for the JsonVerboseLogs
        /// Structure representation is a list of the JsonWriter logs.
        /// [log iter 1, log iter 2, log iter 3, ...]
        /// </summary>
        private readonly List<List<LogEntry>> JsonVerboseLogs;

        /// <summary>
        /// Field declaration with default JSON serializer options
        /// </summary>
        private JsonSerializerOptions jsonSerializerConfig = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            Converters = { new EncodingConverter() }
        };

        internal class EncodingConverter : JsonConverter<Encoding>
        {
            public override Encoding Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                var name = reader.GetString();
                if (name == null)
                    return null;
                return Encoding.GetEncoding(name);
            }
            public override void Write(Utf8JsonWriter writer, Encoding value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.WebName);
            }
        }

        /// <summary>
        /// The profiler.
        /// </summary>
        private readonly Profiler Profiler;

        /// <summary>
        /// The testing task cancellation token source.
        /// </summary>
        private readonly CancellationTokenSource CancellationTokenSource;

        /// <summary>
        /// Data structure containing information
        /// gathered during testing.
        /// </summary>
        public TestReport TestReport { get; set; }

        /// <summary>
        /// A graph of the state machines, state machines and events of a single test schedule.
        /// </summary>
        private Graph Graph;

        /// <summary>
        /// Contains a single schedule of XML log output in the case where the IsXmlLogEnabled
        /// checkerConfiguration is specified.
        /// </summary>
        private StringBuilder XmlLog;

        /// <summary>
        /// The readable trace, if any.
        /// </summary>
        public string ReadableTrace { get; private set; }

        /// <summary>
        /// The reproducable trace, if any.
        /// </summary>
        public string ReproducableTrace { get; private set; }

        /// <summary>
        /// Checks if the systematic testing engine is running in replay mode.
        /// </summary>
        private bool IsReplayModeEnabled => Strategy is ReplayStrategy;

        /// <summary>
        /// A guard for printing info.
        /// </summary>
        private int PrintGuard;

        /// <summary>
        /// Creates a new systematic testing engine.
        /// </summary>
        public static TestingEngine Create(CheckerConfiguration checkerConfiguration) =>
            Create(checkerConfiguration, LoadAssembly(checkerConfiguration.AssemblyToBeAnalyzed));

        /// <summary>
        /// Creates a new systematic testing engine.
        /// </summary>
        public static TestingEngine Create(CheckerConfiguration checkerConfiguration, Assembly assembly)
        {
            if (checkerConfiguration.ListTestCases)
            {
                try
                {
                    var testMethods = TestMethodInfo.GetAllTestMethodsFromAssembly(assembly);
                    Console.Out.WriteLine($".. List of test cases (total {testMethods.Count})");

                    foreach (var mi in testMethods)
                    {
                        Console.Out.WriteLine($"{mi.DeclaringType.Name}");
                    }

                    Environment.Exit(0);
                }
                catch
                {
                    Error.ReportAndExit($"Failed to list test methods from assembly '{assembly.FullName}'");
                }
            }

            TestMethodInfo testMethodInfo = null;
            try
            {
                testMethodInfo = TestMethodInfo.GetFromAssembly(assembly, checkerConfiguration.TestCaseName);
                Console.Out.WriteLine($".. Test case :: {testMethodInfo.Name}");
            }
            catch
            {
                Error.ReportAndExit($"Failed to get test method '{checkerConfiguration.TestCaseName}' from assembly '{assembly.FullName}'");
            }

            return new TestingEngine(checkerConfiguration, testMethodInfo);
        }

        /// <summary>
        /// Creates a new systematic testing engine.
        /// </summary>
        public static TestingEngine Create(CheckerConfiguration checkerConfiguration, Action test) =>
            new TestingEngine(checkerConfiguration, test);
        
        /// <summary>
        /// Creates a new systematic testing engine.
        /// </summary>
        public static TestingEngine Create(CheckerConfiguration checkerConfiguration, Action<ControlledRuntime> test) =>
            new TestingEngine(checkerConfiguration, test);

        /// <summary>
        /// Creates a new systematic testing engine.
        /// </summary>
        public static TestingEngine Create(CheckerConfiguration checkerConfiguration, Func<Task> test) =>
            new TestingEngine(checkerConfiguration, test);

        /// <summary>
        /// Creates a new systematic testing engine.
        /// </summary>
        internal static TestingEngine Create(CheckerConfiguration checkerConfiguration, Func<ControlledRuntime, Task> test) =>
            new TestingEngine(checkerConfiguration, test);

        /// <summary>
        /// Initializes a new instance of the <see cref="TestingEngine"/> class.
        /// </summary>
        internal TestingEngine(CheckerConfiguration checkerConfiguration, Delegate test)
            : this(checkerConfiguration, new TestMethodInfo(test))
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TestingEngine"/> class.
        /// </summary>
        private TestingEngine(CheckerConfiguration checkerConfiguration, TestMethodInfo testMethodInfo)
        {
            _checkerConfiguration = checkerConfiguration;
            TestMethodInfo = testMethodInfo;

            Logger = new ConsoleLogger();
            ErrorReporter = new ErrorReporter(checkerConfiguration, Logger);
            Profiler = new Profiler();

            PerIterationCallbacks = new HashSet<Action<int>>();

            // Initializes scheduling strategy specific components.
            RandomValueGenerator = new RandomValueGenerator(checkerConfiguration);

            TestReport = new TestReport(checkerConfiguration);
            ReadableTrace = string.Empty;
            ReproducableTrace = string.Empty;

            CancellationTokenSource = new CancellationTokenSource();
            PrintGuard = 1;

            // Initialize a new instance of JsonVerboseLogs if running in verbose mode.
            if (checkerConfiguration.IsVerbose)
            {
                JsonVerboseLogs = new List<List<LogEntry>>();
            }

            if (checkerConfiguration.SchedulingStrategy is "replay")
            {
                var scheduleDump = GetScheduleForReplay(out var isFair);
                var schedule = new ScheduleTrace(scheduleDump);
                Strategy = new ReplayStrategy(checkerConfiguration, schedule, isFair);
            }
            else if (checkerConfiguration.SchedulingStrategy is "random")
            {
                Strategy = new RandomStrategy(checkerConfiguration.MaxFairSchedulingSteps, RandomValueGenerator);
            }
            else if (checkerConfiguration.SchedulingStrategy is "statistical")
            {
                Strategy = new StatisticalStrategy(checkerConfiguration.MaxFairSchedulingSteps, RandomValueGenerator);
            }
            else if (checkerConfiguration.SchedulingStrategy is "pct")
            {
                Strategy = new PCTStrategy(checkerConfiguration.MaxUnfairSchedulingSteps, checkerConfiguration.StrategyBound,
                    RandomValueGenerator);
            }
            else if (checkerConfiguration.SchedulingStrategy is "fairpct")
            {
                var prefixLength = checkerConfiguration.MaxUnfairSchedulingSteps;
                var prefixStrategy = new PCTStrategy(prefixLength, checkerConfiguration.StrategyBound, RandomValueGenerator);
                var suffixStrategy = new RandomStrategy(checkerConfiguration.MaxFairSchedulingSteps, RandomValueGenerator);
                Strategy = new ComboStrategy(prefixStrategy, suffixStrategy);
            }
            else if (checkerConfiguration.SchedulingStrategy is "probabilistic")
            {
                Strategy = new ProbabilisticRandomStrategy(checkerConfiguration.MaxFairSchedulingSteps,
                    checkerConfiguration.StrategyBound, RandomValueGenerator);
            }
            else if (checkerConfiguration.SchedulingStrategy is "rl")
            {
                Strategy = new QLearningStrategy(checkerConfiguration.MaxUnfairSchedulingSteps, RandomValueGenerator);
            }
            else if (checkerConfiguration.SchedulingStrategy is "dfs")
            {
                Strategy = new DFSStrategy(checkerConfiguration.MaxUnfairSchedulingSteps);
            }
            else if (checkerConfiguration.SchedulingStrategy is "portfolio")
            {
                Error.ReportAndExit("Portfolio testing strategy is only " +
                                    "available in parallel testing.");
            }

            if (checkerConfiguration.SchedulingStrategy != "replay" &&
                checkerConfiguration.ScheduleFile.Length > 0)
            {
                var scheduleDump = GetScheduleForReplay(out var isFair);
                var schedule = new ScheduleTrace(scheduleDump);
                Strategy = new ReplayStrategy(checkerConfiguration, schedule, isFair, Strategy);
            }
        }

        /// <summary>
        /// Runs the testing engine.
        /// </summary>
        public void Run()
        {
            try
            {
                var task = CreateTestingTask();
                if (_checkerConfiguration.Timeout > 0)
                {
                    CancellationTokenSource.CancelAfter(
                        _checkerConfiguration.Timeout * 1000);
                }

                Profiler.StartMeasuringExecutionTime();
                if (!CancellationTokenSource.IsCancellationRequested)
                {
                    task.Start();
                    task.Wait(CancellationTokenSource.Token);
                }
            }
            catch (OperationCanceledException)
            {
                if (CancellationTokenSource.IsCancellationRequested)
                {
                    Logger.WriteLine($"... Checker timed out.");
                }
            }
            catch (AggregateException aex)
            {
                if (aex.InnerException is OutOfMemoryException)
                {
                    Logger.WriteLine("... Checker ran out of memory.");
                }
                else
                {
                    aex.Handle((ex) =>
                    {
                        Debug.WriteLine(ex.Message);
                        Debug.WriteLine(ex.StackTrace);
                        return true;
                    });

                    if (aex.InnerException is FileNotFoundException)
                    {
                        Error.ReportAndExit($"{aex.InnerException.Message}");
                    }


                    Error.ReportAndExit("Exception thrown during testing outside the context of an state machine, " +
                                        "possibly in a test method. Please use /debug /v:2 to print more information.");
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine($"... Checker failed due to an internal error: {ex}");
                TestReport.InternalErrors.Add(ex.ToString());
            }
            finally
            {
                Profiler.StopMeasuringExecutionTime();
            }
        }

        /// <summary>
        /// Creates a new testing task.
        /// </summary>
        private System.Threading.Tasks.Task CreateTestingTask()
        {
            var options = string.Empty;
            if (_checkerConfiguration.SchedulingStrategy is "random" ||
                _checkerConfiguration.SchedulingStrategy is "statistical" ||
                _checkerConfiguration.SchedulingStrategy is "pct" ||
                _checkerConfiguration.SchedulingStrategy is "fairpct" ||
                _checkerConfiguration.SchedulingStrategy is "probabilistic" ||
                _checkerConfiguration.SchedulingStrategy is "rl")
            {
                options = $" (seed:{RandomValueGenerator.Seed})";
            }

            Logger.WriteLine($"... Checker is " +
                             $"using '{_checkerConfiguration.SchedulingStrategy}' strategy{options}.");

            return new System.Threading.Tasks.Task(() =>
            {
                try
                {
                    // Invokes the user-specified initialization method.
                    TestMethodInfo.InitializeAllIterations();

                    var maxIterations = IsReplayModeEnabled ? 1 : _checkerConfiguration.TestingIterations;
                    int i = 0;
                    while (maxIterations == 0 || i < maxIterations)
                    {
                        if (CancellationTokenSource.IsCancellationRequested)
                        {
                            break;
                        }

                        if (Profiler.GetCurrentMemoryUsage() > _checkerConfiguration.MemoryLimit)
                        {
                            if (_checkerConfiguration.MemoryLimit != 0)
                            {
                                throw new OutOfMemoryException();
                            }
                        }

                        // Runs a new testing schedule.
                        RunNextIteration(i);

                        if (IsReplayModeEnabled || (!_checkerConfiguration.PerformFullExploration &&
                                                    TestReport.NumOfFoundBugs > 0) ||
                            !Strategy.PrepareForNextIteration())
                        {
                            break;
                        }

                        if (RandomValueGenerator != null && _checkerConfiguration.IncrementalSchedulingSeed)
                        {
                            // Increments the seed in the random number generator (if one is used), to
                            // capture the seed used by the scheduling strategy in the next schedule.
                            RandomValueGenerator.Seed += 1;
                        }

                        i++;
                    }

                    // Invokes the user-specified test disposal method.
                    TestMethodInfo.DisposeAllIterations();
                }
                catch (Exception ex)
                {
                    var innerException = ex;
                    while (innerException is TargetInvocationException)
                    {
                        innerException = innerException.InnerException;
                    }

                    if (innerException is AggregateException)
                    {
                        innerException = innerException.InnerException;
                    }

                    if (!(innerException is TaskCanceledException))
                    {
                        ExceptionDispatchInfo.Capture(innerException).Throw();
                    }
                }

                // Output JSON verbose logs at the end of Task
                if (_checkerConfiguration.IsVerbose)
                {
                    // Get the file path to output the json verbose logs file
                    var directory = _checkerConfiguration.OutputDirectory;
                    var file = Path.GetFileNameWithoutExtension(_checkerConfiguration.AssemblyToBeAnalyzed);
                    file += "_" + _checkerConfiguration.TestingProcessId;
                    var jsonVerbosePath = directory + file  + "_verbose.trace.json";

                    Logger.WriteLine("... Emitting verbose logs:");
                    Logger.WriteLine($"..... Writing {jsonVerbosePath}");

                    // Stream directly to the output file while serializing the JSON
                    using var jsonStreamFile = File.Create(jsonVerbosePath);
                    JsonSerializer.Serialize(jsonStreamFile, JsonVerboseLogs, jsonSerializerConfig);
                }

            }, CancellationTokenSource.Token);
        }

        /// <summary>
        /// Runs the next testing schedule.
        /// </summary>
        private void RunNextIteration(int schedule)
        {
            if (!IsReplayModeEnabled && ShouldPrintIteration(schedule + 1))
            {
                Logger.WriteLine($"..... Schedule #{schedule + 1}");

                // Flush when logging to console.
                if (Logger is ConsoleLogger)
                {
                    Console.Out.Flush();
                }
            }

            // Runtime used to serialize and test the program in this schedule.
            ControlledRuntime runtime = null;

            // Logger used to intercept the program output if no custom logger
            // is installed and if verbosity is turned off.
            InMemoryLogger runtimeLogger = null;

            // Gets a handle to the standard output and error streams.
            var stdOut = Console.Out;
            var stdErr = Console.Error;

            try
            {
                // Creates a new instance of the controlled runtime.
                _checkerConfiguration.CurrentIteration = schedule + 1;
                runtime = new ControlledRuntime(_checkerConfiguration, Strategy, RandomValueGenerator);

                // Always output a json log of the error
                JsonLogger = new JsonWriter();
                runtime.SetJsonLogger(JsonLogger);

                // If verbosity is turned off, then intercept the program log, and also redirect
                // the standard output and error streams to a nul logger.
                if (!_checkerConfiguration.IsVerbose)
                {
                    runtimeLogger = new InMemoryLogger();
                    runtime.SetLogger(runtimeLogger);

                    var writer = TextWriter.Null;
                    Console.SetOut(writer);
                    Console.SetError(writer);
                }

                InitializeCustomLogging(runtime);

                // Runs the test and waits for it to terminate.
                runtime.RunTest(TestMethodInfo.Method, TestMethodInfo.Name);
                runtime.WaitAsync().Wait();

                // Invokes the user-specified schedule disposal method.
                TestMethodInfo.DisposeCurrentIteration();

                // Invoke the per schedule callbacks, if any.
                foreach (var callback in PerIterationCallbacks)
                {
                    callback(schedule);
                }

                // Checks that no monitor is in a hot state at termination. Only
                // checked if no safety property violations have been found.
                if (!runtime.Scheduler.BugFound)
                {
                    runtime.CheckNoMonitorInHotStateAtTermination();
                }

                if (runtime.Scheduler.BugFound)
                {
                    ErrorReporter.WriteErrorLine(runtime.Scheduler.BugReport);
                }

                // Only add the current schedule of JsonLogger logs to JsonVerboseLogs if in verbose mode
                if (_checkerConfiguration.IsVerbose)
                {
                    JsonVerboseLogs.Add(JsonLogger.Logs);
                }

                runtime.LogWriter.LogCompletion();

                GatherTestingStatistics(runtime);

                if (!IsReplayModeEnabled && TestReport.NumOfFoundBugs > 0)
                {
                    if (runtimeLogger != null)
                    {
                        ReadableTrace = runtimeLogger.ToString();
                        ReadableTrace += TestReport.GetText(_checkerConfiguration, "<StrategyLog>");
                    }

                    ConstructReproducableTrace(runtime);
                }
            }
            finally
            {
                if (!_checkerConfiguration.IsVerbose)
                {
                    // Restores the standard output and error streams.
                    Console.SetOut(stdOut);
                    Console.SetError(stdErr);
                }

                if (!IsReplayModeEnabled && _checkerConfiguration.PerformFullExploration && runtime.Scheduler.BugFound)
                {
                    Logger.WriteLine($"..... Schedule #{schedule + 1} " +
                                     $"triggered bug #{TestReport.NumOfFoundBugs} " +
                                     $"[task-{_checkerConfiguration.TestingProcessId}]");
                }

                // Cleans up the runtime before the next schedule starts.
                runtimeLogger?.Dispose();
                runtime?.Dispose();
            }
        }

        /// <summary>
        /// Stops the testing engine.
        /// </summary>
        public void Stop()
        {
            CancellationTokenSource.Cancel();
        }

        /// <summary>
        /// Returns a report with the testing results.
        /// </summary>
        public string GetReport()
        {
            if (IsReplayModeEnabled)
            {
                var report = new StringBuilder();
                report.AppendFormat("... Reproduced {0} bug{1}.", TestReport.NumOfFoundBugs,
                    TestReport.NumOfFoundBugs == 1 ? string.Empty : "s");
                report.AppendLine();
                report.Append($"... Elapsed {Profiler.GetElapsedTime():0.##} sec and used {Profiler.GetMaxMemoryUsage():0.##} GB.");
                return report.ToString();
            }

            return TestReport.GetText(_checkerConfiguration, "...");
        }

        /// <summary>
        /// Returns an object where the value null is replaced with "null"
        /// </summary>
        public object RecursivelyReplaceNullWithString(object obj) {
            if (obj == null) {
                return "null";
            }
            if (obj is Dictionary<string, object> dictionaryStr) {
                var newDictionary = new Dictionary<string, object>();
                foreach (var item in dictionaryStr) {
                    var newVal = RecursivelyReplaceNullWithString(item.Value);
                    if (newVal != null)
                        newDictionary[item.Key] = newVal;
                }
                return newDictionary;
            }
            else if (obj is Dictionary<int, object> dictionaryInt) {
                var newDictionary = new Dictionary<int, object>();
                foreach (var item in dictionaryInt) {
                    var newVal = RecursivelyReplaceNullWithString(item.Value);
                    if (newVal != null)
                        newDictionary[item.Key] = newVal;
                }
                return newDictionary;
            }
            else if (obj is List<object> list) {
                var newList = new List<object>();
                foreach (var item in list) {
                    var newItem = RecursivelyReplaceNullWithString(item);
                    if (newItem != null)
                        newList.Add(newItem);
                }
                return newList;
            }
            else {
                return obj;
            }
        }

        /// <summary>
        /// Tries to emit the testing traces, if any.
        /// </summary>
        public void TryEmitTraces(string directory, string file)
        {
            var index = 0;
            // Find the next available file index.
            var match = new Regex("^(.*)_([0-9]+)_([0-9]+)");
            foreach (var path in Directory.GetFiles(directory))
            {
                var name = Path.GetFileName(path);
                if (name.StartsWith(file))
                {
                    var result = match.Match(name);
                    if (result.Success)
                    {
                        var value = result.Groups[3].Value;
                        if (int.TryParse(value, out var i))
                        {
                            index = Math.Max(index, i + 1);
                        }
                    }
                }
            }

            if (!_checkerConfiguration.PerformFullExploration)
            {
                // Emits the human readable trace, if it exists.
                if (!string.IsNullOrEmpty(ReadableTrace))
                {
                    var readableTracePath = directory + file + "_" + index + ".txt";

                    Logger.WriteLine($"..... Writing {readableTracePath}");
                    File.WriteAllText(readableTracePath, ReadableTrace);
                }
            }

            if (_checkerConfiguration.IsXmlLogEnabled)
            {
                var xmlPath = directory + file + "_" + index + ".trace.xml";
                Logger.WriteLine($"..... Writing {xmlPath}");
                File.WriteAllText(xmlPath, XmlLog.ToString());
            }

            if (_checkerConfiguration.IsJsonLogEnabled)
            {
                var jsonPath = directory + file + "_" + index + ".trace.json";
                Logger.WriteLine($"..... Writing {jsonPath}");

                // Remove the null objects from payload recursively for each log event
                for(int i=0; i<JsonLogger.Logs.Count; i++) {
                    JsonLogger.Logs[i].Details.Payload = RecursivelyReplaceNullWithString(JsonLogger.Logs[i].Details.Payload);
                }

                // Stream directly to the output file while serializing the JSON
                using var jsonStreamFile = File.Create(jsonPath);
                JsonSerializer.Serialize(jsonStreamFile, JsonLogger.Logs, jsonSerializerConfig);
            }

            if (!_checkerConfiguration.PerformFullExploration)
            {
                // Emits the reproducable trace, if it exists.
                if (!string.IsNullOrEmpty(ReproducableTrace))
                {
                    var reproTracePath = directory + file + "_" + index + ".schedule";

                    Logger.WriteLine($"..... Writing {reproTracePath}");
                    File.WriteAllText(reproTracePath, ReproducableTrace);
                }
            }

            Logger.WriteLine($"... Elapsed {Profiler.GetElapsedTime():0.##} sec and used {Profiler.GetMaxMemoryUsage():0.##} GB.");
        }

        /// <summary>
        /// Registers a callback to invoke at the end of each schedule. The callback takes as
        /// a parameter an integer representing the current schedule.
        /// </summary>
        public void RegisterPerIterationCallBack(Action<int> callback)
        {
            PerIterationCallbacks.Add(callback);
        }

        /// <summary>
        /// LogWriters on the given object.
        /// </summary>
        private void InitializeCustomLogging(ControlledRuntime runtime)
        {
            if (!string.IsNullOrEmpty(_checkerConfiguration.CustomStateMachineRuntimeLogType))
            {
                var log = Activate<IControlledRuntimeLog>(_checkerConfiguration.CustomStateMachineRuntimeLogType);
                if (log != null)
                {
                    runtime.RegisterLog(log);
                }
            }

            if (_checkerConfiguration.IsDgmlGraphEnabled || _checkerConfiguration.ReportActivityCoverage)
            {
                // Registers an activity coverage graph builder.
                runtime.RegisterLog(new ControlledRuntimeLogGraphBuilder(false)
                {
                    CollapseMachineInstances = _checkerConfiguration.ReportActivityCoverage
                });
            }

            if (_checkerConfiguration.ReportActivityCoverage)
            {
                // Need this additional logger to get the event coverage report correct
                runtime.RegisterLog(new ControlledRuntimeLogEventCoverage());
            }

            if (_checkerConfiguration.IsXmlLogEnabled)
            {
                XmlLog = new StringBuilder();
                runtime.RegisterLog(new PCheckerLogXmlFormatter(XmlWriter.Create(XmlLog,
                    new XmlWriterSettings() { Indent = true, IndentChars = "  ", OmitXmlDeclaration = true })));
            }
        }

        private T Activate<T>(string assemblyQualifiedName)
            where T : class
        {
            // Parses the result of Type.AssemblyQualifiedName.
            // e.g.: ConsoleApp1.Program, ConsoleApp1, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null
            try
            {
                var parts = assemblyQualifiedName.Split(',');
                if (parts.Length > 1)
                {
                    var typeName = parts[0];
                    var assemblyName = parts[1];
                    Assembly a = null;
                    if (File.Exists(assemblyName))
                    {
                        a = Assembly.LoadFrom(assemblyName);
                    }
                    else
                    {
                        a = Assembly.Load(assemblyName);
                    }

                    if (a != null)
                    {
                        var o = a.CreateInstance(typeName);
                        return o as T;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.WriteLine(ex.Message);
            }

            return null;
        }

        /// <summary>
        /// Loads and returns the specified assembly.
        /// </summary>
        private static Assembly LoadAssembly(string assemblyFile)
        {
            Assembly assembly = null;

            try
            {
                assembly = Assembly.LoadFrom(assemblyFile);
            }
            catch (FileNotFoundException ex)
            {
                Error.ReportAndExit(ex.Message);
            }

#if NETFRAMEWORK
            // Load config file and absorb its settings.
            try
            {
                var configFile = System.CheckerConfiguration.ConfigurationManager.OpenExeConfiguration(assemblyFile);
                var settings = configFile.AppSettings.Settings;
                foreach (var key in settings.AllKeys)
                {
                    if (System.CheckerConfiguration.ConfigurationManager.AppSettings.Get(key) is null)
                    {
                        System.CheckerConfiguration.ConfigurationManager.AppSettings.Set(key, settings[key].Value);
                    }
                    else
                    {
                        System.CheckerConfiguration.ConfigurationManager.AppSettings.Add(key, settings[key].Value);
                    }
                }
            }
            catch (System.CheckerConfiguration.ConfigurationErrorsException ex)
            {
                Error.Report(ex.Message);
            }
#endif

            return assembly;
        }

        /// <summary>
        /// Gathers the exploration strategy statistics from the specified runtimne.
        /// </summary>
        private void GatherTestingStatistics(ControlledRuntime runtime)
        {
            var report = runtime.Scheduler.GetReport();
            if (_checkerConfiguration.ReportActivityCoverage)
            {
                report.CoverageInfo.CoverageGraph = Graph;
            }

            var coverageInfo = runtime.GetCoverageInfo();
            report.CoverageInfo.Merge(coverageInfo);
            TestReport.Merge(report);

            // Also save the graph snapshot of the last schedule, if there is one.
            Graph = coverageInfo.CoverageGraph;
        }

        /// <summary>
        /// Constructs a reproducable trace.
        /// </summary>
        private void ConstructReproducableTrace(ControlledRuntime runtime)
        {
            var stringBuilder = new StringBuilder();

            if (Strategy.IsFair())
            {
                stringBuilder.Append("--fair-scheduling").Append(Environment.NewLine);
            }

            if (_checkerConfiguration.IsLivenessCheckingEnabled)
            {
                stringBuilder.Append("--liveness-temperature-threshold:" +
                                     _checkerConfiguration.LivenessTemperatureThreshold).
                    Append(Environment.NewLine);
            }

            if (!string.IsNullOrEmpty(_checkerConfiguration.TestCaseName))
            {
                stringBuilder.Append("--test-method:" +
                                     _checkerConfiguration.TestCaseName).
                    Append(Environment.NewLine);
            }

            for (var idx = 0; idx < runtime.Scheduler.ScheduleTrace.Count; idx++)
            {
                var step = runtime.Scheduler.ScheduleTrace[idx];
                if (step.Type == ScheduleStepType.SchedulingChoice)
                {
                    stringBuilder.Append($"({step.ScheduledOperationId})");
                }
                else if (step.BooleanChoice != null)
                {
                    stringBuilder.Append(step.BooleanChoice.Value);
                }
                else
                {
                    stringBuilder.Append(step.IntegerChoice.Value);
                }

                if (idx < runtime.Scheduler.ScheduleTrace.Count - 1)
                {
                    stringBuilder.Append(Environment.NewLine);
                }
            }

            ReproducableTrace = stringBuilder.ToString();
        }

        /// <summary>
        /// Returns the schedule to replay.
        /// </summary>
        private string[] GetScheduleForReplay(out bool isFair)
        {
            string[] scheduleDump;
            if (_checkerConfiguration.ScheduleTrace.Length > 0)
            {
                scheduleDump = _checkerConfiguration.ScheduleTrace.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            }
            else
            {
                scheduleDump = File.ReadAllLines(_checkerConfiguration.ScheduleFile);
            }

            isFair = false;
            foreach (var line in scheduleDump)
            {
                if (!line.StartsWith("--"))
                {
                    break;
                }

                if (line.Equals("--fair-scheduling"))
                {
                    isFair = true;
                }
                else if (line.StartsWith("--liveness-temperature-threshold:"))
                {
                    _checkerConfiguration.LivenessTemperatureThreshold =
                        int.Parse(line.Substring("--liveness-temperature-threshold:".Length));
                }
                else if (line.StartsWith("--test-method:"))
                {
                    _checkerConfiguration.TestCaseName =
                        line.Substring("--test-method:".Length);
                }
            }

            return scheduleDump;
        }

        /// <summary>
        /// Returns true if the engine should print the current schedule.
        /// </summary>
        private bool ShouldPrintIteration(int schedule)
        {
            if (schedule > PrintGuard * 10)
            {
                var count = schedule.ToString().Length - 1;
                var guard = "1" + (count > 0 ? string.Concat(Enumerable.Repeat("0", count)) : string.Empty);
                PrintGuard = int.Parse(guard);
            }

            return schedule % PrintGuard == 0;
        }

        /// <summary>
        /// Installs the specified <see cref="TextWriter"/>.
        /// </summary>
        public void SetLogger(TextWriter logger)
        {
            Logger.Dispose();

            if (logger is null)
            {
                Logger = TextWriter.Null;
            }
            else
            {
                Logger = logger;
            }

            ErrorReporter.Logger = logger;
        }
    }
}