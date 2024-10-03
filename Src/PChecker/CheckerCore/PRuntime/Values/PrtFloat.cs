using System;
using System.Runtime.CompilerServices;

namespace PChecker.PRuntime.Values
{
    [Serializable]
    public readonly struct PrtFloat : IPrtValue
    {
        private readonly double value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PrtFloat(double value)
        {
            this.value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IPrtValue Clone()
        {
            return this;
        }

        public override string ToString()
        {
            return value.ToString();
        }

        public object ToDict()
        {
            return value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(IPrtValue other)
        {
            return other is PrtFloat f && value == f.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object val)
        {
            return val is PrtFloat other && Equals(value, other.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator double(in PrtFloat val)
        {
            return val.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator PrtFloat(float val)
        {
            return new PrtFloat(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator PrtFloat(double val)
        {
            return new PrtFloat(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator PrtFloat(PrtInt val)
        {
            return new PrtFloat(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtFloat operator +(in PrtFloat prtFloat1, in PrtFloat prtFloat2)
        {
            return new PrtFloat(prtFloat1.value + prtFloat2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtFloat operator -(in PrtFloat prtFloat1, in PrtFloat prtFloat2)
        {
            return new PrtFloat(prtFloat1.value - prtFloat2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtFloat operator *(in PrtFloat prtFloat1, in PrtFloat prtFloat2)
        {
            return new PrtFloat(prtFloat1.value * prtFloat2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtFloat operator /(in PrtFloat prtFloat1, in PrtFloat prtFloat2)
        {
            return new PrtFloat(prtFloat1.value / prtFloat2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator <(in PrtFloat prtFloat1, in PrtFloat prtFloat2)
        {
            return prtFloat1.value < prtFloat2.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator >(in PrtFloat prtFloat1, in PrtFloat prtFloat2)
        {
            return prtFloat1.value > prtFloat2.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator <=(in PrtFloat prtFloat1, in PrtFloat prtFloat2)
        {
            return prtFloat1.value <= prtFloat2.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator >=(in PrtFloat prtFloat1, in PrtFloat prtFloat2)
        {
            return prtFloat1.value >= prtFloat2.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator ==(in PrtFloat prtFloat1, in PrtFloat prtFloat2)
        {
            return Equals(prtFloat1.value, prtFloat2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator !=(in PrtFloat prtFloat1, in PrtFloat prtFloat2)
        {
            return Equals(prtFloat1.value, prtFloat2.value) == false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtFloat operator +(in PrtFloat prtFloat)
        {
            return new PrtFloat(+prtFloat.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtFloat operator -(in PrtFloat prtFloat)
        {
            return new PrtFloat(-prtFloat.value);
        }
    }
}