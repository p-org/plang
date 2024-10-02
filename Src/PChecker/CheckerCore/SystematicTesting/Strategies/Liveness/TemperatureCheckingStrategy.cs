// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using PChecker.Specifications.Monitors;
using PChecker.SystematicTesting.Operations;

namespace PChecker.SystematicTesting.Strategies.Liveness
{
    /// <summary>
    /// Strategy for detecting liveness property violations using the "temperature"
    /// method. It contains a nested <see cref="ISchedulingStrategy"/> that is used
    /// for scheduling decisions. Note that liveness property violations are checked
    /// only if the nested strategy is fair.
    /// </summary>
    internal sealed class TemperatureCheckingStrategy : LivenessCheckingStrategy
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TemperatureCheckingStrategy"/> class.
        /// </summary>
        internal TemperatureCheckingStrategy(CheckerConfiguration checkerConfiguration, List<Monitor> monitors, ISchedulingStrategy strategy)
            : base(checkerConfiguration, monitors, strategy)
        {
        }

        /// <inheritdoc/>
        public override bool GetNextOperation(AsyncOperation current, IEnumerable<AsyncOperation> ops, out AsyncOperation next)
        {
            CheckLivenessTemperature();
            return SchedulingStrategy.GetNextOperation(current, ops, out next);
        }

        /// <inheritdoc/>
        public override bool GetNextBooleanChoice(AsyncOperation current, int maxValue, out bool next)
        {
            CheckLivenessTemperature();
            return SchedulingStrategy.GetNextBooleanChoice(current, maxValue, out next);
        }

        /// <inheritdoc/>
        public override bool GetNextIntegerChoice(AsyncOperation current, int maxValue, out int next)
        {
            CheckLivenessTemperature();
            return SchedulingStrategy.GetNextIntegerChoice(current, maxValue, out next);
        }

        /// <inheritdoc/>
        public override bool GetSampleFromDistribution(string dist, out double sample)
        {
            CheckLivenessTemperature();
            return SchedulingStrategy.GetSampleFromDistribution(dist, out sample);
        }

        /// <summary>
        /// Checks the liveness temperature of each monitor, and
        /// reports an error if one of the liveness monitors has
        /// passed the temperature threshold.
        /// </summary>
        private void CheckLivenessTemperature()
        {
            if (IsFair())
            {
                foreach (var monitor in Monitors)
                {
                    monitor.CheckLivenessTemperature();
                }
            }
        }
    }
}