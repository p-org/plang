// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using PChecker.Coverage;
using PChecker.Utilities;

namespace PChecker.SystematicTesting
{
    /// <summary>
    /// Class implementing the Coyote test report.
    /// </summary>
    [DataContract]
    public class TestReport
    {
        /// <summary>
        /// CheckerConfiguration of the program-under-test.
        /// </summary>
        [DataMember]
        public CheckerConfiguration CheckerConfiguration { get; private set; }

        /// <summary>
        /// Information regarding code coverage.
        /// </summary>
        [DataMember]
        public CoverageInfo CoverageInfo { get; private set; }

        /// <summary>
        /// Number of explored fair schedules.
        /// </summary>
        [DataMember]
        public int NumOfExploredFairSchedules { get; internal set; }

        /// <summary>
        /// Number of explored unfair schedules.
        /// </summary>
        [DataMember]
        public int NumOfExploredUnfairSchedules { get; internal set; }

        /// <summary>
        /// Number of found bugs.
        /// </summary>
        [DataMember]
        public int NumOfFoundBugs { get; internal set; }

        /// <summary>
        /// Set of unique bug reports.
        /// </summary>
        [DataMember]
        public HashSet<string> BugReports { get; internal set; }

        /// <summary>
        /// The min explored scheduling steps in average,
        /// in fair tests.
        /// </summary>
        [DataMember]
        public int MinExploredFairSteps { get; internal set; }

        /// <summary>
        /// The max explored scheduling steps in average,
        /// in fair tests.
        /// </summary>
        [DataMember]
        public int MaxExploredFairSteps { get; internal set; }

        /// <summary>
        /// The total explored scheduling steps (across
        /// all testing schedules), in fair tests.
        /// </summary>
        [DataMember]
        public int TotalExploredFairSteps { get; internal set; }

        /// <summary>
        /// Number of times the fair max steps bound was hit,
        /// in fair tests.
        /// </summary>
        [DataMember]
        public int MaxFairStepsHitInFairTests { get; internal set; }

        /// <summary>
        /// Number of times the unfair max steps bound was hit,
        /// in fair tests.
        /// </summary>
        [DataMember]
        public int MaxUnfairStepsHitInFairTests { get; internal set; }

        /// <summary>
        /// Number of times the unfair max steps bound was hit,
        /// in unfair tests.
        /// </summary>
        [DataMember]
        public int MaxUnfairStepsHitInUnfairTests { get; internal set; }

        /// <summary>
        /// Set of internal errors. If no internal errors
        /// occurred, then this set is empty.
        /// </summary>
        [DataMember]
        public HashSet<string> InternalErrors { get; internal set; }


        /// <summary>
        /// Set of hashes of timelines discovered by the scheduler.
        /// </summary>
        [DataMember]
        public Dictionary<int, int> ExploredTimelines = new();

        /// <summary>
        /// Number of schedulings that satisfies the pattern.
        /// </summary>
        [DataMember]
        public Dictionary<int, int> ValidScheduling = new();

        /// <summary>
        /// Lock for the test report.
        /// </summary>
        private readonly object Lock;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestReport"/> class.
        /// </summary>
        public TestReport(CheckerConfiguration checkerConfiguration)
        {
            CheckerConfiguration = checkerConfiguration;

            CoverageInfo = new CoverageInfo();

            NumOfExploredFairSchedules = 0;
            NumOfExploredUnfairSchedules = 0;
            NumOfFoundBugs = 0;
            BugReports = new HashSet<string>();

            MinExploredFairSteps = -1;
            MaxExploredFairSteps = -1;
            TotalExploredFairSteps = 0;
            MaxFairStepsHitInFairTests = 0;
            MaxUnfairStepsHitInFairTests = 0;
            MaxUnfairStepsHitInUnfairTests = 0;

            InternalErrors = new HashSet<string>();

            Lock = new object();
        }

        /// <summary>
        /// Merges the information from the specified test report.
        /// </summary>
        /// <returns>True if merged successfully.</returns>
        public bool Merge(TestReport testReport)
        {
            if (!CheckerConfiguration.AssemblyToBeAnalyzed.Equals(testReport.CheckerConfiguration.AssemblyToBeAnalyzed))
            {
                // Only merge test reports that have the same program name.
                return false;
            }

            lock (Lock)
            {
                CoverageInfo.Merge(testReport.CoverageInfo);

                NumOfFoundBugs += testReport.NumOfFoundBugs;

                BugReports.UnionWith(testReport.BugReports);

                NumOfExploredFairSchedules += testReport.NumOfExploredFairSchedules;
                NumOfExploredUnfairSchedules += testReport.NumOfExploredUnfairSchedules;

                if (testReport.MinExploredFairSteps >= 0 &&
                    (MinExploredFairSteps < 0 ||
                     MinExploredFairSteps > testReport.MinExploredFairSteps))
                {
                    MinExploredFairSteps = testReport.MinExploredFairSteps;
                }

                if (MaxExploredFairSteps < testReport.MaxExploredFairSteps)
                {
                    MaxExploredFairSteps = testReport.MaxExploredFairSteps;
                }

                TotalExploredFairSteps += testReport.TotalExploredFairSteps;

                MaxFairStepsHitInFairTests += testReport.MaxFairStepsHitInFairTests;
                MaxUnfairStepsHitInFairTests += testReport.MaxUnfairStepsHitInFairTests;
                MaxUnfairStepsHitInUnfairTests += testReport.MaxUnfairStepsHitInUnfairTests;

                InternalErrors.UnionWith(testReport.InternalErrors);
            }

            return true;
        }

