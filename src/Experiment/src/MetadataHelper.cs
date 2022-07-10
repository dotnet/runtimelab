using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace System.Reflection.Emit.Experimental
{
    internal static class MetadataHelper
    {
        internal static AssemblyReferenceHandle AddAssemblyReference(Assembly assembly, MetadataBuilder metadata)
        {
            AssemblyName assemblyName = assembly.GetName();
            if (assemblyName == null || assemblyName.Name == null)
            {
                throw new ArgumentNullException(nameof(assemblyName));
            }
            return AddAssemblyReference(metadata, assemblyName.Name, assemblyName.Version, assemblyName.CultureName, assemblyName.GetPublicKey(), (AssemblyFlags)assemblyName.Flags);
        }

        internal static AssemblyReferenceHandle AddAssemblyReference(MetadataBuilder metadata, string name, Version? version, string? culture, byte[]? publicKey, AssemblyFlags flags)
        {
            Debug.WriteLine($"Searching for the {name} assembly");
            return  metadata.AddAssemblyReference(
            name: metadata.GetOrAddString(name),
            version: version ?? new Version(0, 0, 0, 0),
            culture: (culture == null) ? default : metadata.GetOrAddString(value: culture),
            publicKeyOrToken: (publicKey == null) ? default : metadata.GetOrAddBlob(publicKey),
            flags: flags,
            hashValue: default); // not sure where to find hashValue.
        }

        internal static TypeDefinitionHandle addTypeDef(TypeBuilder typeBuilder, MetadataBuilder metadata, int methodToken)
        {
            //Add type metadata
            return metadata.AddTypeDefinition(
                attributes: typeBuilder.UserTypeAttribute,
                (typeBuilder.Namespace == null) ? default : metadata.GetOrAddString(typeBuilder.Namespace),
                name: metadata.GetOrAddString(typeBuilder.Name),
                baseType: default,//Inheritance to be added
                fieldList: MetadataTokens.FieldDefinitionHandle(1),//Update once we support fields.
                methodList: MetadataTokens.MethodDefinitionHandle(methodToken)); // Manuallly add handle to correct row
        }

        internal static TypeReferenceHandle AddTypeReference(MetadataBuilder metadata, Type type, AssemblyReferenceHandle parent)
        {
             return AddTypeReference(metadata, parent, type.Name, type.Namespace);
        }

        internal static TypeReferenceHandle AddTypeReference(MetadataBuilder metadata, AssemblyReferenceHandle parent, string name, string? nameSpace)
        {
            Debug.WriteLine($"Searching for the {nameSpace}.{name} type");
            return metadata.AddTypeReference(
             parent,
             (nameSpace == null) ? default : metadata.GetOrAddString(nameSpace),
             metadata.GetOrAddString(name)
             );
        }

        internal static MemberReferenceHandle AddConstructorReference(MetadataBuilder metadata, TypeReferenceHandle parent, MethodBase method)
        {
            var blob = SignatureHelper.MethodSignatureEnconder(method.GetParameters(), null, true);
            return metadata.AddMemberReference(
            parent,
            metadata.GetOrAddString(method.Name),
            metadata.GetOrAddBlob(blob));
        }

        internal static MethodDefinitionHandle AddMethodDefintion(MetadataBuilder metadata, MethodBuilder methodBuilder)
        {
            return metadata.AddMethodDefinition(
                methodBuilder.Attributes,
                MethodImplAttributes.IL,
               metadata.GetOrAddString(methodBuilder.Name),
               metadata.GetOrAddBlob(SignatureHelper.MethodSignatureEnconder(methodBuilder._parameters,methodBuilder._returnType, !methodBuilder.IsStatic)),
               -1, //No body supported
               parameterList: default);
        }
    }
}
