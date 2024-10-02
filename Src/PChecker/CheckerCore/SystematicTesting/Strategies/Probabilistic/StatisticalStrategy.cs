using System.Collections.Generic;
using System.Linq;
using PChecker.IO.Debugging;
using PChecker.Random;
using PChecker.SystematicTesting.Operations;

namespace PChecker.SystematicTesting.Strategies.Probabilistic
{
    /// <summary>
    /// A simple (but effective) randomized scheduling strategy.
    /// </summary>
    internal class StatisticalStrategy : ISchedulingStrategy
    {
        /// <summary>
        /// Random value generator.
        /// </summary>
        protected IRandomValueGenerator RandomValueGenerator;

        /// <summary>
        /// The maximum number of steps to schedule.
        /// </summary>
        protected int MaxScheduledSteps;

        /// <summary>
        /// The number of scheduled steps.
        /// </summary>
        protected int ScheduledSteps;

        /// <summary>
        /// Initializes a new instance of the <see cref="StatisticalStrategy"/> class.
        /// </summary>
        public StatisticalStrategy(int maxSteps, IRandomValueGenerator random)
        {
            RandomValueGenerator = random;
            MaxScheduledSteps = maxSteps;
            ScheduledSteps = 0;
        }
        
        /// <inheritdoc/>
        public virtual bool GetNextOperation(AsyncOperation current, IEnumerable<AsyncOperation> ops, out AsyncOperation next)
        {
            var enabledOperations = ops.Where(op => op.Status is AsyncOperationStatus.Enabled).ToList();
            if (enabledOperations.Count == 0)
            {
                next = null;
                return false;
            }

            var idx = RandomValueGenerator.Next(enabledOperations.Count);
            next = enabledOperations[idx];

            ScheduledSteps++;

            return true;
        }

        /// <inheritdoc/>
        public virtual bool GetNextBooleanChoice(AsyncOperation current, int maxValue, out bool next)
        {
            next = false;
            if (RandomValueGenerator.Next(maxValue) == 0)
            {
                next = true;
            }

            ScheduledSteps++;

            return true;
        }

        /// <inheritdoc/>
        public virtual bool GetNextIntegerChoice(AsyncOperation current, int maxValue, out int next)
        {
            next = RandomValueGenerator.Next(maxValue);
            ScheduledSteps++;
            return true;
        }
        
        /// <inheritdoc/>
        public bool GetSampleFromDistribution(string dist, out double sample)
        {
            if (dist is not null)
            {
                if (dist.Contains("DiscreteUniform"))
                {
                    var bounds = dist.Replace(" ", "").Replace("(", "").Replace(")", "").Substring(15).Split(",");
                    if (int.TryParse(bounds[0], out var lowerBound) && int.TryParse(bounds[1], out var upperBound))
                    {
                        sample = RandomValueGenerator.Next(lowerBound, upperBound);
                        return true;
                    }
                } else if (dist.Contains("ContinuousUniform"))
                {
                    var bounds = dist.Replace(" ", "").Replace("(", "").Replace(")", "").Substring(17).Split(",");
                    if (double.TryParse(bounds[0], out var lowerBound) && double.TryParse(bounds[1], out var upperBound))
                    {
                        sample = RandomValueGenerator.NextDouble() * (upperBound - lowerBound) + lowerBound;
                        return true;
                    }
                }
                else
                {
                    if (double.TryParse(dist, out sample))
                    {
                        return true;
                    }

                    Error.ReportAndExit("Given distribution " + dist + " is not supported.");
                }
            }

            sample = 0;
            return false;
        }

        /// <inheritdoc/>
        public virtual bool PrepareForNextIteration()
        {
            ScheduledSteps = 0;
            return true;
        }

        /// <inheritdoc/>
        public int GetScheduledSteps() => ScheduledSteps;

        /// <inheritdoc/>
        public bool HasReachedMaxSchedulingSteps()
        {
            if (MaxScheduledSteps == 0)
            {
                return false;
            }

            return ScheduledSteps >= MaxScheduledSteps;
        }

        /// <inheritdoc/>
        public bool IsFair() => true;

        /// <inheritdoc/>
        public virtual string GetDescription() => $"random[seed '{RandomValueGenerator.Seed}']";

        /// <inheritdoc/>
        public virtual void Reset()
        {
            ScheduledSteps = 0;
        }
    }
}
