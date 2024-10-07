using System;
using System.Runtime;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using PChecker.PRuntime.Values;
using PChecker.PRuntime;
using PChecker.PRuntime.Exceptions;
using System.Threading;
using System.Threading.Tasks;
using PChecker.StateMachines;

#pragma warning disable 162, 219, 414
namespace PImplementation
{
  public static partial class GlobalFunctions
  {
    static System.Random random = new System.Random();
    public static PFloat Random(StateMachine stateMachine)
    {
      return (new PFloat(random.NextDouble()));
    }
  }
}
