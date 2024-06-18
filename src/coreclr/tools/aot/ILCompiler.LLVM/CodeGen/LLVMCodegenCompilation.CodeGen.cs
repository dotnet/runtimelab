// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Numerics;

using ILCompiler.DependencyAnalysis;

using Internal.JitInterface;
using Internal.JitInterface.LLVMInterop;
using Internal.TypeSystem;

namespace ILCompiler
{
    public sealed unsafe partial class LLVMCodegenCompilation
    {
        internal LLVMContext* LLVMContext { get; } = LLVMContextRef.Create();
        internal LLVMType* LLVMInt8Type { get; private set; }
        internal LLVMType* LLVMInt16Type { get; private set; }
        internal LLVMType* LLVMInt32Type { get; private set; }
        internal LLVMType* LLVMInt64Type { get; private set; }
        internal LLVMType* LLVMPtrType { get; private set; }
        internal LLVMType* LLVMFloatType { get; private set; }
        internal LLVMType* LLVMDoubleType { get; private set; }
        internal LLVMType* LLVMVoidType { get; private set; }

        private void InitializeCodeGen()
        {
            LLVMInt8Type = LLVMTypeRef.GetInt(LLVMContext, 8);
            LLVMInt16Type = LLVMTypeRef.GetInt(LLVMContext, 16);
            LLVMInt32Type = LLVMTypeRef.GetInt(LLVMContext, 32);
            LLVMInt64Type = LLVMTypeRef.GetInt(LLVMContext, 64);
            LLVMPtrType = LLVMTypeRef.GetPointer(LLVMContext);
            LLVMFloatType = LLVMTypeRef.GetFloat(LLVMContext);
            LLVMDoubleType = LLVMTypeRef.GetDouble(LLVMContext);
            LLVMVoidType = LLVMTypeRef.GetVoid(LLVMContext);
        }

        public override ISymbolNode GetExternalMethodAccessor(MethodDesc method, ReadOnlySpan<TargetAbiType> sig)
        {
            Debug.Assert(!sig.IsEmpty);
            string name = PInvokeILProvider.GetDirectCallExternName(method);

            return NodeFactory.ExternSymbolWithAccessor(name, method, sig);
        }

        public override CorInfoLlvmEHModel GetLlvmExceptionHandlingModel() => Options.ExceptionHandlingModel;

        internal LLVMTypeRef GetLLVMSignatureForMethod(MethodSignature signature, bool hasHiddenParam)
        {
            LLVMTypeRef llvmReturnType = GetLlvmReturnType(signature.ReturnType, out bool isReturnByRef);

            int maxLlvmSigLength = signature.Length + 4;
            Span<LLVMTypeRef> signatureTypes =
                maxLlvmSigLength > 100 ? new LLVMTypeRef[maxLlvmSigLength] : stackalloc LLVMTypeRef[maxLlvmSigLength];

            int index = 0;
            signatureTypes[index++] = LLVMPtrType;

            if (!signature.IsStatic) // Bug: doesn't handle explicit 'this'.
            {
                signatureTypes[index++] = LLVMPtrType;
            }

            if (isReturnByRef)
            {
                signatureTypes[index++] = LLVMPtrType;
            }

            if (hasHiddenParam)
            {
                signatureTypes[index++] = LLVMPtrType;
            }

            foreach (TypeDesc type in signature)
            {
                signatureTypes[index++] = GetLlvmArgTypeForArg(type);
            }

            return LLVMTypeRef.CreateFunction(llvmReturnType, signatureTypes.Slice(0, index));
        }

        public override TypeDesc GetPrimitiveTypeForTrivialWasmStruct(TypeDesc type)
        {
            int size = type.GetElementSize().AsInt;
            if (size <= sizeof(double) && BitOperations.IsPow2(size))
            {
                while (true)
                {
                    FieldDesc singleInstanceField = null;
                    foreach (FieldDesc field in type.GetFields())
                    {
                        if (!field.IsStatic)
                        {
                            if (singleInstanceField != null)
                            {
                                return null;
                            }

                            singleInstanceField = field;
                        }
                    }

                    if (singleInstanceField == null)
                    {
                        return null;
                    }

                    TypeDesc singleInstanceFieldType = singleInstanceField.FieldType;
                    if (!IsStruct(singleInstanceFieldType))
                    {
                        if (singleInstanceFieldType.GetElementSize().AsInt != size)
                        {
                            return null;
                        }

                        return singleInstanceFieldType;
                    }

                    type = singleInstanceFieldType;
                }
            }

            return null;
        }

        public LLVMTypeRef GetLlvmArgTypeForArg(TypeDesc argSigType)
        {
            bool isPassedByRef = false;
            TypeDesc argType = argSigType;
            if (IsStruct(argSigType))
            {
                argType = GetPrimitiveTypeForTrivialWasmStruct(argSigType);
                if (argType == null)
                {
                    isPassedByRef = true;
                }
            }

            return isPassedByRef ? LLVMTypeRef.GetPointer(LLVMContext) : GetLLVMTypeForTypeDesc(argType);
        }

        internal LLVMTypeRef GetLlvmReturnType(TypeDesc sigReturnType, out bool isPassedByRef)
        {
            TypeDesc returnType = sigReturnType;
            if (IsStruct(sigReturnType))
            {
                returnType = GetPrimitiveTypeForTrivialWasmStruct(sigReturnType);
            }

            isPassedByRef = returnType == null;
            return isPassedByRef ? LLVMVoidType : GetLLVMTypeForTypeDesc(returnType);
        }

        internal LLVMTypeRef GetLLVMTypeForTypeDesc(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Boolean:
                case TypeFlags.SByte:
                case TypeFlags.Byte:
                    return LLVMInt8Type;

                case TypeFlags.Int16:
                case TypeFlags.UInt16:
                case TypeFlags.Char:
                    return LLVMInt16Type;

                case TypeFlags.Int32:
                case TypeFlags.UInt32:
                    return LLVMInt32Type;

                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                    return _nodeFactory.Target.PointerSize == 4 ? LLVMInt32Type : LLVMInt64Type;

                case TypeFlags.Array:
                case TypeFlags.SzArray:
                case TypeFlags.ByRef:
                case TypeFlags.Class:
                case TypeFlags.Interface:
                case TypeFlags.Pointer:
                case TypeFlags.FunctionPointer:
                    return LLVMPtrType;

                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                    return LLVMInt64Type;

                case TypeFlags.Single:
                    return LLVMFloatType;

                case TypeFlags.Double:
                    return LLVMDoubleType;

                case TypeFlags.Enum:
                    return GetLLVMTypeForTypeDesc(type.UnderlyingType);

                case TypeFlags.Void:
                    return LLVMVoidType;

                default:
                    throw new UnreachableException(type.Category.ToString());
            }
        }

        private static bool IsStruct(TypeDesc type) => type.Category is TypeFlags.ValueType or TypeFlags.Nullable;
    }
}
