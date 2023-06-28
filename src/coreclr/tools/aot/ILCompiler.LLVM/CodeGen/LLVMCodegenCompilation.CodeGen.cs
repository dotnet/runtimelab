// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Numerics;

using ILCompiler.DependencyAnalysis;

using Internal.JitInterface;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using LLVMSharp.Interop;

namespace ILCompiler
{
    public sealed partial class LLVMCodegenCompilation
    {
        // We define an alternative entrypoint for the runtime exports, one that has the (original) managed calling convention.
        // This allows managed code as well as low-level runtime helpers to avoid the overhead of shadow stack save/restore
        // when calling the export. Thus, the "mangling" we use here is effectively an ABI contract between the compiler and
        // runtime.
        public override string GetRuntimeExportManagedEntrypointName(MethodDesc method)
        {
            if (!method.HasCustomAttribute("System.Runtime", "RuntimeExportAttribute"))
            {
                return null;
            }

            return ((EcmaMethod)method).GetRuntimeExportName() + "_Managed";
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
            LLVMTypeRef llvmPtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
            LLVMTypeRef llvmReturnType = GetLlvmReturnType(signature.ReturnType, out bool isReturnByRef);

            int maxLlvmSigLength = signature.Length + 4;
            Span<LLVMTypeRef> signatureTypes =
                maxLlvmSigLength > 100 ? new LLVMTypeRef[maxLlvmSigLength] : stackalloc LLVMTypeRef[maxLlvmSigLength];

            int index = 0;
            signatureTypes[index++] = llvmPtrType;

            if (!signature.IsStatic) // Bug: doesn't handle explicit 'this'.
            {
                signatureTypes[index++] = llvmPtrType;
            }

            if (isReturnByRef)
            {
                signatureTypes[index++] = llvmPtrType;
            }

            if (hasHiddenParam)
            {
                signatureTypes[index++] = llvmPtrType;
            }

            foreach (TypeDesc type in signature)
            {
                signatureTypes[index++] = GetLlvmArgTypeForArg(type);
            }

            return LLVMTypeRef.CreateFunction(llvmReturnType, signatureTypes.Slice(0, index), false);
        }

        public override TypeDesc GetPrimitiveTypeForTrivialWasmStruct(TypeDesc type)
        {
            Debug.Assert(IsStruct(type));

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

            return isPassedByRef ? LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) : GetLLVMTypeForTypeDesc(argType);
        }

        internal LLVMTypeRef GetLlvmReturnType(TypeDesc sigReturnType, out bool isPassedByRef)
        {
            TypeDesc returnType = sigReturnType;
            if (IsStruct(sigReturnType))
            {
                returnType = GetPrimitiveTypeForTrivialWasmStruct(sigReturnType);
            }

            isPassedByRef = returnType == null;
            return isPassedByRef ? LLVMTypeRef.Void : GetLLVMTypeForTypeDesc(returnType);
        }

        public override int PadOffset(TypeDesc type, int atOffset)
        {
            var fieldAlignment = type is DefType && type.IsValueType ? ((DefType)type).InstanceFieldAlignment : type.Context.Target.LayoutPointerSize;
            var alignment = LayoutInt.Min(fieldAlignment, new LayoutInt(ComputePackingSize(type))).AsInt;
            var padding = atOffset.AlignUp(alignment);

            return padding;
        }

        internal int PadNextOffset(TypeDesc type, int atOffset)
        {
            return PadOffset(type, atOffset) + type.GetElementSize().AsInt;
        }

        internal LLVMTypeRef GetLLVMTypeForTypeDesc(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Boolean:
                case TypeFlags.SByte:
                case TypeFlags.Byte:
                    return LLVMTypeRef.Int8;

                case TypeFlags.Int16:
                case TypeFlags.UInt16:
                case TypeFlags.Char:
                    return LLVMTypeRef.Int16;

                case TypeFlags.Int32:
                case TypeFlags.UInt32:
                    return LLVMTypeRef.Int32;

                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                    return _nodeFactory.Target.PointerSize == 4 ? LLVMTypeRef.Int32 : LLVMTypeRef.Int64;

                case TypeFlags.Array:
                case TypeFlags.SzArray:
                case TypeFlags.ByRef:
                case TypeFlags.Class:
                case TypeFlags.Interface:
                case TypeFlags.Pointer:
                case TypeFlags.FunctionPointer:
                    return LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);

                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                    return LLVMTypeRef.Int64;

                case TypeFlags.Single:
                    return LLVMTypeRef.Float;

                case TypeFlags.Double:
                    return LLVMTypeRef.Double;

                case TypeFlags.Enum:
                    return GetLLVMTypeForTypeDesc(type.UnderlyingType);

                case TypeFlags.Void:
                    return LLVMTypeRef.Void;

                default:
                    throw new UnreachableException(type.Category.ToString());
            }
        }

        private static int ComputePackingSize(TypeDesc type)
        {
            if (type is MetadataType)
            {
                var metaType = type as MetadataType;
                var layoutMetadata = metaType.GetClassLayout();

                // If a type contains pointers then the metadata specified packing size is ignored (On desktop this is disqualification from ManagedSequential)
                if (layoutMetadata.PackingSize == 0 || metaType.ContainsGCPointers)
                    return type.Context.Target.DefaultPackingSize;
                else
                    return layoutMetadata.PackingSize;
            }
            else
            {
                return type.Context.Target.DefaultPackingSize;
            }
        }

        private static bool IsStruct(TypeDesc type) => type.Category is TypeFlags.ValueType or TypeFlags.Nullable;
    }
}
