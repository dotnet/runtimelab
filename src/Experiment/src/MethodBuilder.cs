// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace System.Reflection.Emit.Experimental
{
    public class MethodBuilder : System.Reflection.MethodInfo
    {
        public override string Name { get; }
        public override System.Reflection.MethodAttributes Attributes { get; }
        public override System.Reflection.CallingConventions CallingConvention { get; }
        public override System.Type? DeclaringType { get; }
        public override System.Reflection.Module Module { get; }
        internal MethodDefinitionHandle _handle;
        private Type? _returnType;
        private Type[]? _parameters;

        internal MethodBuilder(string name, System.Reflection.MethodAttributes attributes, CallingConventions callingConventions, Type? returnType, Type[]? mthdParams,Type declaringType)
        {
            Name = name;
            Attributes = attributes;
            CallingConvention = callingConventions;
            _returnType = returnType;
            _parameters = mthdParams;
            DeclaringType = declaringType;
            Module = declaringType.Module;
        }

        internal MethodDefinitionHandle AppendMetadata(MetadataBuilder _metadata)
        {
            // Encoding return type and parameters.
            var methodSignature = new BlobBuilder();
            ParametersEncoder _parEncoder;
            ReturnTypeEncoder _retEncoder;
            new BlobEncoder(methodSignature).
                MethodSignature(). // Need to add in calling conventions. 
                Parameters((_parameters==null)? 0 :_parameters.Length, out _retEncoder, out _parEncoder);

            if (_returnType != null && _returnType != typeof(void)) 
            {
                MapReflectionTypeToSignatureType(_retEncoder.Type(), _returnType);
            }
            else // If null mark ReturnTypeEncoder as void
            {
                _retEncoder.Void();
            }

            if (_parameters != null) // If parameters null, just keep empty ParametersEncoder empty
            {
                foreach (var parameter in _parameters)
                {
                    MapReflectionTypeToSignatureType(_parEncoder.AddParameter().Type(), parameter);
                }
            }

            // Create the method
            _handle = _metadata.AddMethodDefinition(
                Attributes,
                MethodImplAttributes.IL,
               _metadata.GetOrAddString(Name),
               _metadata.GetOrAddBlob(methodSignature),
               -1, //No body with interfaces
               parameterList: default);
            return _handle;
        }

        private static void MapReflectionTypeToSignatureType(SignatureTypeEncoder signature, Type type)
        {
            //We need to translate from Reflection.Type to SignatureTypeEncoder. Only most common types supported for the prototype.
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
                default: throw new NotImplementedException("This parameter type is not yet supported");
            }
        }

        // These methods seems like they should be implemented next.
        public System.Reflection.Emit.ParameterBuilder DefineParameter(int position, System.Reflection.ParameterAttributes attributes, string? strParamName)
        {
            throw new NotImplementedException();
        }

        public void SetImplementationFlags(System.Reflection.MethodImplAttributes attributes)
        {
            throw new NotImplementedException();
        }

        public void SetParameters(params System.Type[] parameterTypes)
        {
            throw new NotImplementedException();
        }

        public void SetReturnType(System.Type? returnType)
        {
            throw new NotImplementedException();
        }

        public void SetSignature(System.Type? returnType, System.Type[]? returnTypeRequiredCustomModifiers, System.Type[]? returnTypeOptionalCustomModifiers, System.Type[]? parameterTypes, System.Type[][]? parameterTypeRequiredCustomModifiers, System.Type[][]? parameterTypeOptionalCustomModifiers)
        {
            throw new NotImplementedException();
        }

        public override bool ContainsGenericParameters { get => throw new NotImplementedException(); }
        public bool InitLocals { get => throw new NotImplementedException(); set { } }
        public override bool IsGenericMethod { get => throw new NotImplementedException(); }
        public override bool IsGenericMethodDefinition { get => throw new NotImplementedException(); }
        public override bool IsSecurityCritical { get => throw new NotImplementedException(); }
        public override bool IsSecuritySafeCritical { get => throw new NotImplementedException(); }
        public override bool IsSecurityTransparent { get => throw new NotImplementedException(); }
        public override int MetadataToken { get => throw new NotImplementedException(); }
        public override System.RuntimeMethodHandle MethodHandle { get => throw new NotImplementedException(); }
        public override System.Type? ReflectedType { get => throw new NotImplementedException(); }
        public override System.Reflection.ParameterInfo ReturnParameter { get => throw new NotImplementedException(); }
        public override System.Type ReturnType { get => throw new NotImplementedException(); }
        public override System.Reflection.ICustomAttributeProvider ReturnTypeCustomAttributes { get => throw new NotImplementedException(); }
        
        public System.Reflection.Emit.GenericTypeParameterBuilder[] DefineGenericParameters(params string[] names) 
            => throw new NotImplementedException();

        public override System.Reflection.MethodInfo GetBaseDefinition() 
            => throw new NotImplementedException();

        public override object[] GetCustomAttributes(bool inherit) 
            => throw new NotImplementedException();

        public override object[] GetCustomAttributes(System.Type attributeType, bool inherit) 
            => throw new NotImplementedException();

        public override System.Type[] GetGenericArguments() 
            => throw new NotImplementedException();

        public override System.Reflection.MethodInfo GetGenericMethodDefinition() 
            => throw new NotImplementedException();

        public override int GetHashCode() 
            => throw new NotImplementedException();

        public System.Reflection.Emit.ILGenerator GetILGenerator() 
            => throw new NotImplementedException();

        public System.Reflection.Emit.ILGenerator GetILGenerator(int size) 
            => throw new NotImplementedException();

        public override System.Reflection.MethodImplAttributes GetMethodImplementationFlags() 
            => throw new NotImplementedException();

        public override System.Reflection.ParameterInfo[] GetParameters() 
            => throw new NotImplementedException();

        public override object Invoke(object? obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder? binder, object?[]? parameters, System.Globalization.CultureInfo? culture) 
            => throw new NotImplementedException();

        public override bool IsDefined(System.Type attributeType, bool inherit) 
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("The native code for this instantiation might not be available at runtime.")]
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
        public override System.Reflection.MethodInfo MakeGenericMethod(params System.Type[] typeArguments) 
            => throw new NotImplementedException();

        public void SetCustomAttribute(System.Reflection.ConstructorInfo con, byte[] binaryAttribute) 
            => throw new NotImplementedException();

        public void SetCustomAttribute(System.Reflection.Emit.CustomAttributeBuilder customBuilder) 
            => throw new NotImplementedException();

        public override string ToString() 
            => throw new NotImplementedException();
    }
}
