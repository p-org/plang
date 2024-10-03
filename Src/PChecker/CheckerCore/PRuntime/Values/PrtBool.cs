using System;
using System.Runtime.CompilerServices;

namespace PChecker.PRuntime.Values
{
    [Serializable]
    public readonly struct PrtBool : IPrtValue
    {
        public bool Equals(PrtBool other)
        {
            return value == other.value;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            return obj is PrtBool other && Equals(other);
        }

        public override string ToString()
        {
            return value.ToString();
        }

        public object ToDict()
        {
            return value;
        }

        private readonly bool value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PrtBool(bool value)
        {
            this.value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IPrtValue Clone()
        {
            return this;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator bool(in PrtBool val)
        {
            return val.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator PrtBool(bool val)
        {
            return new PrtBool(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator true(in PrtBool pValue)
        {
            return pValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator false(in PrtBool pValue)
        {
            return !pValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator !(in PrtBool pValue)
        {
            return new PrtBool(!pValue.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator &(in PrtBool pValue1, in PrtBool pValue2)
        {
            return new PrtBool(pValue1.value && pValue2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator |(in PrtBool pValue1, in PrtBool pValue2)
        {
            return new PrtBool(pValue1.value || pValue2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in PrtBool pValue1, in PrtBool pValue2)
        {
            return Equals(pValue1, pValue2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in PrtBool pValue1, in PrtBool pValue2)
        {
            return !Equals(pValue1, pValue2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in PrtBool pValue1, in IPrtValue pValue2)
        {
            return pValue2 is PrtBool prtBool && pValue1.value == prtBool.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in PrtBool pValue1, in IPrtValue pValue2)
        {
            return pValue2 is PrtBool prtBool && pValue1.value != prtBool.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in IPrtValue pValue1, in PrtBool pValue2)
        {
            return pValue1 is PrtBool prtBool && pValue2.value == prtBool.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in IPrtValue pValue1, in PrtBool pValue2)
        {
            return pValue1 is PrtBool prtBool && pValue2.value != prtBool.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(IPrtValue obj)
        {
            return obj is PrtBool other && value == other.value;
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }
    }
}