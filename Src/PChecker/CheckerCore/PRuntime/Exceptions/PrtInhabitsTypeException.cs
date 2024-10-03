using System;

namespace PChecker.PRuntime.Exceptions
{
    public class PrtInhabitsTypeException : Exception
    {
        public PrtInhabitsTypeException()
        {
        }

        public PrtInhabitsTypeException(string message) : base(message)
        {
        }

        public PrtInhabitsTypeException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}