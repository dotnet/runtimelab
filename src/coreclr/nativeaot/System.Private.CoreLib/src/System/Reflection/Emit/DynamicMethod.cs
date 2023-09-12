// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace System.Reflection.Emit
{
    public sealed class DynamicMethod : MethodInfo
    {
        [RequiresDynamicCode("Creating a DynamicMethod requires dynamic code.")]
        public DynamicMethod(string name, MethodAttributes attributes, CallingConventions callingConvention, Type returnType, Type[] parameterTypes, Module m, bool skipVisibility)
        {
            ReflectionEmitThrower.ThrowPlatformNotSupportedException();
        }

        [RequiresDynamicCode("Creating a DynamicMethod requires dynamic code.")]
        public DynamicMethod(string name, MethodAttributes attributes, CallingConventions callingConvention, Type returnType, Type[] parameterTypes, Type owner, bool skipVisibility)
        {
            ReflectionEmitThrower.ThrowPlatformNotSupportedException();
        }

        [RequiresDynamicCode("Creating a DynamicMethod requires dynamic code.")]
        public DynamicMethod(string name, Type returnType, Type[] parameterTypes)
        {
#if FEATURE_MINT
            Init(name,
                MethodAttributes.Public | MethodAttributes.Static,
                CallingConventions.Standard,
                returnType,
                parameterTypes,
                null,   // owner
                null,   // m
                false,  // skipVisibility
                true);
#else
            ReflectionEmitThrower.ThrowPlatformNotSupportedException();
#endif
        }

        [RequiresDynamicCode("Creating a DynamicMethod requires dynamic code.")]
        public DynamicMethod(string name, Type returnType, Type[] parameterTypes, bool restrictedSkipVisibility)
        {
            ReflectionEmitThrower.ThrowPlatformNotSupportedException();
        }

        [RequiresDynamicCode("Creating a DynamicMethod requires dynamic code.")]
        public DynamicMethod(string name, Type returnType, Type[] parameterTypes, Module m)
        {
#if FEATURE_MINT
            ArgumentNullException.ThrowIfNull(m);

            Init(name,
                MethodAttributes.Public | MethodAttributes.Static,
                CallingConventions.Standard,
                returnType,
                parameterTypes,
                null,   // owner
                m,      // m
                false,  // skipVisibility
                false);
#else
            ReflectionEmitThrower.ThrowPlatformNotSupportedException();
#endif
        }

        [RequiresDynamicCode("Creating a DynamicMethod requires dynamic code.")]
        public DynamicMethod(string name, Type returnType, Type[] parameterTypes, Module m, bool skipVisibility)
        {
            ReflectionEmitThrower.ThrowPlatformNotSupportedException();
        }

        [RequiresDynamicCode("Creating a DynamicMethod requires dynamic code.")]
        public DynamicMethod(string name, Type returnType, Type[] parameterTypes, Type owner)
        {
            ReflectionEmitThrower.ThrowPlatformNotSupportedException();
        }

        [RequiresDynamicCode("Creating a DynamicMethod requires dynamic code.")]
        public DynamicMethod(string name, Type returnType, Type[] parameterTypes, Type owner, bool skipVisibility)
        {
            ReflectionEmitThrower.ThrowPlatformNotSupportedException();
        }

        public override MethodAttributes Attributes
        {
            get
            {
                return default;
            }
        }

        public override CallingConventions CallingConvention
        {
            get
            {
                return default;
            }
        }

        public override Type DeclaringType
        {
            get
            {
                return default;
            }
        }

        public bool InitLocals
        {
            get
            {
                return default;
            }
            set
            {
            }
        }

        public override RuntimeMethodHandle MethodHandle
        {
            get
            {
                return default;
            }
        }

        public override string Name
        {
            get
            {
                return default;
            }
        }

        public override Type ReflectedType
        {
            get
            {
                return default;
            }
        }

        public override ParameterInfo ReturnParameter
        {
            get
            {
                return default;
            }
        }

        public override Type ReturnType
        {
            get
            {
                return default;
            }
        }

        public override ICustomAttributeProvider ReturnTypeCustomAttributes
        {
            get
            {
                return default;
            }
        }

        public sealed override Delegate CreateDelegate(Type delegateType)
        {
            return default;
        }

        public sealed override Delegate CreateDelegate(Type delegateType, object target)
        {
            return default;
        }

        public ParameterBuilder DefineParameter(int position, ParameterAttributes attributes, string parameterName)
        {
            return default;
        }

        public DynamicILInfo GetDynamicILInfo()
        {
            return default;
        }

        public override MethodInfo GetBaseDefinition()
        {
            return default;
        }

        public override object[] GetCustomAttributes(bool inherit)
        {
            return default;
        }

        public override object[] GetCustomAttributes(Type attributeType, bool inherit)
        {
            return default;
        }

        public ILGenerator GetILGenerator()
        {
            return default;
        }

        public ILGenerator GetILGenerator(int streamSize)
        {
            return default;
        }

        public override MethodImplAttributes GetMethodImplementationFlags()
        {
            return default;
        }

        public override ParameterInfo[] GetParameters()
        {
            return default;
        }

        public override object? Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture)
        {
            return default;
        }

        public override bool IsDefined(Type attributeType, bool inherit)
        {
            return default;
        }

        public override string ToString()
        {
            return default;
        }

#if FEATURE_MINT
        [MemberNotNull(nameof(_parameterTypes))]
        [MemberNotNull(nameof(_returnType))]
        [MemberNotNull(nameof(_module))]
        [MemberNotNull(nameof(_name))]
        private void Init(string name,
                          MethodAttributes attributes,
                          CallingConventions callingConvention,
                          Type? returnType,
                          Type[]? signature,
                          Type? owner,
                          Module? m,
                          bool skipVisibility,
                          bool transparentMethod)
        {
            ArgumentNullException.ThrowIfNull(name);

            AssemblyBuilder.EnsureDynamicCodeSupported();

            if (attributes != (MethodAttributes.Static | MethodAttributes.Public) || callingConvention != CallingConventions.Standard)
                throw new NotSupportedException(SR.NotSupported_DynamicMethodFlags);

            // check and store the signature
            if (signature != null)
            {
                _parameterTypes = new RuntimeType[signature.Length];
                for (int i = 0; i < signature.Length; i++)
                {
                    if (signature[i] == null)
                        throw new ArgumentException(SR.Arg_InvalidTypeInSignature);
                    _parameterTypes[i] = (signature[i].UnderlyingSystemType as RuntimeType)!;
                    if (_parameterTypes[i] == null || _parameterTypes[i] == typeof(void))
                        throw new ArgumentException(SR.Arg_InvalidTypeInSignature);
                }
            }
            else
            {
                _parameterTypes = Array.Empty<RuntimeType>();
            }

            // check and store the return value
            _returnType = returnType is null ?
                (RuntimeType)typeof(void) :
                (returnType.UnderlyingSystemType as RuntimeType) ?? throw new NotSupportedException(SR.Arg_InvalidTypeInRetType);

            if (transparentMethod)
            {
                Debug.Assert(owner == null && m == null, "owner and m cannot be set for transparent methods");
                _module = GetDynamicMethodsModule();
                _restrictedSkipVisibility = skipVisibility;
            }
            else
            {
                Debug.Assert(m != null || owner != null, "Constructor should ensure that either m or owner is set");
                Debug.Assert(m == null || !m.Equals(s_anonymouslyHostedDynamicMethodsModule), "The user cannot explicitly use this assembly");
                Debug.Assert(m == null || owner == null, "m and owner cannot both be set");

                if (m != null)
                    _module = RuntimeModuleBuilder.GetRuntimeModuleFromModule(m); // this returns the underlying module for all RuntimeModule and ModuleBuilder objects.
                else
                {
                    if (owner?.UnderlyingSystemType is RuntimeType rtOwner)
                    {
                        if (rtOwner.HasElementType || rtOwner.ContainsGenericParameters
                            || rtOwner.IsGenericParameter || rtOwner.IsInterface)
                            throw new ArgumentException(SR.Argument_InvalidTypeForDynamicMethod);

                        _typeOwner = rtOwner;
                        _module = rtOwner.GetRuntimeModule();
                    }
                    else
                    {
                        _module = null!;
                    }
                }

                _skipVisibility = skipVisibility;
            }

            // initialize remaining fields
            _ilGenerator = null;
            _initLocals = true;
            _methodHandle = null;
            _name = name;
            _attributes = attributes;
            _callingConvention = callingConvention;
        }
#endif

    }
}
