// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace System.Reflection.Emit.Experimental
{
    //This is prototype code, to generate simple signatures.
    //For more complex signatures, port System.Reflection.Emit's SignatureHelper.
    internal static class SignatureHelper
    {
        internal static BlobBuilder MethodSignatureEnconder(ParameterInfo[]? parameters, ParameterInfo? returnType, bool isInstance)
        {
            Type[]? _typeParameters = null;
            Type? typeReturn = null;

            if (parameters != null)
            {
                _typeParameters = Array.ConvertAll(parameters, parameter => parameter.ParameterType);
            }

            if (returnType != null)
            {
                typeReturn = returnType.ParameterType;
            }

            return MethodSignatureEnconder(_typeParameters, typeReturn, isInstance);
        }
        internal static BlobBuilder MethodSignatureEnconder(Type[]? parameters, Type? returnType, bool isInstance)
        {
            // Encoding return type and parameters.
            var methodSignature = new BlobBuilder();

            ParametersEncoder _parEncoder;
            ReturnTypeEncoder _retEncoder;

            new BlobEncoder(methodSignature).
                MethodSignature(isInstanceMethod: isInstance).
                Parameters((parameters == null) ? 0 : parameters.Length, out _retEncoder, out _parEncoder);

            if (returnType != null && returnType != typeof(void))
            {
                MapReflectionTypeToSignatureType(_retEncoder.Type(), returnType);
            }
            else // If null mark ReturnTypeEncoder as void
            {
                _retEncoder.Void();
            }

            if (parameters != null) // If parameters null, just keep empty ParametersEncoder empty
            {
                foreach (var parameter in parameters)
                {
                    MapReflectionTypeToSignatureType(_parEncoder.AddParameter().Type(), parameter);
                }
            }
            return methodSignature;
        }

        private static void MapReflectionTypeToSignatureType(SignatureTypeEncoder signature, Type type)
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
