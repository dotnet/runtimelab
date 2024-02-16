// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SwiftRuntimeLibrary;

namespace SwiftReflector.Demangling
{
    public class TLDefinition
    {
        protected TLDefinition(CoreCompoundType type, string mangledName, SwiftName module, ulong offset)
        {
            if (module == null)
                throw new ArgumentNullException(nameof(module));
            Type = type;
            Module = module;
            MangledName = Exceptions.ThrowOnNull(mangledName, nameof(mangledName));
            Offset = offset;
        }

        public CoreCompoundType Type { get; private set; }
        public SwiftName Module { get; private set; }
        public string MangledName { get; private set; }
        public ulong Offset { get; private set; }
    }

    public class TLModuleDescriptor : TLDefinition
    {
        public TLModuleDescriptor(string mangledName, SwiftName module, ulong offset)
            : base(CoreCompoundType.ModuleDescriptor, mangledName, module, offset)
        {
        }
    }

    public class TLMetadataDescriptor : TLDefinition
    {
        public TLMetadataDescriptor(SwiftType ofType, bool isBuiltIn, string mangledName, SwiftName module, ulong offset)
            : base(CoreCompoundType.MetadataDescriptor, mangledName, module, offset)
        {
            OfType = Exceptions.ThrowOnNull(ofType, nameof(ofType));
            IsBuiltIn = isBuiltIn;
        }
        public SwiftType OfType { get; private set; }
        public bool IsBuiltIn { get; private set; }
    }

    public class TLClassElem : TLDefinition
    {
        protected TLClassElem(CoreCompoundType type, string mangledName, SwiftName module, SwiftClassType classType, ulong offset)
            : base(type, mangledName, module, offset)
        {
            Class = classType;
        }
        public SwiftClassType Class { get; private set; }
    }

    public class TLThunk : TLClassElem
    {
        public TLThunk(ThunkType thunkType, string mangledName, SwiftName module, SwiftClassType classType, ulong offset)
            : base(CoreCompoundType.Thunk, mangledName, module, classType, offset)
        {
            Thunk = thunkType;
        }

        public ThunkType Thunk { get; private set; }
    }

    public class TLVariable : TLClassElem
    {
        public TLVariable(string mangledName, SwiftName module, SwiftClassType classType, SwiftName ident, SwiftType ofType,
                bool isStatic, ulong offset, SwiftType extensionOn = null)
            : this(CoreCompoundType.Variable, mangledName, module, classType, ident, ofType, isStatic, offset, extensionOn)
        {
        }

        protected TLVariable(CoreCompoundType type, string mangledName, SwiftName module, SwiftClassType classType, SwiftName ident, SwiftType ofType,
                bool isStatic, ulong offset, SwiftType extensionOn)
            : base(type, mangledName, module, classType, offset)
        {
            Name = Exceptions.ThrowOnNull(ident, nameof(ident));
            OfType = Exceptions.ThrowOnNull(ofType, nameof(ofType));
            IsStatic = isStatic;
            ExtensionOn = extensionOn;
        }
        public SwiftType OfType { get; private set; }
        public SwiftType ExtensionOn { get; private set; }
        public SwiftName Name { get; private set; }
        public bool IsStatic { get; private set; }
    }

    public class TLPropertyDescriptor : TLVariable
    {
        public TLPropertyDescriptor(string mangledName, SwiftName module, SwiftClassType classType, SwiftName ident, SwiftType ofType,
                bool isStatic, ulong offset, SwiftType extensionOn = null)
            : base(CoreCompoundType.PropertyDescriptor, mangledName, module, classType, ident, ofType, isStatic, offset, extensionOn)
        {
        }
    }

    public class TLUnsafeMutableAddressor : TLClassElem
    {
        public TLUnsafeMutableAddressor(string mangledName, SwiftName module, SwiftClassType classType, SwiftName ident, SwiftType ofType, ulong offset)
            : base(CoreCompoundType.UnsafeMutableAddressor, mangledName, module, classType, offset)
        {
            Name = Exceptions.ThrowOnNull(ident, nameof(ident));
            OfType = Exceptions.ThrowOnNull(ofType, nameof(ofType));
        }
        public SwiftType OfType { get; private set; }
        public SwiftName Name { get; private set; }
    }

    public class TLFieldOffset : TLClassElem
    {

        public TLFieldOffset(string mangledName, SwiftName module, SwiftClassType classType, bool direct, SwiftName ident, SwiftType type, ulong offset)
            : base(CoreCompoundType.FieldOffset, mangledName, module, classType, offset)
        {
            IsDirect = direct;
            Identifier = ident;
            FieldType = type;
        }

        public bool IsDirect { get; private set; }

        public SwiftName Identifier { get; private set; }
        public SwiftType FieldType { get; private set; }
    }

    public class TLFunction : TLClassElem
    {
        public TLFunction(string mangledName, SwiftName module, SwiftName functionName,
            SwiftClassType classType, SwiftBaseFunctionType signature, ulong offset, OperatorType oper = OperatorType.None)
            : this(mangledName, module, functionName, classType, signature, offset, oper, CoreCompoundType.Function)
        {
        }

        protected TLFunction(string mangledName, SwiftName module, SwiftName functionName,
            SwiftClassType classType, SwiftBaseFunctionType signature, ulong offset, OperatorType oper = OperatorType.None,
                CoreCompoundType type = CoreCompoundType.Function)
                : base(type, mangledName, module, classType, offset)
        {
            Name = functionName;
            Signature = signature;
            Operator = oper;
        }

        public SwiftName Name { get; private set; }
        public SwiftBaseFunctionType Signature { get; private set; }
        public OperatorType Operator { get; private set; }

