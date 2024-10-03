using System;

namespace PChecker.PRuntime.Exceptions
{
    public class PInternalException : Exception
    {
        public PInternalException()
        {
        }

        public PInternalException(string message) : base(message)
        {
        }

        public PInternalException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}