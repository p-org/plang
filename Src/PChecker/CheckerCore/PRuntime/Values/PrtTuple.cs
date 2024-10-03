using System;
using System.Collections.Generic;
using System.Linq;
using PChecker.PRuntime.Exceptions;

namespace PChecker.PRuntime.Values
{
    [Serializable]
    public class PrtTuple : IPrtValue
    {
        public readonly List<IPrtValue> fieldValues;

        public PrtTuple()
        {
            fieldValues = new List<IPrtValue>();
        }

        public PrtTuple(params IPrtValue[] elems)
        {
            fieldValues = new List<IPrtValue>();
            if (elems == null || elems.Length == 1)
            {
                fieldValues.Add(elems?.First());
            }
            else
            {
                foreach (var elem in elems)
                {
                    fieldValues.Add(elem?.Clone());
                }
            }
        }

        public IPrtValue this[int key]
        {
            get => fieldValues[key];
            set => fieldValues[key] = value;
        }

        public IPrtValue Clone()
        {
            var clone = new PrtTuple();
            foreach (var val in fieldValues)
            {
                clone.fieldValues.Add(val?.Clone());
            }

            return clone;
        }

        public bool Equals(IPrtValue val)
        {
            if (val is PrtNamedTuple)
            {
                return false;
            }

            var tupValue = val as PrtTuple;
            if (tupValue == null)
            {
                return false;
            }

            if (tupValue.fieldValues.Count != fieldValues.Count)
            {
                return false;
            }

            for (var i = 0; i < fieldValues.Count; i++)
            {
                if (!PrtValues.SafeEquals(fieldValues[i], tupValue.fieldValues[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public void Update(int index, IPrtValue val)
        {
            fieldValues[index] = val;
        }

        public override int GetHashCode()
        {
            var hashCode = HashHelper.ComputeHash(fieldValues);
            return hashCode;
        }

        public override string ToString()
        {
            var retStr = "<";
            foreach (var field in fieldValues)
            {
                retStr = retStr + field + ",";
            }

            retStr += ">";
            return retStr;
        }

        public object ToDict()
        {
            var tupleDict = new Dictionary<int, object>();

            for (var i = 0; i < fieldValues.Count; i++)
            {
                var fieldValue = fieldValues[i];
                tupleDict.Add(i, fieldValue);
            }

            return tupleDict;
        }
    }

    [Serializable]
    public class PrtNamedTuple : IPrtValue
    {
        public readonly List<IPrtValue> fieldValues;
        public List<string> fieldNames;

        public PrtNamedTuple()
        {
            fieldNames = new List<string>();
            fieldValues = new List<IPrtValue>();
        }

        public PrtNamedTuple(string[] _fieldNames, params IPrtValue[] _fieldValues)
        {
            fieldNames = _fieldNames.ToList();
            if (_fieldValues == null || _fieldValues.Length == 1)
            {
                fieldValues = new List<IPrtValue>();
                fieldValues.Add(_fieldValues?.First());

            }
            else
            {
                fieldValues = _fieldValues.ToList();
            }
        }

        public IPrtValue this[string name]
        {
            get
            {
                var idx = fieldNames.IndexOf(name);
                if (idx == -1)
                {
                    throw UnknownNamedTupleFieldAccess.FromFields(name, fieldNames);
                }
                return fieldValues[fieldNames.IndexOf(name)];
            }
            set
            {
                var idx = fieldNames.IndexOf(name);
                if (idx == -1)
                {
                    throw UnknownNamedTupleFieldAccess.FromFields(name, fieldNames);
                }
                fieldValues[fieldNames.IndexOf(name)] = value;
            }
        }

        public IPrtValue Clone()
        {
            var clone = new PrtNamedTuple();
            foreach (var name in fieldNames)
            {
                clone.fieldNames.Add(name);
            }

            foreach (var val in fieldValues)
            {
                clone.fieldValues.Add(val?.Clone());
            }

            return clone;
        }

        public bool Equals(IPrtValue val)
        {
            if (!(val is PrtNamedTuple tup))
            {
                return false;
            }

            if (tup.fieldValues.Count != fieldValues.Count)
            {
                return false;
            }

            for (var i = 0; i < tup.fieldValues.Count; i++)
            {
                if (fieldNames[i] != tup.fieldNames[i])
                {
                    return false;
                }

                if (!PrtValues.SafeEquals(fieldValues[i], tup.fieldValues[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            var hashCode = HashHelper.ComputeHash(fieldValues);
            return hashCode;
        }

        public override string ToString()
        {
            var retStr = "<";
            for (var i = 0; i < fieldValues.Count; i++)
            {
                retStr += fieldNames[i] + ":" + fieldValues[i] + ", ";
            }

            retStr += ">";
            return retStr;
        }

        public string ToEscapedString()
        {
            var retStr = "<";
            for (var i = 0; i < fieldValues.Count; i++)
            {
                var v = fieldValues[i] == null ? "null" : fieldValues[i].ToEscapedString();
                retStr += fieldNames[i] + ":" + v + ", ";
            }

            retStr += ">";
            return retStr;
        }

        public object ToDict()
        {
            var namedTupleDict = new Dictionary<string, object>();

            for (var i = 0; i < fieldValues.Count; i++)
            {
                var fieldValue = fieldValues[i] == null ? null : fieldValues[i].ToDict();
                var fieldName = fieldNames[i];
                namedTupleDict.Add(fieldName, fieldValue);
            }

            return namedTupleDict;
        }
    }
}