        public bool IsTopLevelFunction { get { return Class == null || Class.ClassName.Nesting.Count == 0; } }
    }

    public class TLMethodDescriptor : TLFunction
    {
        public TLMethodDescriptor(string mangledName, SwiftName module, SwiftName functionName,
            SwiftClassType classType, SwiftBaseFunctionType signature, ulong offset, OperatorType oper = OperatorType.None)
            : base(mangledName, module, functionName, classType, signature, offset, oper, CoreCompoundType.MethodDescriptor)
        {
        }
    }

    public class TLEnumCase : TLFunction
    {
        public TLEnumCase(string mangledName, SwiftName module, SwiftName functionName,
            SwiftClassType classType, SwiftBaseFunctionType signature, ulong offset, OperatorType oper = OperatorType.None)
            : base(mangledName, module, functionName, classType, signature, offset, oper, CoreCompoundType.MethodDescriptor)
        {
        }
    }

    public class TLDefaultArgumentInitializer : TLDefinition
    {
        public TLDefaultArgumentInitializer(string mangledName, SwiftName module, SwiftBaseFunctionType function, int index, ulong offset)
            : base(CoreCompoundType.ArgumentInitializer, mangledName, module, offset)
        {
            Signature = Exceptions.ThrowOnNull(function, nameof(function));
            ArgumentIndex = index;
        }
        public SwiftBaseFunctionType Signature { get; private set; }
        public int ArgumentIndex { get; private set; }
    }

    public class TLLazyCacheVariable : TLClassElem
    {
        public TLLazyCacheVariable(string mangledName, SwiftName module, SwiftClassType cl, ulong offset)
            : base(CoreCompoundType.LazyCache, mangledName, module, cl, offset)
        {
        }
    }

    public class TLDirectMetadata : TLClassElem
    {
        public TLDirectMetadata(string mangledName, SwiftName module, SwiftClassType cl, ulong offset)
            : base(CoreCompoundType.DirectMetadata, mangledName, module, cl, offset)
        {
        }
    }

    public class TLGenericMetadataPattern : TLClassElem
    {
        public TLGenericMetadataPattern(string mangledName, SwiftName module, SwiftClassType cl, ulong offset)
            : base(CoreCompoundType.DirectMetadata, mangledName, module, cl, offset)
        {
        }
    }

    public class TLMetaclass : TLClassElem
    {
        public TLMetaclass(string mangledName, SwiftName module, SwiftClassType cl, ulong offset)
            : base(CoreCompoundType.MetaClass, mangledName, module, cl, offset)
        {
        }
    }

    public class TLNominalTypeDescriptor : TLClassElem
    {
        public TLNominalTypeDescriptor(string mangledName, SwiftName module, SwiftClassType cl, ulong offset)
            : base(CoreCompoundType.NominalTypeDescriptor, mangledName, module, cl, offset)
        {
        }
    }

    public class TLProtocolTypeDescriptor : TLClassElem
    {
        public TLProtocolTypeDescriptor(string mangledName, SwiftName module, SwiftClassType cl, ulong offset)
            : base(CoreCompoundType.ProtocolTypeDescriptor, mangledName, module, cl, offset)
        {
        }
    }

    public class TLProtocolConformanceDescriptor : TLDefinition
    {
        public TLProtocolConformanceDescriptor(string mangledName, SwiftName module, SwiftType implementingType,
            SwiftClassType forProtocol, ulong offset)
            : base(CoreCompoundType.ProtocolConformanceDescriptor, mangledName, module, offset)
        {
            ImplementingType = Exceptions.ThrowOnNull(implementingType, nameof(implementingType));
            Protocol = forProtocol;
        }

        public SwiftType ImplementingType { get; private set; }
        public SwiftClassType Protocol { get; private set; }
    }

    public class TLProtocolRequirementsBaseDescriptor : TLClassElem
    {
        public TLProtocolRequirementsBaseDescriptor(string mangledName, SwiftName module, SwiftClassType cl, ulong offset)
            : base(CoreCompoundType.ProtocolRequirementsBaseDescriptor, mangledName, module, cl, offset)
        {
        }
    }

    public class TLBaseConformanceDescriptor : TLClassElem
    {
        public TLBaseConformanceDescriptor(string mangledName, SwiftName module, SwiftClassType protocol, SwiftClassType requirement, ulong offset)
            : base(CoreCompoundType.BaseConformanceDescriptor, mangledName, module, protocol, offset)
        {
            ProtocolRequirement = Exceptions.ThrowOnNull(requirement, nameof(requirement));
        }
        public SwiftClassType ProtocolRequirement { get; private set; }
    }

    public class TLAssociatedTypeDescriptor : TLClassElem
    {
        public TLAssociatedTypeDescriptor(string mangledName, SwiftName module, SwiftClassType protocol, SwiftName associatedTypeName, ulong offset)
            : base(CoreCompoundType.AssociatedTypeDescriptor, mangledName, module, protocol, offset)
        {
            AssociatedTypeName = associatedTypeName;
        }

        public SwiftName AssociatedTypeName { get; private set; }
    }

    public class TLMetadataBaseOffset : TLClassElem
    {
        public TLMetadataBaseOffset(string mangledName, SwiftName module, SwiftClassType classType, ulong offset)
            : base(CoreCompoundType.MetadataOffset, mangledName, module, classType, offset)
        {
        }
    }

    public class TLMethodLookupFunction : TLClassElem
    {
        public TLMethodLookupFunction(string mangledName, SwiftName module, SwiftClassType classType, ulong offset)
            : base(CoreCompoundType.MethodLookupFunction, mangledName, module, classType, offset)
        {

        }
    }
}

