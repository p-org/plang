// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using PChecker.SystematicTesting;
using PChecker.Testing;
using PChecker.Utilities;
using Debug = PChecker.IO.Debugging.Debug;

namespace PChecker.Scheduling
{
    /// <summary>
    /// Testing Process that handles the scheduler
    /// </summary>
    public class TestingProcessScheduler
    {
        /// <summary>
        /// CheckerConfiguration.
        /// </summary>
        private readonly CheckerConfiguration _checkerConfiguration;

        /// <summary>
        /// Records if we want certain child test processes to terminate, this key here is the
        /// SmartSocketClient Name.
        /// </summary>
        private readonly HashSet<string> Terminating = new HashSet<string>();

        /// <summary>
        /// The test reports per process.
        /// </summary>
        private readonly ConcurrentDictionary<uint, TestReport> TestReports;

        /// <summary>
        /// Test Trace files.
        /// </summary>
        private readonly ConcurrentDictionary<uint, string> traceFiles;

        /// <summary>
        /// The global test report, which contains merged information
        /// from the test report of each testing process.
        /// </summary>
        private readonly TestReport GlobalTestReport;

        /// <summary>
        /// The testing profiler.
        /// </summary>
        private readonly Profiler Profiler;

        /// <summary>
        /// The scheduler lock.
        /// </summary>
        private readonly object SchedulerLock;


        /// <summary>
        /// Set if ctrl-c or ctrl-break occurred.
        /// </summary>
        public static bool IsProcessCanceled;

        /// <summary>
        /// Whether to write verbose output.
        /// </summary>
        private readonly bool IsVerbose;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestingProcessScheduler"/> class.
        /// </summary>
        private TestingProcessScheduler(CheckerConfiguration checkerConfiguration)
        {
            TestReports = new ConcurrentDictionary<uint, TestReport>();
            traceFiles = new ConcurrentDictionary<uint, string>();
            GlobalTestReport = new TestReport(checkerConfiguration);
            Profiler = new Profiler();
            SchedulerLock = new object();

            IsVerbose = checkerConfiguration.IsVerbose;

            checkerConfiguration.EnableColoredConsoleOutput = true;

            _checkerConfiguration = checkerConfiguration;
        }

        /// <summary>
        /// Creates a new testing process scheduler.
        /// </summary>
        public static TestingProcessScheduler Create(CheckerConfiguration checkerConfiguration)
        {
            return new TestingProcessScheduler(checkerConfiguration);
        }

        /// <summary>
        /// Runs the Coyote testing scheduler.
        /// </summary>
        public void Run()
        {
            Profiler.StartMeasuringExecutionTime();

            CreateAndRunInMemoryTestingProcess();

            Profiler.StopMeasuringExecutionTime();

            if (!IsProcessCanceled)
            {
                // Merges and emits the test report.
                EmitTestReport();
            }
        }

        /// <summary>
        /// Creates and runs an in-memory testing process.
        /// </summary>
        private void CreateAndRunInMemoryTestingProcess()
        {
            var testingProcess = TestingProcess.Create(_checkerConfiguration);

            // Runs the testing process.
            testingProcess.Run();

            // Get and merge the test report.
            var testReport = testingProcess.GetTestReport();
            if (testReport != null)
            {
                MergeTestReport(testReport, 0);
            }
        }

        /// <summary>
        /// Merges the test report from the specified process.
        /// </summary>
        private void MergeTestReport(TestReport testReport, uint processId)
        {
            if (TestReports.TryAdd(processId, testReport))
            {
                // Merges the test report into the global report.
                Debug.WriteLine($"... Merging task {processId} test report.");
                GlobalTestReport.Merge(testReport);
            }
            else
            {
                Debug.WriteLine($"... Unable to merge test report from task '{processId}'. " +
                                " Report is already merged.");
            }
        }

        /// <summary>
        /// Emits the test report.
        /// </summary>
        private void EmitTestReport()
        {

            if (TestReports.Count == 0)
            {
                Environment.ExitCode = (int)ExitCode.InternalError;
                return;
            }

            if (_checkerConfiguration.ReportActivityCoverage)
            {
                Console.WriteLine($"... Emitting coverage reports:");
                Reporter.EmitTestingCoverageReport(GlobalTestReport);
            }

            if (_checkerConfiguration.DebugActivityCoverage)
            {
                Console.WriteLine($"... Emitting debug coverage reports:");
                foreach (var report in TestReports)
                {
                    Reporter.EmitTestingCoverageReport(report.Value, report.Key, isDebug: true);
                }
            }

            Console.WriteLine(GlobalTestReport.GetText(_checkerConfiguration, "..."));

            var file = Path.GetFileNameWithoutExtension(GlobalTestReport.CheckerConfiguration.AssemblyToBeAnalyzed);
            var directory = GlobalTestReport.CheckerConfiguration.OutputDirectory;
            var pintPath = directory + file + "_pchecker_summary.txt";
            Console.WriteLine($"..... Writing {pintPath}");
            File.WriteAllText(pintPath, GlobalTestReport.GetSummaryText(Profiler));

            Console.WriteLine($"... Elapsed {Profiler.GetElapsedTime():0.##} sec and used {Profiler.GetMaxMemoryUsage():0.##} GB.");

            if (GlobalTestReport.InternalErrors.Count > 0)
            {
                Environment.ExitCode = (int)ExitCode.InternalError;
            }
            else if (GlobalTestReport.NumOfFoundBugs > 0)
            {
                Environment.ExitCode = (int)ExitCode.BugFound;
            }
            else
            {
                Environment.ExitCode = (int)ExitCode.Success;
            }
        }
    }
}