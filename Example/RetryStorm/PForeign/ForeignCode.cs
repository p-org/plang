using System;
using System.Runtime;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Plang.CSharpRuntime;
using Plang.CSharpRuntime.Values;
using Plang.CSharpRuntime.Exceptions;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable 162, 219, 414
namespace PImplementation
{
  public static partial class GlobalFunctions
  {
    static System.Random random = new System.Random();
    public static PrtFloat Random(PMachine pMachine)
    {
      return (new PrtFloat(random.NextDouble()));
    }
  }
}
