using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace PChecker.PRuntime
{
    public abstract class PrtType
    {
        public override string ToString()
        {
            throw new NotImplementedException("ToString function is not overriden in the derived class");
        }
    }

    public class PrtNullType : PrtType
    {
        public override string ToString()
        {
            return "NULL";
        }
    }

    public class PrtAnyType : PrtType
    {
        public override string ToString()
        {
            return "ANY";
        }
    }

    public class PrtMachineType : PrtType
    {
        public override string ToString()
        {
            return "MACHINE";
        }
    }

    public class PrtIntType : PrtType
    {
        public override string ToString()
        {
            return "INT";
        }
    }

    public class PrtFloatType : PrtType
    {
        public override string ToString()
        {
            return "FLOAT";
        }
    }

    public class PrtEnumType : PrtType
    {
        public Dictionary<long, string> enumConstants;
        public string name;

        public PrtEnumType(string typeName, params object[] args)
        {
            name = typeName;
            enumConstants = new Dictionary<long, string>();
            var i = 0;
            while (i < args.Length)
            {
                var enumConstantName = (string)args[i];
                i++;
                var enumConstantValue = (int)args[i];
                i++;
                enumConstants[enumConstantValue] = enumConstantName;
            }
        }

        public string DefaultConstant => enumConstants[0];

        public override string ToString()
        {
            return name;
        }
    }

    public class PrtPermissionType : PrtType
    {
        public string name;
        public List<string> permissions;

        public PrtPermissionType(string name, IEnumerable<string> perm)
        {
            this.name = name;
            permissions = perm.ToList();
        }

        public override string ToString()
        {
            return name;
        }
    }

    public class PrtBoolType : PrtType
    {
        public override string ToString()
        {
            return "BOOL";
        }
    }

    public class PrtStringType : PrtType
    {
        public override string ToString()
        {
            return "STRING";
        }
    }

    public class PrtEventType : PrtType
    {
        public override string ToString()
        {
            return "EVENT";
        }
    }

    public class PrtMapType : PrtType
    {
        public PrtType keyType;
        public PrtType valType;

        public PrtMapType(PrtType k, PrtType v)
        {
            keyType = k;
            valType = v;
        }

        public override string ToString()
        {
            return $"({keyType} -> {valType})";
        }
    }

    public class PrtSeqType : PrtType
    {
        public PrtType elemType;

        public PrtSeqType(PrtType s)
        {
            elemType = s;
        }

        public override string ToString()
        {
            return $"seq[{elemType}]";
        }
    }

    public class PrtSetType : PrtType
    {
        public PrtType elemType;

        public PrtSetType(PrtType s)
        {
            elemType = s;
        }

        public override string ToString()
        {
            return $"set[{elemType}]";
        }
    }

    public class PrtTupleType : PrtType
    {
        public List<PrtType> fieldTypes;

        public PrtTupleType()
        {
            /*
               This constructor is added only to prevent the other constructor from being called
               when an instance of PrtNamedTupleType is created.
             */
        }

        public PrtTupleType(params PrtType[] fields)
        {
            Debug.Assert(fields.Any());
            fieldTypes = new List<PrtType>();
            foreach (var f in fields)
            {
                fieldTypes.Add(f);
            }
        }

        public override string ToString()
        {
            var retStr = "<";
            foreach (var f in fieldTypes)
            {
                retStr += f + ", ";
            }

            retStr += ">";
            return retStr;
        }
    }

    public class PrtNamedTupleType : PrtTupleType
    {
        public List<string> fieldNames;

        public PrtNamedTupleType(params object[] args)
        {
            Debug.Assert(args.Length > 0);
            fieldNames = new List<string>();
            fieldTypes = new List<PrtType>();

            var index = 0;
            while (index < args.Length)
            {
                fieldNames.Add((string)args[index]);
                index++;
                fieldTypes.Add((PrtType)args[index]);
                index++;
            }
        }

        public override string ToString()
        {
            var retStr = "<";
            var index = 0;
            while (index < fieldTypes.Count)
            {
                retStr += fieldNames[index] + ":" + fieldTypes[index] + ", ";
                index++;
            }

            retStr += ">";
            return retStr;
        }
    }
}