namespace PChecker.PRuntime.Values
{
    public interface IPrtMutableValue : IPrtValue
    {
        void Freeze();
    }
}