        /// <summary>
        /// Returns a simple string testing report with only the keys and values.
        /// </summary>
        public string GetSummaryText(Profiler Profiler) {
            var report = new StringBuilder();

            report.AppendFormat("bugs:{0}", NumOfFoundBugs);
            report.AppendLine();

            var totalExploredSchedules = NumOfExploredFairSchedules +
                                         NumOfExploredUnfairSchedules;
            report.AppendFormat("schedules:{0}", totalExploredSchedules);
            report.AppendLine();

            report.AppendFormat("max_depth:{0}", MaxExploredFairSteps < 0 ? 0 : MaxExploredFairSteps);
            report.AppendLine();

            report.AppendFormat($"time_seconds:{Profiler.GetElapsedTime():0.##}");
            report.AppendLine();

            report.AppendFormat($"memory_max_mb:{Profiler.GetMaxMemoryUsage():0.##}");

            return report.ToString();
        }

        /// <summary>
        /// Returns the testing report as a string, given a checkerConfiguration and an optional prefix.
        /// </summary>
        public string GetText(CheckerConfiguration checkerConfiguration, string prefix = "")
        {
            var report = new StringBuilder();

            report.AppendFormat("{0} Checking statistics:", prefix);

            report.AppendLine();
            report.AppendFormat(
                "{0} Found {1} bug{2}.",
                prefix.Equals("...") ? "....." : prefix,
                NumOfFoundBugs,
                NumOfFoundBugs == 1 ? string.Empty : "s");

            report.AppendLine();
            report.AppendFormat("{0} Scheduling statistics:", prefix);

            var totalExploredSchedules = NumOfExploredFairSchedules +
                                         NumOfExploredUnfairSchedules;

            report.AppendLine();
            report.AppendFormat(
                "{0} Explored {1} schedule{2}",
                prefix.Equals("...") ? "....." : prefix,
                totalExploredSchedules,
                totalExploredSchedules == 1 ? string.Empty : "s");

            if (totalExploredSchedules > 0 &&
                NumOfFoundBugs > 0)
            {
                report.AppendLine();
                report.AppendFormat(
                    "{0} Found {1:F2}% buggy schedules.",
                    prefix.Equals("...") ? "....." : prefix,
                    NumOfFoundBugs * 100.0 / totalExploredSchedules);
            }

            if (NumOfExploredFairSchedules > 0)
            {
                var averageExploredFairSteps = TotalExploredFairSteps /
                                               NumOfExploredFairSchedules;

                report.AppendLine();
                report.AppendFormat(
                    "{0} Number of scheduling points in terminating schedules: {1} (min), {2} (avg), {3} (max).",
                    prefix.Equals("...") ? "....." : prefix,
                    MinExploredFairSteps < 0 ? 0 : MinExploredFairSteps,
                    averageExploredFairSteps,
                    MaxExploredFairSteps < 0 ? 0 : MaxExploredFairSteps);

                if (checkerConfiguration.MaxUnfairSchedulingSteps > 0 &&
                    MaxUnfairStepsHitInFairTests > 0)
                {
                    report.AppendLine();
                    report.AppendFormat(
                        "{0} Exceeded the max-steps bound of '{1}' in {2:F2}% of the fair schedules.",
                        prefix.Equals("...") ? "....." : prefix,
                        checkerConfiguration.MaxUnfairSchedulingSteps,
                        (double)MaxUnfairStepsHitInFairTests / NumOfExploredFairSchedules * 100);
                }

                if (checkerConfiguration.UserExplicitlySetMaxFairSchedulingSteps &&
                    checkerConfiguration.MaxFairSchedulingSteps > 0 &&
                    MaxFairStepsHitInFairTests > 0)
                {
                    report.AppendLine();
                    report.AppendFormat(
                        "{0} Hit the max-steps bound of '{1}' in {2:F2}% of schedules.",
                        prefix.Equals("...") ? "....." : prefix,
                        checkerConfiguration.MaxFairSchedulingSteps,
                        (double)MaxFairStepsHitInFairTests / NumOfExploredFairSchedules * 100);
                }
            }

            if (NumOfExploredUnfairSchedules > 0)
            {
                if (checkerConfiguration.MaxUnfairSchedulingSteps > 0 &&
                    MaxUnfairStepsHitInUnfairTests > 0)
                {
                    report.AppendLine();
                    report.AppendFormat(
                        "{0} Hit the max-steps bound of '{1}' in {2:F2}% of the unfair schedules.",
                        prefix.Equals("...") ? "....." : prefix,
                        checkerConfiguration.MaxUnfairSchedulingSteps,
                        (double)MaxUnfairStepsHitInUnfairTests / NumOfExploredUnfairSchedules * 100);
                }
            }

            return report.ToString();
        }

        /// <summary>
        /// Clones the test report.
        /// </summary>
        public TestReport Clone()
        {
            var serializerSettings = new DataContractSerializerSettings();
            serializerSettings.PreserveObjectReferences = true;
            var serializer = new DataContractSerializer(typeof(TestReport), serializerSettings);
            using (var ms = new MemoryStream())
            {
                lock (Lock)
                {
                    serializer.WriteObject(ms, this);
                    ms.Position = 0;
                    return (TestReport)serializer.ReadObject(ms);
                }
            }
        }
    }
}