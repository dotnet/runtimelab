using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace System.Reflection.Emit.Experimental
{
    internal class ReferenceTool
    {
        internal static  AssemblyReferenceHandle AddAssemblyReference(MetadataBuilder metadata, Assembly assembly)
        {
            AssemblyName assemblyName = assembly.GetName();
            if(assemblyName==null|| assemblyName.Name==null)
            {
                throw new ArgumentNullException(nameof(assemblyName));
            }
            return ReferenceTool.AddAssemblyReference(metadata, assemblyName.Name, assemblyName.Version, assemblyName.CultureName, assemblyName.GetPublicKey(), (AssemblyFlags) assemblyName.Flags);
        }

        internal static AssemblyReferenceHandle AddAssemblyReference(MetadataBuilder metadata,string name, Version? version, string? culture, byte[]? publicKey, AssemblyFlags flags)
        {
            Debug.WriteLine($"Searching for the {name} assembly");
            return metadata.AddAssemblyReference(
            name: metadata.GetOrAddString(name),
            version: version ?? new Version(0, 0, 0, 0),
            culture: (culture == null) ? default : metadata.GetOrAddString(value: culture),
            publicKeyOrToken: (publicKey == null) ? default : metadata.GetOrAddBlob(publicKey),
            flags: flags,
            hashValue: default); // not sure where to find hashValue.
        }

        internal static TypeReferenceHandle AddTypeReference(MetadataBuilder metadata, Type type, AssemblyReferenceHandle parent)
        {
            return ReferenceTool.AddTypeReference(metadata, parent, type.Name, type.Namespace);
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

        internal static MemberReferenceHandle AddMethodReference(MetadataBuilder metadata, TypeReferenceHandle parent, string name, BlobHandle signature)
        {
            Debug.WriteLine($"Searching for the {name} method");
            return metadata.AddMemberReference(
            parent,
            metadata.GetOrAddString(name),
            signature);
        }

        internal static BlobHandle ConstructorSignatureEnconder(MetadataBuilder metadata, ConstructorInfo constructorInfo)
        {
            var CtorSignature = new BlobBuilder();
            ParametersEncoder _parEncoder;
            ReturnTypeEncoder _retEncoder;
            new BlobEncoder(CtorSignature).
                MethodSignature(isInstanceMethod: true).
                Parameters(constructorInfo.GetParameters().Length, out _retEncoder, out _parEncoder);
            //.Ctor always returns void
            _retEncoder.Void();
            if (constructorInfo.GetParameters().Length > 0)
            {
                foreach (var parameter in constructorInfo.GetParameters())
                {
                    if (parameter.ParameterType != null)
                    {
                        MapReflectionTypeToSignatureType(_parEncoder.AddParameter().Type(), parameter.ParameterType);
                    }
                }
            }
            else
            {
                Debug.WriteLine("My custom attr constructor has no parameters");
            }

            return metadata.GetOrAddBlob(CtorSignature);
        }

        internal static void MapReflectionTypeToSignatureType(SignatureTypeEncoder signature, Type type)
        {
            // We need to translate from Reflection.Type to SignatureTypeEncoder. Most common types for proof of concept. More types will be added.
            switch (type.FullName)
            {
                case "System.Boolean":
                    signature.Boolean();
                    break;
                case "System.Byte":
                    signature.Byte();
                    break;
                case "System.Char":
                    signature.Char();
                    break;
                case "System.Double":
                    signature.Double();
                    break;
                case "System.Int32":
                    signature.Int32();
                    break;
                case "System.Int64":
                    signature.Int64();
                    break;
                case "System.Object":
                    signature.Object();
                    break;
                case "System.String":
                    signature.String();
                    break;
                default: throw new NotImplementedException("This parameter type is not yet supported: " + type.FullName);
            }
        }
    }
}
