// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using static System.Reflection.Emit.Experimental.EntityWrappers;

namespace System.Reflection.Emit.Experimental
{
    public class ModuleBuilder : System.Reflection.Module
    {
        internal List<AssemmblyReferenceWrapper> _assemblyRefStore = new List<AssemmblyReferenceWrapper>();
        internal int _nextAssemblyRefRowId = 1;

        internal List<TypeReferenceWrapper> _typeRefStore = new List<TypeReferenceWrapper>();
        internal int _nextTypeRefRowId = 1;

        internal List<MethodReferenceWrapper> _methodRefStore = new List<MethodReferenceWrapper>();
        internal int _nextMethodRefRowId = 1;
        

        internal List<TypeBuilder> _typeDefStore = new List<TypeBuilder>();
        internal int _nextMethodDefRowId = 1;

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
            //Add module metadata
            metadata.AddModule(
                generation: 0,
                metadata.GetOrAddString(ScopeName),
                metadata.GetOrAddGuid(Guid.NewGuid()),
                default(GuidHandle),
                default(GuidHandle));

            // Create type definition for the special <Module> type that holds global functions
            metadata.AddTypeDefinition(
                default(TypeAttributes),
                default(StringHandle),
                metadata.GetOrAddString("<Module>"),
                baseType: default(EntityHandle),
                fieldList: MetadataTokens.FieldDefinitionHandle(1),
                methodList: MetadataTokens.MethodDefinitionHandle(1));

            // Add each assembly reference to metadata table.
            foreach (var assemblyRef in _assemblyRefStore)
            {
                MetadataHelper.AddAssemblyReference(assemblyRef.assembly, metadata);
            }

            // Add each type reference to metadata table.
            foreach (var typeReference in _typeRefStore)
            {
                AssemblyReferenceHandle parent = MetadataTokens.AssemblyReferenceHandle(typeReference.parentToken);
                MetadataHelper.AddTypeReference(metadata, typeReference.type, parent);
            }

            // Add each method reference to metadta table.
            foreach (var methodRef in _methodRefStore)
            {
                TypeReferenceHandle parent = MetadataTokens.TypeReferenceHandle(methodRef.parentToken);
                MetadataHelper.AddConstructorReference(metadata, parent, methodRef.method);
            }

            //Add each type defintion to metadata table.
            foreach (TypeBuilder typeBuilder in _typeDefStore)
            {
                TypeDefinitionHandle typeDefintionHandle =  MetadataHelper.addTypeDef(typeBuilder,metadata, _nextMethodDefRowId);
                //Add each method defintion to metadata table.
                foreach (MethodBuilder method in typeBuilder._methodDefStore)
                {
                    MetadataHelper.AddMethodDefintion(metadata, method);
                    _nextMethodDefRowId++;
                }

                //Add each custom attribute to metadata table.
                foreach (EntityWrappers.CustomAttribute customAttribute in typeBuilder._customAttributes )
                {
                    MemberReferenceHandle constructorHandle = MetadataTokens.MemberReferenceHandle(customAttribute.conToken);
                    metadata.AddCustomAttribute(typeDefintionHandle, constructorHandle, metadata.GetOrAddBlob(customAttribute.binaryAttribute));
                }
            }
        }

        public System.Reflection.Emit.Experimental.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr)
        {
            TypeBuilder _type = new TypeBuilder(name, this, Assembly, attr);
            _typeDefStore.Add(_type);
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
