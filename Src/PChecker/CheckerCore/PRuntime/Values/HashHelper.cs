using System.Collections.Generic;
using System.Linq;

namespace PChecker.PRuntime.Values
{
    public class HashHelper
    {
        private const uint HashSeed = 0x802CBBDB;

        public static int ComputeHash<T>(IEnumerable<T> values)
        {
            unchecked
            {
                return (int)values.Aggregate(HashSeed,
                    (current, value) => current ^ (value == null ? 0 : (uint)value.GetHashCode()));
            }
        }
    }
}