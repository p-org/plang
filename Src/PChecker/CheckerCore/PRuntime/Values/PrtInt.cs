using System;
using System.Runtime.CompilerServices;

namespace PChecker.PRuntime.Values
{
    [Serializable]
    public readonly struct PrtInt : IPrtValue
    {
        private readonly long value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PrtInt(long value)
        {
            this.value = value;
        }

        public bool Equals(IPrtValue other)
        {
            return other is PrtInt i && value == i.value;
        }

        public IPrtValue Clone()
        {
            return this;
        }

        public override bool Equals(object val)
        {
            return val is PrtInt other && Equals(value, other.value);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
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
        public static implicit operator PrtInt(byte val)
        {
            return new PrtInt(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator PrtInt(short val)
        {
            return new PrtInt(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator PrtInt(int val)
        {
            return new PrtInt(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator PrtInt(long val)
        {
            return new PrtInt(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator PrtInt(in PrtFloat val)
        {
            return new PrtInt((long)val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator int(in PrtInt val)
        {
            return (int)val.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator long(in PrtInt val)
        {
            return val.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtInt operator +(in PrtInt prtInt1, in PrtInt prtInt2)
        {
            return new PrtInt(prtInt1.value + prtInt2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtInt operator -(in PrtInt prtInt1, in PrtInt prtInt2)
        {
            return new PrtInt(prtInt1.value - prtInt2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtInt operator *(in PrtInt prtInt1, in PrtInt prtInt2)
        {
            return new PrtInt(prtInt1.value * prtInt2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtInt operator /(in PrtInt prtInt1, in PrtInt prtInt2)
        {
            return new PrtInt(prtInt1.value / prtInt2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator <(in PrtInt prtInt1, in PrtInt prtInt2)
        {
            return prtInt1.value < prtInt2.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator >(in PrtInt prtInt1, in PrtInt prtInt2)
        {
            return prtInt1.value > prtInt2.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator <=(in PrtInt prtInt1, in PrtInt prtInt2)
        {
            return prtInt1.value <= prtInt2.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator >=(in PrtInt prtInt1, in PrtInt prtInt2)
        {
            return prtInt1.value >= prtInt2.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator ==(in PrtInt prtInt1, in PrtInt prtInt2)
        {
            return Equals(prtInt1.value, prtInt2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator ==(in IPrtValue prtInt1, in PrtInt prtInt2)
        {
            return prtInt1 is PrtInt int1 && Equals(int1.value, prtInt2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator !=(in IPrtValue prtInt1, in PrtInt prtInt2)
        {
            return prtInt1 is PrtInt int1 && !Equals(int1.value, prtInt2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator ==(in PrtInt prtInt1, in IPrtValue prtInt2)
        {
            return prtInt2 is PrtInt int2 && Equals(prtInt1.value, int2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator !=(in PrtInt prtInt1, in IPrtValue prtInt2)
        {
            return prtInt2 is PrtInt int2 && !Equals(prtInt1.value, int2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator !=(in PrtInt prtInt1, in PrtInt prtInt2)
        {
            return Equals(prtInt1.value, prtInt2.value) == false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtInt operator +(in PrtInt prtInt)
        {
            return new PrtInt(+prtInt.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtInt operator -(in PrtInt prtInt)
        {
            return new PrtInt(-prtInt.value);
        }
    }
}