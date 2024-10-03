namespace PChecker.PRuntime.Values
{
    public static class MutabilityHelper
    {
        public static void EnsureFrozen<T>(T value)
            where T : IPrtValue
        {
            if (value is IPrtMutableValue mutable)
            {
                mutable.Freeze();
            }
        }
    }
}