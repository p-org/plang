using System;

namespace PChecker.PRuntime.Exceptions
{
    public enum NonStandardReturn
    {
        Raise,
        Goto,
        Pop
    }

    public class PNonStandardReturnException : Exception
    {
        public PNonStandardReturnException()
        {
        }

        public PNonStandardReturnException(string message) : base(message)
        {
        }

        public PNonStandardReturnException(string message, Exception innerException) : base(message, innerException)
        {
        }

        public NonStandardReturn ReturnKind { get; set; }
    }
}