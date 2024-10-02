// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace PChecker.Random
{
    /// <summary>
    /// Interface for random value generators.
    /// </summary>
    public interface IRandomValueGenerator
    {
        /// <summary>
        /// The seed currently used by the generator.
        /// </summary>
        uint Seed { get; set; }

        /// <summary>
        /// Returns a non-negative random number.
        /// </summary>
        int Next();

        /// <summary>
        /// Returns a non-negative random number less than maxValue.
        /// </summary>
        /// <param name="maxValue">Exclusive upper bound</param>
        int Next(int maxValue);

        /// <summary>
        /// Returns a random number more than or equal to minValue and less than maxValue.
        /// </summary>
        /// <param name="minValue">Inclusive lower bound</param>
        /// <param name="maxValue">Exclusive upper bound</param>
        int Next(int minValue, int maxValue);

        /// <summary>
        /// Returns a random floating-point number that is greater
        /// than or equal to 0.0, and less than 1.0.
        /// </summary>
        double NextDouble();
    }
}