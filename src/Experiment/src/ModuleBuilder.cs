// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace System.Reflection.Emit.Experimental
{
    public class ModuleBuilder : System.Reflection.Module
    {
        /* According to ECMA-335, (II.22.37), a TypeDef refrences the row of its first method in the MethodDef table. It owns all methods that lie between 
         * that row and the row refrenced by the next TypeDef.
         * For a TypeA that has no methods followed by a TypeB that has methods, MetaDataBuilder indicates this by having TypeA refrence the row of the first method of TypeB. Since TypeB
         * has the same row refrence, TypeA will have no methods. (Why it needs to do this is unclear to me, since according to ECMA, ibid. 18, MethodList can be null).
         * See https://docs.microsoft.com/en-us/dotnet/api/system.reflection.metadata.ecma335.metadatabuilder.addtypedefinition?view=net-6.0#system-reflection-metadata-ecma335-metadatabuilder-addtypedefinition(system-reflection-typeattributes-system-reflection-metadata-stringhandle-system-reflection-metadata-stringhandle-system-reflection-metadata-entityhandle-system-reflection-metadata-fielddefinitionhandle-system-reflection-metadata-methoddefinitionhandle)
         * Using a LinkedList allows Types to inspect later Types when needed. 
         * Identical issue for fields when implemented.
         */
        internal LinkedList<TypeBuilder> _typeStorage = new LinkedList<TypeBuilder>(); 
        public override System.Reflection.Assembly Assembly { get; }
        public override string ScopeName
        {
            get;
        }

        internal ModuleBuilder(string name, Assembly assembly)
        {
         //Example random GUID - I still need to understand when, how and why to generate them.
        _s_guid= Guid.NewGuid();
        ScopeName = name;
            Assembly = assembly;
        }

        internal void AppendMetadata(MetadataBuilder _metadata)
        {
            MethodDefinitionHandle? _handle = null;
            //Generate underlying metadata
            foreach (TypeBuilder entry in _typeStorage)
            {
                entry.GenerateComponentMetadata(_metadata);
            }

            //Get first method handle
            foreach (TypeBuilder entry in _typeStorage)
            {
                if(entry._first!=null)
                {
                    _handle= entry._first;
                    break;
                }
            }
            // Create type definition for the special <Module> type that holds global functions
            _metadata.AddTypeDefinition(
                default(TypeAttributes),
                default(StringHandle),
                _metadata.GetOrAddString("<Module>"),
                baseType: default(EntityHandle),
                fieldList: MetadataTokens.FieldDefinitionHandle(1),
                methodList: (MethodDefinitionHandle)((_handle != null) ? _handle : MetadataTokens.MethodDefinitionHandle(1)));
            
            //Add each type's metadata
            foreach (TypeBuilder entry in _typeStorage)
            {
                entry.AppendMetadata(_metadata);
            }

            //Add module metadata
            _metadata.AddModule(
                generation: 0,
                _metadata.GetOrAddString(ScopeName),
                _metadata.GetOrAddGuid(Guid.NewGuid()),
                default(GuidHandle),
                default(GuidHandle));
        }

        public System.Reflection.Emit.Experimental.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr)
        {
            TypeBuilder _type = new TypeBuilder(name,this,Assembly,attr);
            _typeStorage.AddLast(_type);  
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
