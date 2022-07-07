
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;


//This is prototype code, to generate simple signatures.
//For more complex signatures, port System.Reflection.Emit's SignatureHelper.
namespace System.Reflection.Emit.Experimental
{
    internal class SignatureHelper
    {

        internal static BlobBuilder MethodSignatureEnconder(ParameterInfo[] parameters, Type? returnType, bool isInstance)
        {
            // Encoding return type and parameters.
            var methodSignature = new BlobBuilder();

            ParametersEncoder _parEncoder;
            ReturnTypeEncoder _retEncoder;

            new BlobEncoder(methodSignature).
                MethodSignature(isInstanceMethod:isInstance). 
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
                    MapReflectionTypeToSignatureType(_parEncoder.AddParameter().Type(), parameter.ParameterType);
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
