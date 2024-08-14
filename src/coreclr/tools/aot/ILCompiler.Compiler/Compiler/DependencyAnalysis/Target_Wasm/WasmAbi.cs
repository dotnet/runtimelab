// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis.Wasm
{
    public static class WasmAbi
    {
        public static WasmValueType GetNaturalIntType(TargetDetails target) =>
            target.PointerSize == 4 ? WasmValueType.I32 : WasmValueType.I64;

        public static WasmFunctionType GetWasmFunctionType(MethodDesc method) =>
            GetWasmFunctionType(method.Signature, method.RequiresInstArg());

        public static WasmFunctionType GetWasmFunctionType(MethodSignature signature, bool hasHiddenParam)
        {
            WasmValueType wasmPointerType = GetNaturalIntType(signature.Context.Target);

            WasmValueType GetWasmArgTypeForArg(TypeDesc argSigType)
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

                return isPassedByRef ? wasmPointerType : GetWasmTypeForTypeDesc(argType);
            }

            WasmValueType wasmReturnType = GetWasmReturnType(signature.ReturnType, out bool isReturnByRef);

            int maxWasmSigLength = signature.Length + 4;
            Span<WasmValueType> signatureTypes =
                maxWasmSigLength > 100 ? new WasmValueType[maxWasmSigLength] : stackalloc WasmValueType[maxWasmSigLength];

            int index = 0;
            signatureTypes[index++] = wasmPointerType;

            if (!signature.IsStatic) // TODO-LLVM-Bug: doesn't handle explicit 'this'.
            {
                signatureTypes[index++] = wasmPointerType;
            }

            if (isReturnByRef)
            {
                signatureTypes[index++] = wasmPointerType;
            }

            if (hasHiddenParam)
            {
                signatureTypes[index++] = wasmPointerType;
            }

            foreach (TypeDesc type in signature)
            {
                signatureTypes[index++] = GetWasmArgTypeForArg(type);
            }

            return new WasmFunctionType(wasmReturnType, signatureTypes.Slice(0, index).ToArray());
        }

        public static WasmValueType GetWasmReturnType(MethodDesc method, out bool isPassedByRef) =>
            GetWasmReturnType(method.Signature.ReturnType, out isPassedByRef);

        public static TypeDesc GetPrimitiveTypeForTrivialWasmStruct(TypeDesc type)
        {
            int size = type.GetElementSize().AsInt;
            if (size <= sizeof(double) && int.IsPow2(size))
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

        private static WasmValueType GetWasmReturnType(TypeDesc sigReturnType, out bool isPassedByRef)
        {
            TypeDesc returnType = sigReturnType;
            if (IsStruct(sigReturnType))
            {
                returnType = GetPrimitiveTypeForTrivialWasmStruct(sigReturnType);
            }

            isPassedByRef = returnType == null;
            return isPassedByRef ? WasmValueType.Invalid : GetWasmTypeForTypeDesc(returnType);
        }

        private static WasmValueType GetWasmTypeForTypeDesc(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Boolean:
                case TypeFlags.SByte:
                case TypeFlags.Byte:
                case TypeFlags.Int16:
                case TypeFlags.UInt16:
                case TypeFlags.Char:
                case TypeFlags.Int32:
                case TypeFlags.UInt32:
                    return WasmValueType.I32;

                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                case TypeFlags.Array:
                case TypeFlags.SzArray:
                case TypeFlags.ByRef:
                case TypeFlags.Class:
                case TypeFlags.Interface:
                case TypeFlags.Pointer:
                case TypeFlags.FunctionPointer:
                    return GetNaturalIntType(type.Context.Target);

                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                    return WasmValueType.I64;

                case TypeFlags.Single:
                    return WasmValueType.F32;

                case TypeFlags.Double:
                    return WasmValueType.F64;

                case TypeFlags.Enum:
                    return GetWasmTypeForTypeDesc(type.UnderlyingType);

                case TypeFlags.Void:
                    return WasmValueType.Invalid;

                default:
                    throw new UnreachableException(type.Category.ToString());
            }
        }

        private static bool IsStruct(TypeDesc type) => type.Category is TypeFlags.ValueType or TypeFlags.Nullable;
    }
}
