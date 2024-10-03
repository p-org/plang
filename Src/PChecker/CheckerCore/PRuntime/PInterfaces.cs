using System.Collections.Generic;
using System.Linq;
using PChecker.PRuntime.Exceptions;
using PChecker.PRuntime.Values;

namespace PChecker.PRuntime
{
    public class PInterfaces
    {
        private static readonly Dictionary<string, List<string>> Interfaces = new Dictionary<string, List<string>>();

        public static void AddInterface(string interfaceName, params string[] permissions)
        {
            Interfaces.Add(interfaceName, permissions.ToList());
        }

        public static void Clear()
        {
            Interfaces.Clear();
        }

        public static List<string> GetPermissions(string interfaceName)
        {
            return Interfaces[interfaceName].ToList();
        }

        public static bool IsCoercionAllowed(PMachineValue val, string interfaceName)
        {
            if (GetPermissions(interfaceName).Any(ev => !val.Permissions.Contains(ev)))
            {
                throw new PIllegalCoercionException($"value cannot be coerced to interface {interfaceName}");
            }

            return true;
        }
    }
}