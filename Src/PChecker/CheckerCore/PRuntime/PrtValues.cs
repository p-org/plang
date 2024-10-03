using PChecker.PRuntime.Values;

namespace PChecker.PRuntime
{
    public static class PrtValues
    {
        public static PrtBool Box(bool value)
        {
            return value;
        }

        public static PrtInt Box(long value)
        {
            return new PrtInt(value);
        }

        public static PrtInt Box(int value)
        {
            return new PrtInt(value);
        }

        public static PrtInt Box(short value)
        {
            return new PrtInt(value);
        }

        public static PrtInt Box(byte value)
        {
            return new PrtInt(value);
        }

        public static PrtFloat Box(double value)
        {
            return new PrtFloat(value);
        }

        public static PrtFloat Box(float value)
        {
            return new PrtFloat(value);
        }

        public static PrtBool SafeEquals(IPrtValue val1, IPrtValue val2)
        {
            return ReferenceEquals(val1, val2) || val1 != null && val1.Equals(val2);
        }

        public static IPrtValue PrtCastValue(IPrtValue value, PrtType type)
        {
            //todo: Needs to be fixed for better error message
            /*if (!PrtInhabitsType(value, type))
                throw new PrtInhabitsTypeException(
                    $"value {value.ToString()} is not a member of type {type.ToString()}");*/
            return value.Clone();
        }
    }
}