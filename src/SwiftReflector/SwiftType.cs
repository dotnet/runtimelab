// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using SwiftReflector.Demangling;
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


        public bool IsConstructor { get { return this is SwiftConstructorType; } }

        public bool IsOptionalConstructor
        {
            get
            {
                // a SwiftType is an optional ctor if:
                // 1 it's a SwiftConstructorType
                // 2 its return type is SwiftBoundGenericType
                // 3 its return type has a base of Swift.Optional
                // 4 its return type has exactly one bound type to the type of
                //   the metatype of this class
                var ctor = this as SwiftConstructorType;
                if (ctor == null)
                    return false;
                var returnType = ctor.ReturnType as SwiftBoundGenericType;
                if (returnType == null)
                    return false;
                if (returnType.BoundTypes.Count != 1)
                    return false;
                var baseType = returnType.BaseType as SwiftClassType;
                if (baseType == null)
                    return false;
                var boundType = returnType.BoundTypes[0] as SwiftClassType;
                if (boundType == null)
                    return false;
                var uncurriedParameter = ctor.UncurriedParameter as SwiftMetaClassType;
                if (uncurriedParameter == null)
                    return false;
                return baseType.ClassName.ToFullyQualifiedName() == "Swift.Optional" &&
                           boundType.ClassName.ToFullyQualifiedName(true) == uncurriedParameter.Class.ClassName.ToFullyQualifiedName(true);
            }
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

    public class SwiftModuleNameType : SwiftType
    {
        public SwiftModuleNameType(SwiftName name, bool isReference)
            : base(CoreCompoundType.ModuleName, isReference, name)
        {
        }
        public override string ToString()
        {
            return Name != null ? Name.Name : "(unknown module)";
        }
    }

    public abstract class SwiftBaseFunctionType : SwiftType
    {
        public SwiftBaseFunctionType(SwiftType parms, SwiftType ret, bool isReference, bool canThrow, SwiftName name = null, SwiftType extensionOn = null)
            : base(CoreCompoundType.Function, isReference, name)
        {
            Parameters = Exceptions.ThrowOnNull(parms, nameof(parms));
            ReturnType = Exceptions.ThrowOnNull(ret, nameof(ret));
            GenericArguments = new List<GenericArgument>();
            CanThrow = canThrow;
            ExtensionOn = extensionOn;
        }

        public bool ContainsGenericParameters
        {
            get
            {
                return GenericArguments.Count() > 0;
            }
        }
        public SwiftType ExtensionOn { get; set; }
        public abstract MemberType MemberType { get; }
        public SwiftType Parameters { get; private set; }
        public SwiftType ReturnType { get; private set; }
        public List<GenericArgument> GenericArguments { get; private set; }
        public bool CanThrow { get; private set; }
        public bool IsExtension { get { return ExtensionOn != null; } }
        public virtual bool IsThunk => false;
        public SwiftBaseFunctionType Thunk { get; set; }

        public abstract SwiftBaseFunctionType AsThunk();

        // for short-lived discretionary storage of information
        public string DiscretionaryString { get; set; }

        public IEnumerable<SwiftType> EachParameter
        {
            get
            {
                for (int i = 0; i < ParameterCount; i++)
                {
                    yield return GetParameter(i);
                }
            }
        }

        public int GenericParameterCount
        {
            get { return EachParameter.Sum(st => st is SwiftGenericArgReferenceType ? 1 : 0); }
        }


        public bool IsVoid { get { return ReturnType.Type == CoreCompoundType.Tuple && ((SwiftTupleType)ReturnType).IsEmpty; } }

        protected override bool LLEquals(SwiftType other)
        {
            var fn = other as SwiftBaseFunctionType;
            if (fn == null)
                return false;
            if (Name != null)
                Name.Equals(fn.Name);
            return MemberType == fn.MemberType && Parameters.Equals(fn.Parameters)
                && ReturnType.Equals(fn.ReturnType);
        }

        public int ParameterCount
        {
            get
            {
                var tt = Parameters as SwiftTupleType;
                if (tt == null)
                {
                    return 1;
                }
                else
                {
                    return tt.Contents.Count;
                }
            }
        }

        public SwiftType GetParameter(int index)
        {
            var tt = Parameters as SwiftTupleType;
            if (tt == null)
            {
                if (index == 0)
                    return Parameters;
                else
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
            else
            {
                if (index < 0 || index >= tt.Contents.Count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return tt.Contents[index];
            }
        }

        public override string ToString()
        {
            var name = Name != null ? Name.Name + ": " : "";
            return $"{name}{Parameters.ToString()}->{ReturnType.ToString()}";
        }
    }

    public class SwiftCFunctionType : SwiftBaseFunctionType
    {
        public SwiftCFunctionType(SwiftType parms, SwiftType ret, bool isReference, SwiftName name = null)
            : base(parms, ret, isReference, false, name, null)
        {
        }

        public override MemberType MemberType
        {
            get
            {
                return MemberType.CFunction;
            }
        }

        public override SwiftBaseFunctionType AsThunk()
        {
            var thunk = new SwiftCFunctionTypeThunk(Parameters, ReturnType, IsReference, Name);
            thunk.DiscretionaryString = DiscretionaryString;
            return thunk;
        }
    }

    public class SwiftCFunctionTypeThunk : SwiftCFunctionType
    {
        public SwiftCFunctionTypeThunk(SwiftType parms, SwiftType ret, bool isReference, SwiftName name = null)
            : base(parms, ret, isReference, name)
        {
        }

        public override bool IsThunk => true;

        public override SwiftBaseFunctionType AsThunk() => this;
    }

    public class SwiftAddressorType : SwiftBaseFunctionType
    {
        public SwiftAddressorType(AddressorType addressor, SwiftType ret, bool isReference, SwiftName name = null)
            : base(SwiftTupleType.Empty, ret, isReference, false, name, null)
        {
            AddressorType = addressor;
        }
        public AddressorType AddressorType { get; private set; }
        public override MemberType MemberType
        {
            get
            {
                return MemberType.Addressor;
            }
        }

        public override SwiftBaseFunctionType AsThunk()
        {
            var thunk = new SwiftAddressorThunkType(AddressorType, ReturnType, IsReference, Name);
            thunk.DiscretionaryString = DiscretionaryString;
            return thunk;
        }
    }

    public class SwiftAddressorThunkType : SwiftAddressorType
    {
        public SwiftAddressorThunkType(AddressorType addressor, SwiftType ret, bool isReference, SwiftName name = null)
            : base(addressor, ret, isReference, name)
        {
        }

        public override bool IsThunk => true;

        public override SwiftBaseFunctionType AsThunk() => this;
    }

    public class SwiftInitializerType : SwiftBaseFunctionType
    {
        public SwiftInitializerType(InitializerType initType, SwiftType ret, SwiftClassType owner, SwiftName name)
            : base(SwiftTupleType.Empty, ret, false, false, name, null)
        {
            Owner = Exceptions.ThrowOnNull(owner, nameof(owner));
            InitializerType = initType;
        }

        public InitializerType InitializerType { get; private set; }
        public SwiftClassType Owner { get; private set; }
        public override MemberType MemberType
        {
            get
            {
                return MemberType.Initializer;
            }
        }

        public override SwiftBaseFunctionType AsThunk()
        {
            var thunk = new SwiftInitializerThunkType(InitializerType, ReturnType, Owner, Name);
            thunk.DiscretionaryString = DiscretionaryString;
            return thunk;
        }
    }

    public class SwiftInitializerThunkType : SwiftInitializerType
    {
        public SwiftInitializerThunkType(InitializerType initType, SwiftType ret, SwiftClassType owner, SwiftName name)
            : base(initType, ret, owner, name)
        {
        }

        public override bool IsThunk => true;

        public override SwiftBaseFunctionType AsThunk() => this;
    }

    public class SwiftFunctionType : SwiftBaseFunctionType
    {
        public SwiftFunctionType(SwiftType parms, SwiftType ret, bool isReference, bool canThrow, SwiftName name = null, SwiftType extensionOn = null, bool isEscaping = true)
            : base(parms, ret, isReference, canThrow, name, extensionOn)
        {
            IsEscaping = isEscaping;
        }

        public override MemberType MemberType { get { return MemberType.Function; } }
        public bool IsEscaping { get; private set; }

        public override SwiftBaseFunctionType AsThunk()
        {
            var thunk = new SwiftFunctionThunkType(Parameters, ReturnType, IsReference, CanThrow, Name, ExtensionOn, IsEscaping);
            thunk.DiscretionaryString = DiscretionaryString;
            return thunk;
        }
    }

    public class SwiftFunctionThunkType : SwiftFunctionType
    {
        public SwiftFunctionThunkType(SwiftType parms, SwiftType ret, bool isReference, bool canThrow, SwiftName name = null, SwiftType extensionOn = null, bool isEscaping = true)
            : base(parms, ret, isReference, canThrow, name, extensionOn, isEscaping)
        {
        }

        public override bool IsThunk => true;

        public override SwiftBaseFunctionType AsThunk() => this;
    }

    public class SwiftCFunctionPointerType : SwiftFunctionType
    {
        public SwiftCFunctionPointerType(SwiftType parms, SwiftType ret, bool isReference, bool canThrow, SwiftName name = null)
            : base(parms, ret, isReference, canThrow, name, null)
        {
        }

        public override SwiftBaseFunctionType AsThunk()
        {
            var thunk = new SwiftCFunctionPointerThunkType(Parameters, ReturnType, IsReference, CanThrow, Name);
            thunk.DiscretionaryString = DiscretionaryString;
            return thunk;
        }
    }

    public class SwiftCFunctionPointerThunkType : SwiftCFunctionPointerType
    {
        public SwiftCFunctionPointerThunkType(SwiftType parms, SwiftType ret, bool isReference, bool canThrow, SwiftName name = null)
            : base(parms, ret, isReference, canThrow, name)
        {
        }

        public override bool IsThunk => true;

        public override SwiftBaseFunctionType AsThunk() => this;
    }

    public class SwiftStaticFunctionType : SwiftFunctionType
    {
        public SwiftStaticFunctionType(SwiftType parms, SwiftType ret, bool isReference, bool canThrow, SwiftClassType ofClass, SwiftName name = null, SwiftType extensionOn = null)
            : base(parms, ret, isReference, canThrow, name, extensionOn)
        {
            OfClass = ofClass;
        }

        public SwiftClassType OfClass { get; private set; }

        public override SwiftBaseFunctionType AsThunk()
        {
            var func = new SwiftStaticFunctionThunkType(Parameters, ReturnType, IsReference, CanThrow, OfClass, Name, ExtensionOn);
            func.DiscretionaryString = DiscretionaryString;
            return func;
        }
    }

    public class SwiftStaticFunctionThunkType : SwiftStaticFunctionType
    {
        public SwiftStaticFunctionThunkType(SwiftType parms, SwiftType ret, bool isReference, bool canThrow, SwiftClassType ofClass, SwiftName name = null, SwiftType extensionOn = null)
            : base(parms, ret, isReference, canThrow, ofClass, name, extensionOn)
        {
        }

        public override bool IsThunk => true;

        public override SwiftBaseFunctionType AsThunk() => this;
    }


    public class SwiftClassConstructorType : SwiftFunctionType
    {
        public SwiftClassConstructorType(SwiftMetaClassType meta, bool isReference)
            : base(SwiftTupleType.Empty, Exceptions.ThrowOnNull(meta, "meta"), isReference, false, Decomposer.kSwiftClassConstructorName)
        {
        }

        public override SwiftBaseFunctionType AsThunk()
        {
            var thunk = new SwiftClassConstructorThunkType(ReturnType as SwiftMetaClassType, IsReference);
            thunk.DiscretionaryString = DiscretionaryString;
            return thunk;
        }
    }

    public class SwiftClassConstructorThunkType : SwiftClassConstructorType
    {
        public SwiftClassConstructorThunkType(SwiftMetaClassType meta, bool isReference)
            : base(meta, isReference)
        {
        }

        public override bool IsThunk => true;

        public override SwiftBaseFunctionType AsThunk() => this;
    }

    public class SwiftUncurriedFunctionType : SwiftBaseFunctionType
    {
        public SwiftUncurriedFunctionType(SwiftType unCurriedParameter,
                                           SwiftType parms, SwiftType ret, bool isReference, bool canThrow, SwiftName name = null, SwiftType extensionOn = null)
            : this(MemberType.UncurriedFunction, unCurriedParameter, parms, ret, isReference, canThrow, name, extensionOn)
        {
        }

        protected SwiftUncurriedFunctionType(MemberType memberType, SwiftType unCurriedParameter, SwiftType parms, SwiftType ret, bool isReference, bool canThrow, SwiftName name = null, SwiftType extensionOn = null)
            : base(parms, ret, isReference, canThrow, name, extensionOn)
        {
            // oddly enough, this is allowed to be null
            UncurriedParameter = unCurriedParameter;
            this.memberType = memberType;
        }
        MemberType memberType;
        public override MemberType MemberType { get { return memberType; } }
        public SwiftType UncurriedParameter { get; private set; }

        protected override bool LLEquals(SwiftType other)
        {
            var ucf = other as SwiftUncurriedFunctionType;
            return ucf != null && ucf.UncurriedParameter.Equals(UncurriedParameter) && base.LLEquals(other);
        }

        public override SwiftBaseFunctionType AsThunk()
        {
            var thunk = new SwiftUncurriedFunctionThunkType(UncurriedParameter, Parameters, ReturnType, IsReference, CanThrow, Name, ExtensionOn);
            thunk.DiscretionaryString = DiscretionaryString;
            return thunk;
        }
    }

    public class SwiftUncurriedFunctionThunkType : SwiftUncurriedFunctionType
    {
        public SwiftUncurriedFunctionThunkType(SwiftType unCurriedParameter,
                           SwiftType parms, SwiftType ret, bool isReference, bool canThrow, SwiftName name = null, SwiftType extensionOn = null)
            : base(unCurriedParameter, parms, ret, isReference, canThrow, name, extensionOn)
        {
        }

        public override bool IsThunk => true;

        public override SwiftBaseFunctionType AsThunk() => this;
    }


    public class SwiftConstructorType : SwiftUncurriedFunctionType
    {
        public SwiftConstructorType(bool isAllocating, SwiftType unCurriedParameter, SwiftType parms, SwiftType ret, bool isReference, bool canThrow, SwiftType extensionOn = null)
            : base(isAllocating ? MemberType.Allocator : MemberType.Constructor,
            unCurriedParameter, parms, ret, isReference, canThrow, isAllocating ? Decomposer.kSwiftAllocatingConstructorName :
                Decomposer.kSwiftNonAllocatingConstructorName, extensionOn)
        {
        }

        public override SwiftBaseFunctionType AsThunk()
        {
            var thunk = new SwiftConstructorThunkType(MemberType == MemberType.Allocator, UncurriedParameter,
                Parameters, ReturnType, IsReference, CanThrow, ExtensionOn);
            thunk.DiscretionaryString = DiscretionaryString;
            return thunk;
        }
    }

    public class SwiftConstructorThunkType : SwiftConstructorType
    {
        public SwiftConstructorThunkType(bool isAllocating, SwiftType unCurriedParameter, SwiftType parms, SwiftType ret, bool isReference, bool canThrow, SwiftType extensionOn = null)
            : base(isAllocating, unCurriedParameter, parms, ret, isReference, canThrow, extensionOn)
        {
        }

        public override bool IsThunk => true;

        public override SwiftBaseFunctionType AsThunk() => this;
    }

    public class SwiftDestructorType : SwiftBaseFunctionType
    {
        public SwiftDestructorType(bool isDeallocating, SwiftClassType classType, bool isReference, bool canThrow)
            : base(classType, classType, isReference, canThrow,
            isDeallocating ? Decomposer.kSwiftDeallocatingDestructorName : Decomposer.kSwiftNonDeallocatingDestructorName, null)
        {
            memberType = isDeallocating ? MemberType.Deallocator : MemberType.Destructor;
        }
        MemberType memberType;
        public override MemberType MemberType { get { return memberType; } }

        public override SwiftBaseFunctionType AsThunk()
        {
            var thunk = new SwiftDestructorThunkType(Name == Decomposer.kSwiftDeallocatingDestructorName, ReturnType as SwiftClassType, IsReference, CanThrow);
            thunk.DiscretionaryString = DiscretionaryString;
            return thunk;
        }
    }

    public class SwiftDestructorThunkType : SwiftDestructorType
    {
        public SwiftDestructorThunkType(bool isDeallocating, SwiftClassType classType, bool isReference, bool canThrow)
            : base(isDeallocating, classType, isReference, canThrow)
        {
        }

        public override bool IsThunk => true;

        public override SwiftBaseFunctionType AsThunk() => this;
    }

    public class SwiftPropertyType : SwiftUncurriedFunctionType
    {
        public SwiftPropertyType(SwiftType unCurriedParameter, PropertyType propType, SwiftName propName,
                                  SwiftName privateName, SwiftType ofType, bool isStatic, bool isReference, SwiftType extensionOn = null)
            : base(unCurriedParameter,
                (propType == PropertyType.Setter || propType == PropertyType.Materializer) ? ofType : SwiftTupleType.Empty,
                (propType == PropertyType.Getter) ? ofType : SwiftTupleType.Empty,
                isReference, false, propName, extensionOn)
        {
            PropertyType = propType;
            PrivateName = privateName;
            OfType = Exceptions.ThrowOnNull(ofType, "ofType");
            IsSubscript = false;
            IsStatic = isStatic;
        }
        public SwiftPropertyType(SwiftType unCurriedParameter, PropertyType propType, SwiftName propName,
                                  SwiftName privateName, SwiftFunctionType accessor, bool isStatic, bool isReference, SwiftType extensionOn = null)
            : base(unCurriedParameter, accessor.Parameters, accessor.ReturnType, isReference, false, propName, extensionOn)
        {
            PropertyType = propType;
            PrivateName = privateName;
            OfType = accessor;
            IsSubscript = true;
            IsStatic = isStatic;
        }

        public PropertyType PropertyType { get; private set; }
        public SwiftName PrivateName { get; private set; }
        public SwiftType OfType { get; private set; }
        public bool IsStatic { get; private set; }

        public bool IsSubscript { get; private set; }
        public bool IsPublic { get { return PrivateName == null; } }
        public bool IsPrivate { get { return PrivateName != null; } }
        public bool IsGlobal { get { return UncurriedParameter == null; } }

        public SwiftPropertyType RecastAsStatic()
        {
            if (IsStatic)
                return this;
            SwiftPropertyType newProp = null;
            if (OfType is SwiftFunctionType)
            {
                if (this is SwiftPropertyThunkType)
                {
                    newProp = new SwiftPropertyThunkType(UncurriedParameter, PropertyType, Name, PrivateName, OfType as SwiftFunctionType,
                  true, IsReference);

                }
                else
                {
                    newProp = new SwiftPropertyType(UncurriedParameter, PropertyType, Name, PrivateName, OfType as SwiftFunctionType,
                                      true, IsReference);
                }
            }
            else
            {
                if (this is SwiftPropertyThunkType)
                {
                    newProp = new SwiftPropertyThunkType(UncurriedParameter, PropertyType, Name, PrivateName, OfType, true, IsReference);

                }
                else
                {
                    newProp = new SwiftPropertyType(UncurriedParameter, PropertyType, Name, PrivateName, OfType, true, IsReference);
                }
            }
            newProp.DiscretionaryString = DiscretionaryString;
            newProp.ExtensionOn = this.ExtensionOn;
            return newProp;
        }

        public override SwiftBaseFunctionType AsThunk()
        {
            if (IsSubscript)
            {
                var pt = new SwiftPropertyThunkType(UncurriedParameter, PropertyType, Name,
                    PrivateName, OfType as SwiftFunctionType, IsStatic, IsReference, ExtensionOn);
                pt.DiscretionaryString = DiscretionaryString;
                return pt;
            }
            else
            {
                var pt = new SwiftPropertyThunkType(UncurriedParameter, PropertyType, Name,
                    PrivateName, OfType, IsStatic, IsReference, ExtensionOn);
                pt.DiscretionaryString = DiscretionaryString;
                return pt;
            }
        }
    }

    public class SwiftPropertyThunkType : SwiftPropertyType
    {
        public SwiftPropertyThunkType(SwiftType unCurriedParameter, PropertyType propType, SwiftName propName,
                      SwiftName privateName, SwiftType ofType, bool isStatic, bool isReference, SwiftType extensionOn = null)
            : base(unCurriedParameter, propType, propName, privateName, ofType, isStatic, isReference, extensionOn)
        {
        }

        public SwiftPropertyThunkType(SwiftType unCurriedParameter, PropertyType propType, SwiftName propName,
              SwiftName privateName, SwiftFunctionType accessor, bool isStatic, bool isReference, SwiftType extensionOn = null)
            : base(unCurriedParameter, propType, propName, privateName, accessor, isStatic, isReference, extensionOn)
        {
        }

        public override bool IsThunk => true;

        public override SwiftBaseFunctionType AsThunk() => this;
    }

    public class SwiftExplicitClosureType : SwiftBaseFunctionType
    {
        public SwiftExplicitClosureType(bool isReference)
            : base(SwiftTupleType.Empty, SwiftTupleType.Empty, isReference, false, null)
        {
        }

        public override MemberType MemberType
        {
            get
            {
                return MemberType.ExplicitClosure;
            }
        }

        public override SwiftBaseFunctionType AsThunk()
        {
            var thunk = new SwiftExplicitClosureThunkType(IsReference);
            thunk.DiscretionaryString = DiscretionaryString;
            return thunk;
        }
    }

    public class SwiftExplicitClosureThunkType : SwiftExplicitClosureType
    {
        public SwiftExplicitClosureThunkType(bool isReference)
            : base(isReference)
        {
        }

        public override bool IsThunk => true;

        public override SwiftBaseFunctionType AsThunk() => this;
    }

    public class SwiftWitnessTableType : SwiftUncurriedFunctionType
    {
        public SwiftWitnessTableType(WitnessType witnessType, SwiftClassType protocolType = null, SwiftClassType owningType = null)
            : base((SwiftType)owningType ?? SwiftTupleType.Empty, SwiftTupleType.Empty, SwiftTupleType.Empty, false, false, null)
        {
            WitnessType = witnessType;
            if (WitnessType == WitnessType.Protocol && protocolType == null)
                throw new ArgumentNullException(nameof(protocolType));
            ProtocolType = protocolType;
        }
        public WitnessType WitnessType { get; private set; }
        public SwiftClassType ProtocolType { get; private set; }

        public override SwiftBaseFunctionType AsThunk()
        {
            var thunk = new SwiftWitnessTableThunkType(WitnessType, ProtocolType, UncurriedParameter as SwiftClassType);
            thunk.DiscretionaryString = DiscretionaryString;
            return thunk;
        }
    }

    public class SwiftWitnessTableThunkType : SwiftWitnessTableType
    {
        public SwiftWitnessTableThunkType(WitnessType witnessType, SwiftClassType protocolType = null, SwiftClassType owningType = null)
            : base(witnessType, protocolType, owningType)
        {
        }

        public override bool IsThunk => true;
        public override SwiftBaseFunctionType AsThunk() => this;
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

    public class SwiftArrayType : SwiftType
    {
        public SwiftArrayType(bool isReference, SwiftName name = null)
            : base(CoreCompoundType.Array, isReference, name)
        {
        }
        public override string ToString()
        {
            var name = Name != null ? Name.Name + ": " : "";
            return $"{name}[]";
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

    public class SwiftProtocolListType : SwiftType
    {
        public SwiftProtocolListType(IEnumerable<SwiftClassType> protocols, bool isReference, SwiftName name = null)
            : base(CoreCompoundType.ProtocolList, isReference, name)
        {
            Protocols = new List<SwiftClassType>();
            Protocols.AddRange(protocols.Where(p =>
            {
                if (p.IsProtocol)
                {
                    return true;
                }
                else
                {
                    throw new ArgumentOutOfRangeException("protocols", "protocols must contain only SwiftClassType with EntityKind protocol.");
                }
            }));
        }

        public SwiftProtocolListType(SwiftClassType protocol, bool isReference, SwiftName name = null)
            : base(CoreCompoundType.ProtocolList, isReference, name)
        {
            Protocols = new List<SwiftClassType>();
            if (!protocol.IsProtocol)
                throw new ArgumentOutOfRangeException($"Type {protocol.ClassName.ToFullyQualifiedName()} is not a protocol");
            Protocols.Add(protocol);
        }

        public List<SwiftClassType> Protocols { get; private set; }

        protected override bool LLEquals(SwiftType other)
        {
            var prot = other as SwiftProtocolListType;
            if (other == null)
                return false;
            if (Protocols.Count != prot.Protocols.Count)
                return false;
            return Protocols.SequenceEqual(prot.Protocols);
        }

        public override string ToString()
        {
            var name = Name != null ? Name.Name + ": " : "";
            return name + Protocols.Select(p => p.ToString()).InterleaveStrings(" & ");
        }
    }

    public class SwiftMetaClassType : SwiftType
    {
        public SwiftMetaClassType(SwiftClassType classType, bool isReference, SwiftName name = null)
            : base(CoreCompoundType.MetaClass, isReference, name)
        {
            Class = Exceptions.ThrowOnNull(classType, nameof(classType));
        }
        public SwiftMetaClassType(SwiftGenericArgReferenceType classGenericReference, bool isReference, SwiftName name = null)
            : base(CoreCompoundType.MetaClass, isReference, name)
        {
            ClassGenericReference = Exceptions.ThrowOnNull(classGenericReference, nameof(classGenericReference));
        }
        public SwiftClassType Class { get; private set; }
        public SwiftGenericArgReferenceType ClassGenericReference { get; private set; }
        protected override bool LLEquals(SwiftType other)
        {
            var meta = other as SwiftMetaClassType;
            if (meta == null)
                return false;
            if (Class != null)
                return Class.Equals(meta.Class);
            else
                return ClassGenericReference.Equals(meta.ClassGenericReference);
        }
        public override string ToString()
        {
            var name = Name != null ? Name.Name + ": " : "";
            return name + "Meta " + Class;
        }
    }

    public class SwiftExistentialMetaType : SwiftType
    {
        public SwiftExistentialMetaType(SwiftProtocolListType protocolList, bool isReference, SwiftName name = null)
            : base(CoreCompoundType.MetaClass, isReference, name)
        {
            Protocol = Exceptions.ThrowOnNull(protocolList, nameof(protocolList));
        }
        public SwiftProtocolListType Protocol { get; private set; }
        protected override bool LLEquals(SwiftType other)
        {
            var meta = other as SwiftExistentialMetaType;
            return meta != null && Protocol.Equals(meta.Protocol);
        }
        public bool IsAny { get { return Protocol.Protocols.Count == 0; } }
        public override string ToString()
        {
            var name = Name != null ? Name.Name + ": " : "";
            return name + "Existential Metatype " + Protocol;
        }
    }

    public class GenericArgument
    {
        public GenericArgument(int depth, int index)
        {
            Constraints = new List<SwiftType>();
            Depth = depth;
            Index = index;
        }
        public int Depth { get; set; }
        public int Index { get; set; }
        public List<SwiftType> Constraints { get; private set; }
        public bool IsProtocolConstrained()
        {
            if (Constraints.Count == 0)
                return false;
            foreach (SwiftType ty in Constraints)
            {
                var ct = ty as SwiftClassType;
                if (ct == null)
                    throw new NotSupportedException("Expected a class type, but got " + ty.GetType().Name);
                if (ct.EntityKind != MemberNesting.Protocol)
                    return false;
            }
            return true;
        }
        public bool IsClassConstrained()
        {
            if (Constraints.Count == 0)
                return false;
            foreach (SwiftType ty in Constraints)
            {
                var ct = ty as SwiftClassType;
                if (ct == null)
                    throw new NotSupportedException("Expected a class type, but got " + ty.GetType().Name);
                if (ct.EntityKind != MemberNesting.Protocol)
                    return true;
            }
            return false;
        }
    }


    public class SwiftUnboundGenericType : SwiftType
    {
        public SwiftUnboundGenericType(SwiftType dependentType, List<GenericArgument> parms, bool isReference, SwiftName name = null)
            : base(CoreCompoundType.UnboundGeneric, isReference, name)
        {
            DependentType = Exceptions.ThrowOnNull(dependentType, nameof(dependentType));
            Arguments = Exceptions.ThrowOnNull(parms, nameof(parms));
        }

        public SwiftType DependentType { get; private set; }
        public List<GenericArgument> Arguments { get; private set; }
        public override string ToString()
        {
            var name = Name != null ? Name.Name + ": " : "";
            var genArgs = Arguments.Select((arg) => $"({arg.Depth},{arg.Index})").InterleaveCommas();
            return $"{name}{DependentType.ToString()}<{genArgs}>";
        }
    }

    public class SwiftGenericArgReferenceType : SwiftType
    {
        public SwiftGenericArgReferenceType(int depth, int index, bool isReference, SwiftName name = null, List<string> associatedTypePath = null)
            : base(CoreCompoundType.GenericReference, isReference, name)
        {
            Depth = depth;
            Index = index;
            AssociatedTypePath = new List<string>();
            if (associatedTypePath != null)
                AssociatedTypePath.AddRange(associatedTypePath);
        }

        public int Depth { get; private set; }
        public int Index { get; private set; }
        public List<string> AssociatedTypePath { get; private set; }
        public bool HasAssociatedTypePath => AssociatedTypePath.Count > 0;

        protected override bool LLEquals(SwiftType other)
        {
            var art = other as SwiftGenericArgReferenceType;
            if (art == null)
                return false;
            if (Depth != art.Depth || Index != art.Index)
                return false;
            if (AssociatedTypePath.Count != art.AssociatedTypePath.Count)
                return false;
            return AssociatedTypePath.SequenceEqual(art.AssociatedTypePath);
        }

        public override string ToString()
        {
            var name = Name != null ? Name.Name + ": " : "";
            if (HasAssociatedTypePath)
            {
                var path = AssociatedTypePath.InterleaveStrings(".");
                return name + $"({Depth},{Index}){(char)('A' + Depth)}{Index}.{path}";
            }
            else
            {
                return name + $"({Depth},{Index})";
            }
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

