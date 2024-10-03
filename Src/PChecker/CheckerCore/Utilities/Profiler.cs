// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;

namespace PChecker.Utilities
{
    /// <summary>
    /// The Coyote profiler.
    /// </summary>
    public class Profiler
    {
        private Stopwatch StopWatch;
        private static double maxMemoryUsed = 0.0;

        /// <summary>
        /// Starts measuring execution time.
        /// </summary>
        public void StartMeasuringExecutionTime()
        {
            StopWatch = new Stopwatch();
            StopWatch.Start();
        }

        /// <summary>
        /// Stops measuring execution time.
        /// </summary>
        public void StopMeasuringExecutionTime()
        {
            if (StopWatch != null)
            {
                StopWatch.Stop();
            }
        }

        /// <summary>
        /// Returns profiling results.
        /// </summary>
        public double GetElapsedTime() =>
            StopWatch != null ? StopWatch.Elapsed.TotalSeconds : 0;

        /// <summary>
        /// Get memory usage in gigabytes.
        /// </summary>
        public double GetCurrentMemoryUsage()
        {
            double memUsed = GC.GetTotalMemory(false) / 1024.0 / 1024.0 / 1024.0;
            if (memUsed > maxMemoryUsed)
                maxMemoryUsed = memUsed;
            return memUsed;
        }

        /// <summary>
        /// Get max memory usage in gigabytes.
        /// </summary>
        public static double GetMaxMemoryUsage()
        {
            return maxMemoryUsed;
        }
    }
}