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

#pragma warning disable 162, 219, 414
namespace PImplementation
{
  public static partial class GlobalFunctions
  {
    public static PrtNamedTuple ChooseRandomTransaction(PrtInt uniqueId, PMachine pMachine)
    {
      return (new PrtNamedTuple(new string[] { "key", "val", "transId" }, (PrtString)pMachine.TryRandomInt(10).ToString(), (PrtInt)pMachine.TryRandomInt(10), (PrtInt) uniqueId));
    }
  }
}