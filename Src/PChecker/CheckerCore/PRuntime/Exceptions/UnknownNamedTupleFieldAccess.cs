using System;
using System.Collections.Generic;

namespace PChecker.PRuntime.Exceptions
{
    public class UnknownNamedTupleFieldAccess : Exception
    {
        public static UnknownNamedTupleFieldAccess FromFields(string expectedField, IEnumerable<string> actualFields)
        {
            var msg =
                "Field " + expectedField + " absent from NamedTuple with fields " + String.Join(",", actualFields);
            return new UnknownNamedTupleFieldAccess(msg);
        }

        private UnknownNamedTupleFieldAccess(string msg): base(msg)
        {

        }
    }
}