// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using SwiftRuntimeLibrary;

namespace SwiftReflector
{
    public class SwiftType
    {
        protected SwiftType(CoreCompoundType type, bool isReference, SwiftName name = null)
        {
            Type = type;
            IsReference = isReference;
            Name = name;
            Attributes = new List<SwiftTypeAttribute>();
            IsVariadic = false;
        }

        public CoreCompoundType Type { get; private set; }
        public SwiftName Name { get; private set; }
        public bool IsReference { get; private set; }
        public List<SwiftTypeAttribute> Attributes { get; private set; }
        public bool IsVariadic { get; set; }


        public SwiftType ReferenceCloneOf()
        {
            if (IsReference)
                return this;
            var ty = MemberwiseClone() as SwiftType;
            ty.IsReference = true;
            return ty;
        }

        public SwiftType NonReferenceCloneOf()
        {
            if (!IsReference)
                return this;
            var ty = MemberwiseClone() as SwiftType;
            ty.IsReference = false;
            return ty;
        }

        public SwiftType RenamedCloneOf(SwiftName newName)
        {
            var ty = MemberwiseClone() as SwiftType;
            ty.Name = newName;
            return ty;
        }

        public bool IsEmptyTuple
        {
            get
            {
                var tuple = this as SwiftTupleType;
                return tuple == null ? false : tuple.IsEmpty;
            }
        }
        public virtual bool IsClass { get { return false; } }
        public virtual bool IsStruct { get { return false; } }
        public virtual bool IsEnum { get { return false; } }
        public virtual bool IsProtocol { get { return false; } }

        public bool HasAttribute(SwiftTypeAttribute attribute)
        {
            return Attributes.Any(attr => attr == attribute);
        }

        [Obsolete("this is not supported in Swift 5")]
        public bool HasObjCAttribute => HasAttribute(SwiftTypeAttribute.ObjC);

        public override int GetHashCode()
        {
            return Type.GetHashCode() +
                (Name != null ? Name.GetHashCode() : 0);
        }

        public override bool Equals(object obj)
        {
            var type = obj as SwiftType;
            if (type == null)
                return false;
            if (type.Type != Type)
                return false;
            if (type.IsReference != IsReference)
                return false;
            if (type.IsVariadic != IsVariadic)
                return false;
            if (type.GetType() != this.GetType())
                return false;
            // shouldn't do Name equality except in functions
            return LLEquals(type);
        }

        public virtual bool EqualsReferenceInvaraint(SwiftType type)
        {
            var a = ProjectAsNonReference(this);
            var b = ProjectAsNonReference(type);

            if (b.Type != a.Type)
                return false;
            if (b.GetType() != a.GetType())
                return false;
            // shouldn't do Name equality except in functions
            return a.LLEquals(b);
        }

        static SwiftType ProjectAsNonReference(SwiftType a)
        {
            if (a.IsReference)
            {
                return a.NonReferenceCloneOf();
            }
            var bgt = a as SwiftBoundGenericType;
            if (bgt != null && bgt.BoundTypes.Count == 1)
            {
                var baseType = bgt.BaseType as SwiftClassType;
                if (baseType != null)
                {
                    string name = baseType.ClassName.ToFullyQualifiedName(true);
                    if (name == "Swift.UnsafePointer" || name == "Swift.UnsafeMutablePointer")
                        return bgt.BoundTypes[0];
                }
            }
            return a;
        }

        protected virtual bool LLEquals(SwiftType other)
        {
            return true;
        }


        public static bool IsStructScalar(SwiftType st)
        {
            if (st == null)
                return false;
            if (st is SwiftBuiltInType)
                return true;
            var ct = st as SwiftClassType;
            if (ct == null)
                return false;
            return IsStructScalar(ct.ClassName.ToFullyQualifiedName());
        }

        public static bool IsStructScalar(string fullyQualifiedName)
        {
            switch (fullyQualifiedName)
            {
                case "Swift.Int64":
                case "Swift.UInt64":
                case "Swift.Int32":
                case "Swift.UInt32":
                case "Swift.Int16":
                case "Swift.UInt16":
                case "Swift.Int8":
                case "Swift.UInt8":
                case "Swift.Char":
                case "CoreGraphics.CGFloat":
                case "Swift.UnsafeRawPointer":
                case "Swift.UnsafeMutableRawPointer":
                case "Swift.OpaquePointer":
                    return true;
                default:
                    return false;
            }
        }
    }

    public class SwiftTupleType : SwiftType
    {
        public SwiftTupleType(bool isReference)
            : this(null, isReference, null)
        {
        }

        public SwiftTupleType(IEnumerable<SwiftType> contents, bool isReference, SwiftName name = null)
           : base(CoreCompoundType.Tuple, isReference, name)
        {
            Contents = new List<SwiftType>();
            if (contents != null)
                Contents.AddRange(contents);
        }

