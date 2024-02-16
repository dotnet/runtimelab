// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SwiftReflector.Demangling;
using SwiftRuntimeLibrary;

namespace SwiftReflector
{
    public class SwiftClassName
    {
        public SwiftClassName(SwiftName module, IList<MemberNesting> nesting, IList<SwiftName> nestingNames, OperatorType oper = OperatorType.None)
        {
            Module = Exceptions.ThrowOnNull(module, "module");
            Nesting = Exceptions.ThrowOnNull(nesting, "nesting");
            NestingNames = Exceptions.ThrowOnNull(nestingNames, "nestingNames");
            Terminus = NestingNames.Count > 0 ? NestingNames[NestingNames.Count - 1] : null;
            Operator = oper;
        }

        public static SwiftClassName FromFullyQualifiedName(string fullyQualifiedName, OperatorType oper, params char[] nesting)
        {
            string[] parts = Exceptions.ThrowOnNull(fullyQualifiedName, "fullyQualifiedName").Split('.');
            if (parts.Length < 2)
                throw new ArgumentException(String.Format("Fully qualified name '{0}' requires at least a module and one name.",
                    fullyQualifiedName));
            if (nesting.Length != parts.Length - 1)
                throw new ArgumentException(String.Format("Nesting should have {0} elements, but has {1}.",
                    parts.Length - 1, nesting.Length), "nesting");
            SwiftName module = new SwiftName(parts[0], false);
            List<SwiftName> nestingNames = parts.Skip(1).Select(name => new SwiftName(name, false)).ToList();
            List<MemberNesting> actualNesting = nesting.Select(c => Decomposer.ToMaybeMemberNesting(c, true).Value).ToList();
            return new SwiftClassName(module, actualNesting, nestingNames, oper);
        }

        public static SwiftClassName FromFullyQualifiedName(string fullyQualifiedName, OperatorType oper, string nesting)
        {
            return FromFullyQualifiedName(fullyQualifiedName, oper, nesting.ToArray());
        }

        public SwiftName Module { get; private set; }
        public IList<MemberNesting> Nesting { get; private set; }
        public IList<SwiftName> NestingNames { get; private set; }
        public SwiftName Terminus { get; private set; }
        public OperatorType Operator { get; set; }

        public bool IsClass { get { return Nesting.Count > 0 && Nesting.Last() == MemberNesting.Class; } }
        public bool IsStruct { get { return Nesting.Count > 0 && Nesting.Last() == MemberNesting.Struct; } }
        public bool IsEnum { get { return Nesting.Count > 0 && Nesting.Last() == MemberNesting.Enum; } }
        public bool IsOperator { get { return Operator != OperatorType.None; } }
        public string ToFullyQualifiedName(bool includeModule = true)
        {
            var sb = new StringBuilder();
            if (includeModule)
                sb.Append(Module.Name);
            for (int i = 0; i < NestingNames.Count; i++)
            {
                if (includeModule || (!includeModule && i > 0))
                    sb.Append('.');
                sb.Append(NestingNames[i].Name);
            }
            return sb.ToString();
        }

        public override int GetHashCode()
        {
            int hashCode = Module.GetHashCode();
            foreach (SwiftName name in NestingNames)
                hashCode += name.GetHashCode();
            return hashCode;
        }

        public override bool Equals(object obj)
        {
            var other = obj as SwiftClassName;
            if (other == null)
            {
                return false;
            }
            if (other == this)
                return true;
            return Module.Equals(other.Module) && NamesAreEqual(other);
        }

        public override string ToString()
        {
            return ToFullyQualifiedName(true);
        }

        bool NamesAreEqual(SwiftClassName other)
        {
            if (NestingNames.Count != other.NestingNames.Count)
                return false;
            for (int i = 0; i < NestingNames.Count; i++)
            {
                if (!NestingNames[i].Equals(other.NestingNames[i]))
                    return false;
            }
            return true;
        }

    }
}

