using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;

namespace DllImportGenerator.Tools
{
    /// <summary>
    /// Information about return/argument type in a P/Invoke
    /// </summary>
    public class ParameterInfo
    {
        public enum IndirectKind
        {
            None,
            Array,
            Pointer,
            ByRef,
            FunctionPointer
        }

        public IndirectKind Indirection { get; init; }
        public string Name { get; init; }
        public ParameterInfo Element { get; init; }
        public string AssemblyName { get; init; }
        public MarshalAsAttribute MarshalAsInfo { get; set; }

        public ParameterInfo(IndirectKind indirection, ParameterInfo element)
        {
            Debug.Assert(indirection != IndirectKind.None && indirection != IndirectKind.FunctionPointer);
            Debug.Assert(element != null);

            Indirection = indirection;
            Element = element;
            Name = element.Name;
        }

        public ParameterInfo(IndirectKind indirection, string name)
        {
            Debug.Assert(indirection == IndirectKind.None || indirection == IndirectKind.FunctionPointer);

            Indirection = indirection;
            Name = name;
        }

        public override string ToString()
        {
            string result = string.Empty;
            ParameterInfo currType = this;
            while (currType.Indirection != IndirectKind.None && currType.Indirection != IndirectKind.FunctionPointer)
            {
                Debug.Assert(currType.Element != null);
                string modifier = currType.Indirection switch
                {
                    IndirectKind.Array => "[]",
                    IndirectKind.Pointer => "*",
                    IndirectKind.ByRef => "&",
                    _ => "",
                };
                result = $"{modifier}{result}";
                currType = currType.Element;
            }

            result = $"{currType.Name}{result}";
            return MarshalAsInfo == null
                ? result
                : $"{result} marshal({MarshalAsInfo.Value})";
        }
    }

    public sealed partial class PInvokeDump
    {
        public class NotSupportedTypeException : Exception
        {
            public string Type { get; init; }
            public NotSupportedTypeException(string type) { this.Type = type; }
        }

        private class UnusedGenericContext { }

        /// <summary>
        /// Simple type provider for decoding a method signature
        /// </summary>
        private class TypeProvider : ISignatureTypeProvider<ParameterInfo, UnusedGenericContext>
        {
            public ParameterInfo GetArrayType(ParameterInfo elementType, ArrayShape shape)
            {
                throw new NotSupportedTypeException($"Array ({elementType.Name}) - {shape}");
            }

            public ParameterInfo GetByReferenceType(ParameterInfo elementType)
            {
                return new ParameterInfo(ParameterInfo.IndirectKind.ByRef, elementType);
            }

            public ParameterInfo GetFunctionPointerType(MethodSignature<ParameterInfo> signature)
            {
                return new ParameterInfo(
                    ParameterInfo.IndirectKind.FunctionPointer,
                    $"method {signature.ReturnType} ({string.Join(", ", signature.ParameterTypes.Select(t => t.ToString()))})");
            }

            public ParameterInfo GetGenericInstantiation(ParameterInfo genericType, ImmutableArray<ParameterInfo> typeArguments)
            {
                throw new NotSupportedTypeException($"Generic ({genericType.Name})");
            }

            public ParameterInfo GetGenericMethodParameter(UnusedGenericContext genericContext, int index)
            {
                throw new NotSupportedTypeException($"Generic - {index}");
            }

            public ParameterInfo GetGenericTypeParameter(UnusedGenericContext genericContext, int index)
            {
                throw new NotSupportedTypeException($"Generic - {index}");
            }

            public ParameterInfo GetModifiedType(ParameterInfo modifier, ParameterInfo unmodifiedType, bool isRequired)
            {
                throw new NotSupportedTypeException($"Modified ({unmodifiedType.Name})");
            }

            public ParameterInfo GetPinnedType(ParameterInfo elementType)
            {
                throw new NotSupportedTypeException($"Pinned ({elementType.Name})");
            }

            public ParameterInfo GetPointerType(ParameterInfo elementType)
            {
                return new ParameterInfo(ParameterInfo.IndirectKind.Pointer, elementType);
            }

            public ParameterInfo GetPrimitiveType(PrimitiveTypeCode typeCode)
            {
                return new ParameterInfo(ParameterInfo.IndirectKind.None, typeCode.ToString());
            }

            public ParameterInfo GetSZArrayType(ParameterInfo elementType)
            {
                return new ParameterInfo(ParameterInfo.IndirectKind.Array, elementType);
            }

            public ParameterInfo GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                TypeDefinition typeDef = reader.GetTypeDefinition(handle);

                string name = GetTypeDefinitionFullName(reader, typeDef);
                return new ParameterInfo(ParameterInfo.IndirectKind.None, name)
                {
                    AssemblyName = reader.GetString(reader.GetAssemblyDefinition().Name),
                };
            }

            public ParameterInfo GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                TypeReference typeRef = reader.GetTypeReference(handle);
                Handle scope = typeRef.ResolutionScope;

                string name = typeRef.Namespace.IsNil
                    ? reader.GetString(typeRef.Name)
                    : reader.GetString(typeRef.Namespace) + Type.Delimiter + reader.GetString(typeRef.Name);

                switch (scope.Kind)
                {
                    case HandleKind.AssemblyReference:
                        AssemblyReference assemblyRef = reader.GetAssemblyReference((AssemblyReferenceHandle)scope);
                        string assemblyName = reader.GetString(assemblyRef.Name);
                        return new ParameterInfo(ParameterInfo.IndirectKind.None, name)
                        {
                            AssemblyName = assemblyName
                        };
                    case HandleKind.TypeReference:
                        return GetTypeFromReference(reader, (TypeReferenceHandle)scope, rawTypeKind);
                    default:
                        throw new NotSupportedTypeException($"TypeReference ({name}) - Resolution scope: {scope.Kind}");
                }
            }

            public ParameterInfo GetTypeFromSpecification(MetadataReader reader, UnusedGenericContext genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                throw new NotSupportedTypeException($"TypeSpecification - {reader.GetTypeSpecification(handle)}");
            }
        }
    }
}
