using System;
using System.Runtime.CompilerServices;

namespace PChecker.PRuntime.Values
{
    [Serializable]
    public readonly struct PrtString : IPrtValue
    {
        public bool Equals(PrtString other)
        {
            return string.Equals(value, other.value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            return obj is PrtString other && Equals(other);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public IPrtValue Clone()
        {
            return this;
        }

        private readonly string value;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator PrtString(string val)
        {
            return new PrtString(val);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static implicit operator string(PrtString val)
        {
            return val.value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private PrtString(string value)
        {
            this.value = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtString operator +(in PrtString prtString1, in PrtString prtString2)
        {
            return new PrtString(prtString1.value + prtString2.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in PrtString pValue1, in PrtString pValue2)
        {
            return Equals(pValue1, pValue2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in PrtString pValue1, in PrtString pValue2)
        {
            return !Equals(pValue1, pValue2);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in PrtString pValue1, in IPrtValue pValue2)
        {
            return pValue2 is PrtString prtString && string.Equals(pValue1.value, prtString.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in PrtString pValue1, in IPrtValue pValue2)
        {
            return pValue2 is PrtString prtString && !string.Equals(pValue1.value, prtString.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(in IPrtValue pValue1, in PrtString pValue2)
        {
            return pValue1 is PrtString prtString && string.Equals(pValue2.value, prtString.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(in IPrtValue pValue1, in PrtString pValue2)
        {
            return pValue1 is PrtString prtString && !string.Equals(pValue2.value, prtString.value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator <(in PrtString prtString1, in PrtString prtString2)
        {
            return string.Compare(prtString1.value, prtString2.value) == -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator <(in IPrtValue pValue1, in PrtString pValue2)
        {
            return pValue1 is PrtString prtString && string.Compare(prtString.value, pValue2.value) == -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator <(in PrtString pValue1, in IPrtValue pValue2)
        {
            return pValue2 is PrtString prtString && string.Compare(prtString.value, pValue1.value) == -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator >(in PrtString prtString1, in PrtString prtString2)
        {
            return string.Compare(prtString1.value, prtString2.value) == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator >(in IPrtValue pValue1, in PrtString pValue2)
        {
            return pValue1 is PrtString prtString && string.Compare(prtString.value, pValue2.value) == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator >(in PrtString pValue1, in IPrtValue pValue2)
        {
            return pValue2 is PrtString prtString && string.Compare(prtString.value, pValue1.value) == 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator <=(in PrtString prtString1, in PrtString prtString2)
        {
            return string.Compare(prtString1.value, prtString2.value) != 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator <=(in IPrtValue pValue1, in PrtString pValue2)
        {
            return pValue1 is PrtString prtString && string.Compare(prtString.value, pValue2.value) != 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator <=(in PrtString pValue1, in IPrtValue pValue2)
        {
            return pValue2 is PrtString prtString && string.Compare(prtString.value, pValue1.value) != 1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator >=(in PrtString prtString1, in PrtString prtString2)
        {
            return string.Compare(prtString1.value, prtString2.value) != -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator >=(in IPrtValue pValue1, in PrtString pValue2)
        {
            return pValue1 is PrtString prtString && string.Compare(prtString.value, pValue2.value) != -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static PrtBool operator >=(in PrtString pValue1, in IPrtValue pValue2)
        {
            return pValue2 is PrtString prtString && string.Compare(prtString.value, pValue1.value) != -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(IPrtValue obj)
        {
            return obj is PrtString other && string.Equals(value, other.value);
        }

        public override int GetHashCode()
        {
            return value.GetHashCode();
        }

        public override string ToString()
        {
            return value;
        }

        /// <summary>
        /// Like ToString, but emits a representation of a string literal, surrounded by double-quotes,
        /// and where all interior double-quotes are escaped.
        /// </summary>
        /// <returns></returns>
        public string ToEscapedString()
        {
            var v = value ?? "";
            return $"\"{v.Replace("\"", "\\\"")}\"";
        }


        public object ToDict()
        {
            return ToString();
        }
    }
}