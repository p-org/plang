using System.Collections;
using PChecker.PRuntime.Values;

namespace PImplementation
{
    /*
     * Local foreign functions declared in a machine must be implemented inside the Class corresponding to that P machine.
     * In this case the `AddIntToQueue` function is a local function of the `Client` machine.
     */
    partial class Client
    {
        public static tPriorityQueue AddIntToQueue(tPriorityQueue queue, IPrtValue elem, PrtInt priority)
        {
            queue.Add(new ElementWithPriority(elem, priority));
            return queue;
        }
    }
}