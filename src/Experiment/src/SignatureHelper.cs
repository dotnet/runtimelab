// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace System.Reflection.Emit.Experimental
{
    internal static class SignatureHelper
    {
        internal static BlobBuilder FieldSignatureEncoder(Type fieldType, ModuleBuilder moduleBuilder)
        {
            var fieldSignature = new BlobBuilder();

            var encoder = new BlobEncoder(fieldSignature).FieldSignature();

            MapReflectionTypeToSignatureType(encoder, fieldType, moduleBuilder);

            return fieldSignature;
        }

        internal static BlobBuilder MethodSignatureEncoder(ParameterInfo[]? parameters, ParameterInfo? returnType, bool isInstance, ModuleBuilder moduleBuilder)
        {
            Type[]? typeParameters = null;
            Type? typeReturn = null;

            if (parameters != null)
            {
                typeParameters = Array.ConvertAll(parameters, parameter => parameter.ParameterType);
            }

            if (returnType != null)
            {
                typeReturn = returnType.ParameterType;
            }

            return MethodSignatureEncoder(typeParameters, typeReturn, isInstance, moduleBuilder);
        }

        internal static BlobBuilder MethodSignatureEncoder(Type[]? parameters, Type? returnType, bool isInstance, ModuleBuilder moduleBuilder)
        {
            // Encoding return type and parameters.
            var methodSignature = new BlobBuilder();

            ParametersEncoder parEncoder;
            ReturnTypeEncoder retEncoder;

            new BlobEncoder(methodSignature)
                .MethodSignature(isInstanceMethod: isInstance).
                Parameters((parameters == null) ? 0 : parameters.Length, out retEncoder, out parEncoder);

            if (returnType != null && returnType != typeof(void))
            {
                MapReflectionTypeToSignatureType(retEncoder.Type(), returnType, moduleBuilder);
            }
            else // If null mark ReturnTypeEncoder as void
            {
                retEncoder.Void();
            }

            if (parameters != null) // If parameters null, just keep empty ParametersEncoder empty
            {
                foreach (var parameter in parameters)
                {
                    MapReflectionTypeToSignatureType(parEncoder.AddParameter().Type(), parameter, moduleBuilder);
                }
            }

            return methodSignature;
        }

        private static void MapReflectionTypeToSignatureType(SignatureTypeEncoder signature, Type type, ModuleBuilder module)
        {
            bool standardType = true;

            if (type.IsArray) // Currently assuming SZ arrays
            {
                signature.SZArray();
                var type1 = type.GetElementType();
                if (type1 == null)
                {
                    throw new ArgumentException("Array has no type");
                }

                type = type1;
            }

            // We need to translate from Reflection.Type to SignatureTypeEncoder.
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
                case "System.Single":
                    signature.Single();
                    break;
                case "System.Double":
                    signature.Double();
                    break;
                case "System.Int32":
                    signature.Int32();
                    break;
                case "System.UInt32":
                    signature.UInt32();
                    break;
                case "System.IntPtr":
                    signature.IntPtr();
                    break;
                case "System.UIntPtr":
                    signature.UIntPtr();
                    break;
                case "System.Int64":
                    signature.Int64();
                    break;
                case "System.UInt64":
                    signature.UInt64();
                    break;
                case "System.Int16":
                    signature.Int16();
                    break;
                case "System.UInt16":
                    signature.UInt16();
                    break;
                case "System.Object":
                    signature.Object();
                    break;
                case "System.String":
                    signature.String();
                    break;
                default: standardType = false;
                    break;
            }

            if (!standardType)
            {
                signature.Type(module.AddorGetTypeReference(type), type.IsValueType);
            }

        }
    }
}
