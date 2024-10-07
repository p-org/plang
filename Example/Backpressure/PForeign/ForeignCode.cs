using System;
using System.Runtime;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using PChecker.PRuntime.Values;
using PChecker.PRuntime;
using PChecker.PRuntime.Exceptions;
using PChecker.StateMachines;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable 162, 219, 414
namespace PImplementation
{
  public static partial class GlobalFunctions
  {
    static System.Random random = new System.Random();
    public static PFloat Expovariate(PFloat lambda, StateMachine stateMachine)
    {
      return (new PFloat(Math.Log(1 - random.NextDouble()))/(-lambda));
    }
    public static PFloat Random(StateMachine stateMachine)
    {
      return (new PFloat(random.NextDouble()));
    }
  }
}
