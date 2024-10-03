using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PChecker.PRuntime.Exceptions;

namespace PChecker.PRuntime.Values
{
    public sealed class PrtSet : IPrtMutableValue, IReadOnlyList<IPrtValue>, ICollection<IPrtValue>
    {
        private readonly ISet<IPrtValue> set = new HashSet<IPrtValue>();

        private int hashCode;
        private bool isDirty;
        private bool isFrozen;

        public PrtSet()
        {
            hashCode = ComputeHashCode();
        }

        public PrtSet(ISet<IPrtValue> set)
        {
            this.set = set;
            hashCode = ComputeHashCode();
        }

        private bool IsDirty
        {
            get => isDirty;
            set
            {
                if (value && isFrozen) throw new PFrozenMutationException();

                isDirty = value;
            }
        }

        public override int GetHashCode()
        {
            if (!IsDirty)
            {
                return hashCode;
            }

            hashCode = ComputeHashCode();
            IsDirty = false;

            return hashCode;
        }

        public IEnumerator<IPrtValue> GetEnumerator()
        {
            return set.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Add(IPrtValue item)
        {
            MutabilityHelper.EnsureFrozen(item);
            IsDirty = true;
            return set.Add(item.Clone());
        }

        public void Clear()
        {
            set.Clear();
            IsDirty = true;
        }

        public bool Contains(IPrtValue item)
        {
            return set.Contains(item);
        }

        public bool Remove(IPrtValue item)
        {
            var removed = set.Remove(item);
            IsDirty = true;
            return removed;
        }

        public int Count => set.Count;
        public bool IsReadOnly => false;

        public bool Equals(IPrtValue other)
        {
            return other is PrtSet otherSet
                   && set.SetEquals(otherSet.set);
        }

        public IPrtValue Clone()
        {
            var clone = new PrtSet(new HashSet<IPrtValue>());
            foreach (var item in set) clone.Add(item.Clone());
            return clone;
        }

        public void Freeze()
        {
            foreach (var item in set) MutabilityHelper.EnsureFrozen(item);
            isFrozen = true;
        }

        private int ComputeHashCode()
        {
            return HashHelper.ComputeHash(set);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("(");
            var sep = "";
            foreach (var value in set)
            {
                sb.Append(sep);
                sb.Append("<");
                sb.Append(value);
                sb.Append(">");
                sep = ", ";
            }

            sb.Append(")");
            return sb.ToString();
        }

        public string ToEscapedString()
        {
            var sb = new StringBuilder();
            sb.Append("(");
            var sep = "";
            foreach (var value in set)
            {
                var v = value == null ? "null" : value.ToEscapedString();
                sb.Append(sep);
                sb.Append("<");
                sb.Append(v);
                sb.Append(">");
                sep = ", ";
            }

            sb.Append(")");
            return sb.ToString();
        }

        public object ToDict()
        {
            return set.Select(value => value == null ? null : value.ToDict()).ToList();
        }

        public IPrtValue this[int index]
        {
            get => set.ElementAt(index);
            set => throw new Exception("Setting set elements using indexing is not allowed!");
        }
        public void UnionWith(IEnumerable<IPrtValue> other)
        {
            set.UnionWith(other.Select(i => i.Clone()));
        }

        public void IntersectWith(IEnumerable<IPrtValue> other)
        {
            set.IntersectWith(other);
        }

        public void ExceptWith(IEnumerable<IPrtValue> other)
        {
            set.ExceptWith(other);
        }

        public void SymmetricExceptWith(IEnumerable<IPrtValue> other)
        {
            set.SymmetricExceptWith(other);
        }

        public bool IsSubsetOf(IEnumerable<IPrtValue> other)
        {
            return set.IsSubsetOf(other);
        }

        public bool IsSupersetOf(IEnumerable<IPrtValue> other)
        {
            return set.IsSupersetOf(other);
        }

        public bool IsProperSupersetOf(IEnumerable<IPrtValue> other)
        {
            return set.IsProperSupersetOf(other);
        }

        public bool IsProperSubsetOf(IEnumerable<IPrtValue> other)
        {
            return set.IsProperSubsetOf(other);
        }

        public bool Overlaps(IEnumerable<IPrtValue> other)
        {
            return set.Overlaps(other);
        }

        public bool SetEquals(IEnumerable<IPrtValue> other)
        {
            return set.SetEquals(other);
        }

        void ICollection<IPrtValue>.Add(IPrtValue item)
        {
            set.Add(item);
        }

        public void CopyTo(IPrtValue[] array, int arrayIndex)
        {
            set.CopyTo(array, arrayIndex);
        }
    }
}