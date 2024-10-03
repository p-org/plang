/*****************************************************
 * We will use this example of the PriorityQueue to explain the implementation details for Foreign Types and Functions Interfaces
 ******************************************************/

using System;
using System.Runtime;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using PChecker.PRuntime;
using PChecker.PRuntime.Values;
using PChecker.PRuntime.Exceptions;
using System.Threading;
using System.Threading.Tasks;

// Note: All the foreign code (foreign types or functions) for any P project is always implemnented under the `PImplementation` global namespace.
namespace PImplementation
{
  // Note: Global Foreign functions, i.e., foreign functions declared at the top-level and not inside a machine in P, are all implemented inside the
  // GlobalFunctions class. We can use `partial` keyword in C# to extend the classes declared in the Generated Code.
  // Also, all global foreign functions must be declared static!
  public static partial class GlobalFunctions
  {
      public static tPriorityQueue CreatePriorityQueue(PMachine machine)
      {
          // use these log statements if you want logs in the foreign code to show up in the error trace.
          machine.LogLine("Creating Priority Queue!");
          return new tPriorityQueue();
      }
      public static tPriorityQueue AddElement(tPriorityQueue queue, IPrtValue elem, PrtInt priority, PMachine machine)
      {
          queue.Add(new ElementWithPriority(elem, priority));
          machine.LogLine("Adding Element in the Priority Queue!");
          return queue;
      }

      public static PrtNamedTuple RemoveElement(tPriorityQueue queue, PMachine machine)
      {
          var element = queue.PriorityRemove();
          var retVal = new PrtNamedTuple(new string[] { "element", "queue" }, new IPrtValue[] { element, queue });
          return retVal;
      }

      public static PrtInt CountElement(tPriorityQueue queue, PMachine machine)
      {
          return queue.Size();
      }

      /*
       * Modeling Nondeterminism or Randomness in Foreign Functions
       */
      public static IPrtValue ChooseElement(tPriorityQueue queue, PMachine machine)
      {
          // one can write a nondeterministic foreign function using the machine.*Random functions.
          // all machine.TryRandom*() functions are controlled by the P checker during exploration.
          var index = machine.TryRandomInt(queue.Size());
          machine.LogLine("Choosing element at location: " + index);
          return queue.GetElementAt(index);
      }
  }
}