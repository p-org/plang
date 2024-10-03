using System;

namespace PChecker.PRuntime.Exceptions
{
    public class PFrozenMutationException : Exception
    {
        public PFrozenMutationException()
        {
        }

        public PFrozenMutationException(string message) : base(message)
        {
        }
    }
}