        public SwiftTupleType(bool isReference, SwiftName name, params SwiftType[] contents)
            : this(contents, isReference, name)
        {
        }

        public List<SwiftType> Contents { get; private set; }
        public bool IsEmpty { get { return Contents.Count == 0; } }
        static SwiftTupleType empty = new SwiftTupleType(null, false, null);
        public static SwiftTupleType Empty { get { return empty; } }

        public bool HasNames()
        {
            return Contents.FirstOrDefault(st => st.Name != null) != null;
        }

        protected override bool LLEquals(SwiftType other)
        {
            SwiftTupleType st = other as SwiftTupleType;
            if (st == null)
                return false;
            return Contents.SequenceEqual(st.Contents);
        }

        public SwiftType AllButFirst()
        {
            if (IsEmpty)
                throw new ArgumentOutOfRangeException("tuple is empty");
            // seriously, this is what we want to do.
            // If a function has one argument, it will be a the simple type.
            // If a function has more than one argument, it will be a tuple.
            if (Contents.Count == 2)
                return Contents[1];
            return new SwiftTupleType(Contents.TakeWhile((st, i) => i > 0), IsReference, Name);
        }

        public SwiftType AllButFirstN(int n)
        {
            if (IsEmpty)
                throw new ArgumentOutOfRangeException("tuple is empty");
            // seriously, this is what we want to do.
            // If a function has one argument, it will be a the simple type.
            // If a function has more than one argument, it will be a tuple.
            // So if we are returning the last element, we're returning an single not a tuple.
            if (Contents.Count == n + 1)
                return Contents[n];
            return new SwiftTupleType(Contents.Skip(n), IsReference, Name);
        }

        public override string ToString()
        {
            var contents = Contents.Select(el =>
            {
                var elname = el.Name != null ? el.Name.Name + ": " : "";
                return elname + el.ToString();
            }).InterleaveCommas();
            return $"({contents})";
        }
    }

    public class SwiftBuiltInType : SwiftType
    {
        public SwiftBuiltInType(CoreBuiltInType scalarType, bool isReference, SwiftName name = null)
            : base(CoreCompoundType.Scalar, isReference, name)
        {
            BuiltInType = scalarType;
        }

        public CoreBuiltInType BuiltInType { get; private set; }

        protected override bool LLEquals(SwiftType other)
        {
            SwiftBuiltInType sb = other as SwiftBuiltInType;
            return sb != null && BuiltInType == sb.BuiltInType;
        }
        public override string ToString()
        {
            var name = Name != null ? Name.Name + ": " : "";
            return name + BuiltInType.ToString();
        }

    }

    public class SwiftClassType : SwiftType
    {
        public SwiftClassType(SwiftClassName className, bool isReference, SwiftName name = null)
            : base(CoreCompoundType.Class, isReference, name)
        {
            ClassName = className;
        }
        public SwiftClassName ClassName { get; private set; }
        public override bool IsClass { get { return EntityKind == MemberNesting.Class; } }
        public override bool IsStruct { get { return EntityKind == MemberNesting.Struct; } }
        public override bool IsEnum { get { return EntityKind == MemberNesting.Enum; } }
        public override bool IsProtocol { get { return EntityKind == MemberNesting.Protocol; } }

        public MemberNesting EntityKind { get { return ClassName.Nesting.Last(); } }

        protected override bool LLEquals(SwiftType other)
        {
            var sct = other as SwiftClassType;
            return sct != null && ClassName.Equals(sct.ClassName);
        }

        public override string ToString()
        {
            var name = Name != null ? Name.Name + ": " : "";
            return name + ClassName.ToFullyQualifiedName();
        }
    }

    public class SwiftBoundGenericType : SwiftType
    {
        public SwiftBoundGenericType(SwiftType baseType, List<SwiftType> boundTypes, bool isReference, SwiftName name = null)
            : base(CoreCompoundType.BoundGeneric, isReference, name)
        {
            BaseType = Exceptions.ThrowOnNull(baseType, "baseType");
            BoundTypes = new List<SwiftType>();
            if (boundTypes != null)
                BoundTypes.AddRange(boundTypes);
        }
        public SwiftType BaseType { get; private set; }
        public List<SwiftType> BoundTypes { get; private set; }
        protected override bool LLEquals(SwiftType other)
        {
            var bgt = other as SwiftBoundGenericType;
            return bgt != null && BaseType.Equals(bgt.BaseType)
                && BoundTypes.SequenceEqual(bgt.BoundTypes);
        }

        public override bool IsClass { get { return BaseType.IsClass; } }
        public override bool IsEnum { get { return BaseType.IsEnum; } }
        public override bool IsStruct { get { return BaseType.IsStruct; } }
        public override string ToString()
        {
            var name = Name != null ? Name.Name + ": " : "";
            var genArgs = BoundTypes.Select(arg => arg.ToString()).InterleaveCommas();
            return $"{name}{BaseType.ToString()}<{genArgs}>";
        }
    }

}

