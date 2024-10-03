using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PChecker.PRuntime.Exceptions;

namespace PChecker.PRuntime.Values
{
    public sealed class PrtMap : IPrtMutableValue, IDictionary<IPrtValue, IPrtValue>
    {
        private readonly IDictionary<IPrtValue, IPrtValue> map = new Dictionary<IPrtValue, IPrtValue>();

        private int hashCode;
        private bool isDirty;
        private bool isFrozen;

        public PrtMap()
        {
            hashCode = ComputeHashCode();
        }

        public PrtMap(IDictionary<IPrtValue, IPrtValue> map)
        {
            this.map = map;
            hashCode = ComputeHashCode();
        }

        private bool IsDirty
        {
            get => isDirty;
            set
            {
                if (value && isFrozen)
                {
                    throw new PFrozenMutationException();
                }

                isDirty = value;
            }
        }

        public IEnumerator<KeyValuePair<IPrtValue, IPrtValue>> GetEnumerator()
        {
            return map.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(KeyValuePair<IPrtValue, IPrtValue> item)
        {
            MutabilityHelper.EnsureFrozen(item.Key);
            map.Add(item.Key?.Clone(), item.Value?.Clone());
            IsDirty = true;
        }

        public void Clear()
        {
            map.Clear();
            IsDirty = true;
        }

        public bool Contains(KeyValuePair<IPrtValue, IPrtValue> item)
        {
            return map.Contains(item);
        }

        public void CopyTo(KeyValuePair<IPrtValue, IPrtValue>[] array, int arrayIndex)
        {
            foreach (var kv in map)
            {
                array[arrayIndex++] = kv;
            }
        }

        public bool Remove(KeyValuePair<IPrtValue, IPrtValue> item)
        {
            var removed = map.Remove(item.Key);
            IsDirty = true;
            return removed;
        }

        public int Count => map.Count;
        public bool IsReadOnly => false;

        public void Add(IPrtValue key, IPrtValue value)
        {
            MutabilityHelper.EnsureFrozen(key);
            map.Add(key?.Clone(), value?.Clone());
            IsDirty = true;
        }

        public bool ContainsKey(IPrtValue key)
        {
            return map.ContainsKey(key);
        }

        public bool Remove(IPrtValue key)
        {
            var removed = map.Remove(key);
            IsDirty = true;
            return removed;
        }

        public bool TryGetValue(IPrtValue key, out IPrtValue value)
        {
            return map.TryGetValue(key, out value);
        }

        public IPrtValue this[IPrtValue key]
        {
            get => map[key];
            set
            {
                MutabilityHelper.EnsureFrozen(key);
                map[key] = value;
                IsDirty = true;
            }
        }

        public ICollection<IPrtValue> Keys => map.Keys;
        public ICollection<IPrtValue> Values => map.Values;

        public bool Equals(IPrtValue other)
        {
            return other is PrtMap otherMap
                   && !map.Keys.Except(otherMap.map.Keys).Any()
                   && !otherMap.map.Keys.Except(map.Keys).Any()
                   && map.All(kv => PrtValues.SafeEquals(otherMap.map[kv.Key], kv.Value));
        }

        public IPrtValue Clone()
        {
            return new PrtMap(map.ToDictionary(
                kv => kv.Key?.Clone(),
                kv => kv.Value?.Clone()));
        }

        public void Freeze()
        {
            foreach (var key in map.Keys)
            {
                MutabilityHelper.EnsureFrozen(key);
            }

            isFrozen = true;
        }

        public PrtSeq CloneKeys()
        {
            return new PrtSeq(map.Keys.Select(v => v.Clone()));
        }

        public PrtSeq CloneValues()
        {
            return new PrtSeq(map.Values.Select(v => v.Clone()));
        }

        public override int GetHashCode()
        {
            if (IsDirty)
            {
                hashCode = ComputeHashCode();
                IsDirty = false;
            }

            return hashCode;
        }

        private int ComputeHashCode()
        {
            return HashHelper.ComputeHash(map.Keys);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append("(");
            var sep = "";
            foreach (var value in map)
            {
                sb.Append(sep);
                sb.Append("<");
                sb.Append(value.Key);
                sb.Append("->");
                sb.Append(value.Value);
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
            foreach (var value in map)
            {
                string k = value.Key == null ? "null" : value.Key.ToEscapedString();
                string v = value.Value == null ? "null" : value.Value.ToEscapedString();
                sb.Append(sep);
                sb.Append("<");
                sb.Append(k);
                sb.Append("->");
                sb.Append(v);
                sb.Append(">");
                sep = ", ";
            }

            sb.Append(")");
            return sb.ToString();
        }

        public object ToDict()
        {
            var mapDict = new Dictionary<string, object>();

            foreach (var value in map)
            {
                var mapKey = value.Key.ToEscapedString();
                var mapValue = value.Value.ToDict();

                mapDict.Add(mapKey, mapValue);
            }

            return mapDict;
        }
    }
}