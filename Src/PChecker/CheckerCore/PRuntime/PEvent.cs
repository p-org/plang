using System;
using PChecker.Actors.Events;
using PChecker.PRuntime.Values;

namespace PChecker.PRuntime
{
    public class PEvent : Event, IPrtValue
    {
        public PEvent(int loc) : base()
        {
            Loc = loc;
        }

        public PEvent(IPrtValue payload, int loc) : base()
        {
            Payload = payload;
            Loc = loc;
        }

        public IPrtValue Payload { get; }

        public bool Equals(IPrtValue other)
        {
            return other != null && GetType().FullName.Equals(other.GetType().FullName);
        }

        public virtual IPrtValue Clone()
        {
            throw new NotImplementedException();
        }

        public object ToDict()
        {
            return this.GetType().Name;
        }

        public override bool Equals(object obj)
        {
            return obj is PEvent other && Equals(other);
        }

        public override int GetHashCode()
        {
            return GetType().FullName.GetHashCode();
        }

        public override string ToString()
        {
            return GetType().Name;
        }
    }

    public class PHalt : PEvent
    {
        public PHalt(IPrtValue payload, int loc) : base(payload, loc)
        {
        }
    }
}