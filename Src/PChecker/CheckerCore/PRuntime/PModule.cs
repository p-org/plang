using System;
using System.Collections.Generic;
using PChecker.Actors;

namespace PChecker.PRuntime
{
    public class PModule
    {
        public static Dictionary<string, Type> interfaceDefinitionMap = new Dictionary<string, Type>();
        public static Dictionary<string, List<Type>> monitorMap = new Dictionary<string, List<Type>>();
        public static Dictionary<string, List<string>> monitorObserves = new Dictionary<string, List<string>>();

        public static IDictionary<string, Dictionary<string, string>> linkMap =
            new Dictionary<string, Dictionary<string, string>>();

        public static IActorRuntime runtime;
    }
}