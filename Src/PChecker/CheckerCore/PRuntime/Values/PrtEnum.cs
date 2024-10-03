using System.Collections.Generic;
using System.Linq;

namespace PChecker.PRuntime.Values
{
    public class PrtEnum
    {
        private static readonly Dictionary<string, PrtInt> enumElements = new Dictionary<string, PrtInt>();

        public static PrtInt Get(string name)
        {
            return enumElements[name];
        }

        public static void AddEnumElements(string[] names, int[] values)
        {
            for (var i = 0; i < names.Length; i++)
            {
                enumElements.Add(names[i], values[i]);
            }
        }

        public static void Clear()
        {
            enumElements.Clear();
        }

        public object ToDict()
        {
            return enumElements.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToDict());
        }
    }
}