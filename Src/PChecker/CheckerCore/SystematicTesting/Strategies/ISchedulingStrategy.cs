// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using PChecker.SystematicTesting.Operations;

namespace PChecker.SystematicTesting.Strategies
{
    /// <summary>
    /// Interface of an exploration strategy used during controlled testing.
    /// </summary>
    internal interface ISchedulingStrategy
    {
        /// <summary>
        /// Returns the next asynchronous operation to schedule.
        /// </summary>
        /// <param name="current">The currently scheduled operation.</param>
        /// <param name="ops">List of operations that can be scheduled.</param>
        /// <param name="next">The next operation to schedule.</param>
        /// <returns>True if there is a next choice, else false.</returns>
        bool GetNextOperation(AsyncOperation current, IEnumerable<AsyncOperation> ops, out AsyncOperation next);

        /// <summary>
        /// Returns the next boolean choice.
        /// </summary>
        /// <param name="current">The currently scheduled operation.</param>
        /// <param name="maxValue">The max value.</param>
        /// <param name="next">The next boolean choice.</param>
        /// <returns>True if there is a next choice, else false.</returns>
        bool GetNextBooleanChoice(AsyncOperation current, int maxValue, out bool next);

        /// <summary>
        /// Returns the next integer choice.
        /// </summary>
        /// <param name="current">The currently scheduled operation.</param>
        /// <param name="maxValue">The max value.</param>
        /// <param name="next">The next integer choice.</param>
        /// <returns>True if there is a next choice, else false.</returns>
        bool GetNextIntegerChoice(AsyncOperation current, int maxValue, out int next);

        /// <summary>
        /// Returns a sample from the given distribution.
        /// </summary>
        /// <param name="dist">The string value defining the distribution to sample from.</param>
        /// <param name="sample">The sampled value.</param>
        /// <returns>True if the sampling is successful, else false.</returns>
        bool GetSampleFromDistribution(string dist, out double sample)
        {
            sample = 0;
            return false;
        }

        /// <summary>
        /// Prepares for the next schedule. This is invoked
        /// at the end of a schedule. It must return false
        /// if the scheduling strategy should stop exploring.
        /// </summary>
        /// <returns>True to start the next schedule.</returns>
        bool PrepareForNextIteration();

        /// <summary>
        /// Returns the scheduled steps.
        /// </summary>
        int GetScheduledSteps();

        /// <summary>
        /// True if the scheduling strategy has reached the max
        /// scheduling steps for the given schedule.
        /// </summary>
        bool HasReachedMaxSchedulingSteps();

        /// <summary>
        /// Checks if this is a fair scheduling strategy.
        /// </summary>
        bool IsFair();

        /// <summary>
        /// Returns a textual description of the scheduling strategy.
        /// </summary>
        string GetDescription();

        /// <summary>
        /// Resets the scheduling strategy. This is typically invoked by
        /// parent strategies to reset child strategies.
        /// </summary>
        void Reset();
    }
}