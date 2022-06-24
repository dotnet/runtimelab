// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace System.Reflection.Emit.Experimental
{
    public class ModuleBuilder : System.Reflection.Module
    {
        internal List<TypeBuilder> _typeStorage = new List<TypeBuilder>();
        internal int _nextMethodDefRowId = 1; // Store method count for use in Type defintion.
        public override System.Reflection.Assembly Assembly { get; }
        public override string ScopeName
        {
            get;
        }

        internal ModuleBuilder(string name, Assembly assembly)
        {
            ScopeName = name;
            Assembly = assembly;
        }

        internal void AppendMetadata(MetadataBuilder metadata)
        {
            // Create type definition for the special <Module> type that holds global functions
            metadata.AddTypeDefinition(
                default(TypeAttributes),
                default(StringHandle),
                metadata.GetOrAddString("<Module>"),
                baseType: default(EntityHandle),
                fieldList: MetadataTokens.FieldDefinitionHandle(1),
                methodList: MetadataTokens.MethodDefinitionHandle(1));

            //Add each type's metadata
            foreach (TypeBuilder entry in _typeStorage)
            {
                entry.AppendMetadata(metadata);
            }

            //Add module metadata
            metadata.AddModule(
                generation: 0,
                metadata.GetOrAddString(ScopeName),
                metadata.GetOrAddGuid(Guid.NewGuid()),
                default(GuidHandle),
                default(GuidHandle));
        }

        public System.Reflection.Emit.Experimental.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr)
        {
            TypeBuilder _type = new TypeBuilder(name, this, Assembly, attr);
            _typeStorage.Add(_type);
            return _type;
        }

        public void CreateGlobalFunctions()
           => throw new NotImplementedException();

        // For all these methods, return type will be changed to "System.Reflection.Emit.Experiment.x" once non-empty modules/assemblies are supported.
        public System.Reflection.Emit.EnumBuilder DefineEnum(string name, System.Reflection.TypeAttributes visibility, System.Type underlyingType)
            => throw new NotImplementedException();

        public System.Reflection.Emit.MethodBuilder DefineGlobalMethod(string name, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? parameterTypes)
            => throw new NotImplementedException();

        public System.Reflection.Emit.MethodBuilder DefineGlobalMethod(string name, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? requiredReturnTypeCustomModifiers, System.Type[]? optionalReturnTypeCustomModifiers, System.Type[]? parameterTypes, System.Type[][]? requiredParameterTypeCustomModifiers, System.Type[][]? optionalParameterTypeCustomModifiers)
            => throw new NotImplementedException();

        public System.Reflection.Emit.MethodBuilder DefineGlobalMethod(string name, System.Reflection.MethodAttributes attributes, System.Type? returnType, System.Type[]? parameterTypes)
           => throw new NotImplementedException();

        public System.Reflection.Emit.FieldBuilder DefineInitializedData(string name, byte[] data, System.Reflection.FieldAttributes attributes)
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute("P/Invoke marshalling may dynamically access members that could be trimmed.")]
        public System.Reflection.Emit.MethodBuilder DefinePInvokeMethod(string name, string dllName, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? parameterTypes, System.Runtime.InteropServices.CallingConvention nativeCallConv, System.Runtime.InteropServices.CharSet nativeCharSet)
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute("P/Invoke marshalling may dynamically access members that could be trimmed.")]
        public System.Reflection.Emit.MethodBuilder DefinePInvokeMethod(string name, string dllName, string entryName, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? parameterTypes, System.Runtime.InteropServices.CallingConvention nativeCallConv, System.Runtime.InteropServices.CharSet nativeCharSet)
            => throw new NotImplementedException();

        public System.Reflection.Emit.TypeBuilder DefineType(string name)
            => throw new NotImplementedException();

        public System.Reflection.Emit.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent)
            => throw new NotImplementedException();

        public System.Reflection.Emit.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent, int typesize)
            => throw new NotImplementedException();

        public System.Reflection.Emit.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent, System.Reflection.Emit.PackingSize packsize)
            => throw new NotImplementedException();

        public System.Reflection.Emit.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent, System.Reflection.Emit.PackingSize packingSize, int typesize)
           => throw new NotImplementedException();

        public System.Reflection.Emit.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent, System.Type[]? interfaces)
            => throw new NotImplementedException();

        public System.Reflection.Emit.FieldBuilder DefineUninitializedData(string name, int size, System.Reflection.FieldAttributes attributes)
            => throw new NotImplementedException();

        public System.Reflection.MethodInfo GetArrayMethod(System.Type arrayClass, string methodName, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? parameterTypes)
            => throw new NotImplementedException();

        public void SetCustomAttribute(System.Reflection.ConstructorInfo con, byte[] binaryAttribute)
           => throw new NotImplementedException();

        public void SetCustomAttribute(System.Reflection.Emit.CustomAttributeBuilder customBuilder)
            => throw new NotImplementedException();

    }
}
