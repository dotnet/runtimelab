// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace System.Reflection.Emit.Experimental
{
    // This static helper class adds common entities to a Metadata Builder.
    internal static class MetadataHelper
    {
        internal static AssemblyReferenceHandle AddAssemblyReference(Assembly assembly, MetadataBuilder metadata)
        {
            AssemblyName assemblyName = assembly.GetName();

            if (assemblyName == null || assemblyName.Name == null)
            {
                throw new ArgumentException(nameof(assemblyName));
            }

            return AddAssemblyReference(metadata, assemblyName.Name, assemblyName.Version, assemblyName.CultureName, assemblyName.GetPublicKey(), (AssemblyFlags)assemblyName.Flags);
        }

#pragma warning disable SA1011 // Closing square brackets should be spaced correctly
        internal static AssemblyReferenceHandle AddAssemblyReference(MetadataBuilder metadata, string name, Version? version, string? culture, byte[]? publicKey, AssemblyFlags flags)
#pragma warning restore SA1011 // Closing square brackets should be spaced correctly
        {
            return metadata.AddAssemblyReference(
            name: metadata.GetOrAddString(name),
            version: version ?? new Version(0, 0, 0, 0),
            culture: (culture == null) ? default : metadata.GetOrAddString(value: culture),
            publicKeyOrToken: (publicKey == null) ? default : metadata.GetOrAddBlob(publicKey),
            flags: flags,
            hashValue: default); // not sure where to find hashValue.
        }

        internal static TypeDefinitionHandle AddTypeDef(TypeBuilder typeBuilder, MetadataBuilder metadata, int methodToken, int fieldToken, EntityHandle? baseType)
        {
            // Add type metadata
            return metadata.AddTypeDefinition(
                attributes: typeBuilder.UserTypeAttribute,
                (typeBuilder.Namespace == null) ? default : metadata.GetOrAddString(typeBuilder.Namespace),
                name: metadata.GetOrAddString(typeBuilder.Name),
                baseType: baseType == null ? default : (EntityHandle)baseType,
                fieldList: MetadataTokens.FieldDefinitionHandle(fieldToken),
                methodList: MetadataTokens.MethodDefinitionHandle(methodToken));
        }

        internal static TypeReferenceHandle AddTypeReference(MetadataBuilder metadata, Type type, EntityHandle parent)
        {
            return AddTypeReference(metadata, parent, type.Name, type.Namespace);
        }

        internal static TypeReferenceHandle AddTypeReference(MetadataBuilder metadata, EntityHandle parent, string name, string? nameSpace)
        {
            return metadata.AddTypeReference(
                parent,
                (nameSpace == null) ? default : metadata.GetOrAddString(nameSpace),
                metadata.GetOrAddString(name));
        }

        internal static MemberReferenceHandle AddConstructorReference(MetadataBuilder metadata, EntityHandle parent, MethodBase method, ModuleBuilder module)
        {
            var blob = SignatureHelper.MethodSignatureEncoder(method.GetParameters(), null, true, module);
            return metadata.AddMemberReference(
                parent,
                metadata.GetOrAddString(method.Name),
                metadata.GetOrAddBlob(blob));
        }

        internal static MethodDefinitionHandle AddMethodDefintion(MetadataBuilder metadata, MethodBuilder methodBuilder, ModuleBuilder module)
        {
            return metadata.AddMethodDefinition(
                methodBuilder.Attributes,
                MethodImplAttributes.IL,
                metadata.GetOrAddString(methodBuilder.Name),
                metadata.GetOrAddBlob(SignatureHelper.MethodSignatureEncoder(methodBuilder._parameters, methodBuilder._returnType, !methodBuilder.IsStatic, module)),
                -1,
                parameterList: default);
        }

        internal static FieldDefinitionHandle AddFieldDefinition(MetadataBuilder metadata, FieldBuilder fieldBuilder)
        {
            return metadata.AddFieldDefinition(fieldBuilder.Attributes, metadata.GetOrAddString(fieldBuilder.Name), metadata.GetOrAddBlob(fieldBuilder.FieldSignature));
        }
    }
}
