// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using PChecker.Specifications.Monitors;
using PChecker.SystematicTesting.Operations;

namespace PChecker.SystematicTesting.Strategies.Liveness
{
    /// <summary>
    /// Abstract strategy for detecting liveness property violations. It
    /// contains a nested <see cref="ISchedulingStrategy"/> that is used
    /// for scheduling decisions.
    /// </summary>
    internal abstract class LivenessCheckingStrategy : ISchedulingStrategy
    {
        /// <summary>
        /// The checkerConfiguration.
        /// </summary>
        protected CheckerConfiguration CheckerConfiguration;

        /// <summary>
        /// List of monitors in the program.
        /// </summary>
        protected List<Monitor> Monitors;

        /// <summary>
        /// Strategy used for scheduling decisions.
        /// </summary>
        protected ISchedulingStrategy SchedulingStrategy;

        /// <summary>
        /// Initializes a new instance of the <see cref="LivenessCheckingStrategy"/> class.
        /// </summary>
        internal LivenessCheckingStrategy(CheckerConfiguration checkerConfiguration, List<Monitor> monitors, ISchedulingStrategy strategy)
        {
            CheckerConfiguration = checkerConfiguration;
            Monitors = monitors;
            SchedulingStrategy = strategy;
        }

        /// <inheritdoc/>
        public abstract bool GetNextOperation(AsyncOperation current, IEnumerable<AsyncOperation> ops, out AsyncOperation next);

        /// <inheritdoc/>
        public abstract bool GetNextBooleanChoice(AsyncOperation current, int maxValue, out bool next);

        /// <inheritdoc/>
        public abstract bool GetNextIntegerChoice(AsyncOperation current, int maxValue, out int next);

        /// <inheritdoc/>
        public abstract bool GetSampleFromDistribution(string dist, out double sample);

        /// <inheritdoc/>
        public virtual bool PrepareForNextIteration()
        {
            return SchedulingStrategy.PrepareForNextIteration();
        }

        /// <inheritdoc/>
        public virtual int GetScheduledSteps()
        {
            return SchedulingStrategy.GetScheduledSteps();
        }

        /// <inheritdoc/>
        public virtual bool HasReachedMaxSchedulingSteps()
        {
            return SchedulingStrategy.HasReachedMaxSchedulingSteps();
        }

        /// <inheritdoc/>
        public virtual bool IsFair()
        {
            return SchedulingStrategy.IsFair();
        }

        /// <inheritdoc/>
        public virtual string GetDescription()
        {
            return SchedulingStrategy.GetDescription();
        }

        /// <inheritdoc/>
        public virtual void Reset()
        {
            SchedulingStrategy.Reset();
        }
    }
}