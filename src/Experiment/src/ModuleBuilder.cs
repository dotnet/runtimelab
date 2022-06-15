using System;
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
        private readonly Guid _s_guid;
        public override System.Reflection.Assembly Assembly { get; }
        public override string ScopeName { get; }

        internal ModuleBuilder(string name, Assembly assembly)
        {
         //Example random GUID - I still need to understand when, how and why to generate them.
        _s_guid= Guid.NewGuid();
        ScopeName = name;
            Assembly = assembly;
        }

        internal void AppendMetadata(MetadataBuilder metadataBuilder)
        {
            metadataBuilder.AddModule(
                generation: 0,
                metadataBuilder.GetOrAddString(ScopeName),
                metadataBuilder.GetOrAddGuid(_s_guid),//See above comment on GUID
                default(GuidHandle),
                default(GuidHandle));
        }

        public void CreateGlobalFunctions()
        {
            throw new NotImplementedException();
        }

        // For all these methods, return type will be changed to "System.Reflection.Emit.Experiment.x" once non-empty modules/assemblies are supported.
        public System.Reflection.Emit.EnumBuilder DefineEnum(string name, System.Reflection.TypeAttributes visibility, System.Type underlyingType) 
        { 
            throw new NotImplementedException(); 
        }

        public System.Reflection.Emit.MethodBuilder DefineGlobalMethod(string name, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? parameterTypes)
        {
            throw new NotImplementedException();
        }

        public System.Reflection.Emit.MethodBuilder DefineGlobalMethod(string name, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? requiredReturnTypeCustomModifiers, System.Type[]? optionalReturnTypeCustomModifiers, System.Type[]? parameterTypes, System.Type[][]? requiredParameterTypeCustomModifiers, System.Type[][]? optionalParameterTypeCustomModifiers)
        {
            throw new NotImplementedException();
        }

        public System.Reflection.Emit.MethodBuilder DefineGlobalMethod(string name, System.Reflection.MethodAttributes attributes, System.Type? returnType, System.Type[]? parameterTypes)
        {
            throw new NotImplementedException();
        }

        public System.Reflection.Emit.FieldBuilder DefineInitializedData(string name, byte[] data, System.Reflection.FieldAttributes attributes)
        {
            throw new NotImplementedException();
        }

        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute("P/Invoke marshalling may dynamically access members that could be trimmed.")]
        public System.Reflection.Emit.MethodBuilder DefinePInvokeMethod(string name, string dllName, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? parameterTypes, System.Runtime.InteropServices.CallingConvention nativeCallConv, System.Runtime.InteropServices.CharSet nativeCharSet)
        {
            throw new NotImplementedException();
        }

        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute("P/Invoke marshalling may dynamically access members that could be trimmed.")]
        public System.Reflection.Emit.MethodBuilder DefinePInvokeMethod(string name, string dllName, string entryName, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? parameterTypes, System.Runtime.InteropServices.CallingConvention nativeCallConv, System.Runtime.InteropServices.CharSet nativeCharSet)
        {
            throw new NotImplementedException();
        }

        public System.Reflection.Emit.TypeBuilder DefineType(string name)
        {
            throw new NotImplementedException();
        }

        public System.Reflection.Emit.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr)
        {
            throw new NotImplementedException();
        }

        public System.Reflection.Emit.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent)
        {
            throw new NotImplementedException();
        }

        public System.Reflection.Emit.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent, int typesize)
        {
            throw new NotImplementedException();
        }

        public System.Reflection.Emit.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent, System.Reflection.Emit.PackingSize packsize)
        {
            throw new NotImplementedException();
        }

        public System.Reflection.Emit.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent, System.Reflection.Emit.PackingSize packingSize, int typesize)
        {
            throw new NotImplementedException();
        }

        public System.Reflection.Emit.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent, System.Type[]? interfaces)
        {
            throw new NotImplementedException();
        }

        public System.Reflection.Emit.FieldBuilder DefineUninitializedData(string name, int size, System.Reflection.FieldAttributes attributes)
        {
            throw new NotImplementedException();
        }

        public System.Reflection.MethodInfo GetArrayMethod(System.Type arrayClass, string methodName, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? parameterTypes)
        {
            throw new NotImplementedException();
        }

        public void SetCustomAttribute(System.Reflection.ConstructorInfo con, byte[] binaryAttribute)
        {
            throw new NotImplementedException();
        }

        public void SetCustomAttribute(System.Reflection.Emit.CustomAttributeBuilder customBuilder)
        {
            throw new NotImplementedException();
        }



    }
}
