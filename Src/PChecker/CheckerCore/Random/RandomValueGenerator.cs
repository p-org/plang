// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace PChecker.Random
{
    /// <summary>
    /// Basic random value generator that uses the <see cref="System.Random"/> generator.
    /// </summary>
    internal class RandomValueGenerator : IRandomValueGenerator
    {
        /// <summary>
        /// Device for generating random numbers.
        /// </summary>
        private System.Random Random;

        /// <summary>
        /// The seed currently used by the generator.
        /// </summary>
        private uint RandomSeed;

        /// <summary>
        /// The seed currently used by the generator.
        /// </summary>
        public uint Seed
        {
            get => RandomSeed;

            set
            {
                RandomSeed = value;
                Random = new System.Random((int)RandomSeed);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RandomValueGenerator"/> class.
        /// </summary>
        internal RandomValueGenerator(CheckerConfiguration checkerConfiguration)
        {
            RandomSeed = checkerConfiguration.RandomGeneratorSeed ?? (uint)Guid.NewGuid().GetHashCode();
            Random = new System.Random((int)RandomSeed);
        }

        /// <summary>
        /// Returns a non-negative random number.
        /// </summary>
        public int Next() => Random.Next();

        /// <summary>
        /// Returns a non-negative random number less than the specified max value.
        /// </summary>
        /// <param name="maxValue">Exclusive upper bound.</param>
        public int Next(int maxValue) => Random.Next(maxValue);

        /// <summary>
        /// Returns a random number more than or equal to minValue and less than maxValue.
        /// </summary>
        /// <param name="minValue">Inclusive lower bound.</param>
        /// <param name="maxValue">Exclusive upper bound.</param>
        public int Next(int minValue, int maxValue) => Random.Next(minValue, maxValue);

        /// <summary>
        /// Returns a random floating-point number that is greater
        /// than or equal to 0.0, and less than 1.0.
        /// </summary>
        public double NextDouble() => Random.NextDouble();
    }
}