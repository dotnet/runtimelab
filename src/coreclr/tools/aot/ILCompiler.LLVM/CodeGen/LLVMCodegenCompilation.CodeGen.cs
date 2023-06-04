// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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
        private readonly Dictionary<TypeDesc, LLVMTypeRef> _llvmStructs = new();

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

        internal LLVMTypeRef GetLLVMSignatureForMethod(bool isManagedAbi, MethodSignature signature, bool hasHiddenParam)
        {
            LLVMTypeRef llvmReturnType = GetLlvmReturnType(signature.ReturnType, out bool isReturnByRef);

            ArrayBuilder<LLVMTypeRef> signatureTypes = default;
            signatureTypes.Add(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)); // Shadow stack pointer

            if (isReturnByRef)
            {
                signatureTypes.Add(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
            }

            if (hasHiddenParam)
            {
                signatureTypes.Add(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
            }

            // Intentionally skipping the 'this' pointer since it could always be a GC reference
            // and thus must be on the shadow stack
            foreach (TypeDesc type in signature)
            {
                if (GetLlvmArgTypeForArg(isManagedAbi, type, out LLVMTypeRef llvmArgType, out _))
                {
                    signatureTypes.Add(llvmArgType);
                }
            }

            return LLVMTypeRef.CreateFunction(llvmReturnType, signatureTypes.ToArray(), false);
        }

        internal static bool CanStoreTypeOnStack(TypeDesc type)
        {
            if (type is DefType defType)
            {
                if (!defType.IsGCPointer && !defType.ContainsGCPointers && !ContainsIsByRef(type))
                {
                    return true;
                }
            }
            else if (type is PointerType || type is FunctionPointerType)
            {
                return true;
            }

            return false;
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

        internal bool GetLlvmArgTypeForArg(bool isManagedAbi, TypeDesc argSigType, out LLVMTypeRef llvmArgType, out bool isPassedByRef)
        {
            isPassedByRef = false;
            bool isLlvmArg = !isManagedAbi || CanStoreTypeOnStack(argSigType);
            TypeDesc argType = argSigType;
            if (isLlvmArg && IsStruct(argSigType))
            {
                argType = GetPrimitiveTypeForTrivialWasmStruct(argSigType);
                if (argType == null)
                {
                    isPassedByRef = true;
                }
            }

            llvmArgType = isPassedByRef ? LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) : GetLLVMTypeForTypeDesc(argType);
            return isLlvmArg;
        }

        internal LLVMTypeRef GetLlvmReturnType(TypeDesc sigReturnType, out bool isPassedByRef)
        {
            isPassedByRef = IsStruct(sigReturnType) && GetPrimitiveTypeForTrivialWasmStruct(sigReturnType) == null;
            if (isPassedByRef || sigReturnType.IsVoid)
            {
                return LLVMTypeRef.Void;
            }

            return GetLLVMTypeForTypeDesc(sigReturnType);
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

                case TypeFlags.ValueType:
                case TypeFlags.Nullable:
                    if (!_llvmStructs.TryGetValue(type, out LLVMTypeRef llvmStructType))
                    {
                        // Treat trivial structs like their underlying types for compatibility with the native ABI.
                        if (GetPrimitiveTypeForTrivialWasmStruct(type) is TypeDesc primitiveType)
                        {
                            llvmStructType = GetLLVMTypeForTypeDesc(primitiveType);
                        }
                        else
                        {
                            List<FieldDesc> sortedFields = new();
                            foreach (FieldDesc field in type.GetFields())
                            {
                                if (!field.IsStatic)
                                {
                                    sortedFields.Add(field);
                                }
                            }

                            // Sort fields by offset and size in order to handle generating unions
                            sortedFields.Sort((left, right) =>
                            {
                                int leftOffset = left.Offset.AsInt;
                                int rightOffset = right.Offset.AsInt;
                                if (leftOffset == rightOffset)
                                {
                                    // Sort union fields in a descending order.
                                    return right.FieldType.GetElementSize().AsInt - left.FieldType.GetElementSize().AsInt;
                                }

                                return leftOffset - rightOffset;
                            });

                            List<LLVMTypeRef> llvmFields = new List<LLVMTypeRef>(sortedFields.Count);
                            int lastOffset = -1;
                            int nextNewOffset = -1;
                            TypeDesc prevType = null;
                            int totalSize = 0;

                            foreach (FieldDesc field in sortedFields)
                            {
                                int curOffset = field.Offset.AsInt;

                                if (prevType == null || (curOffset != lastOffset && curOffset >= nextNewOffset))
                                {
                                    // The layout should be in order
                                    Debug.Assert(curOffset > lastOffset);

                                    int prevElementSize;
                                    if (prevType == null)
                                    {
                                        lastOffset = 0;
                                        prevElementSize = 0;
                                    }
                                    else
                                    {
                                        prevElementSize = prevType.GetElementSize().AsInt;
                                    }

                                    // Pad to this field if necessary
                                    int paddingSize = curOffset - lastOffset - prevElementSize;
                                    if (paddingSize > 0)
                                    {
                                        AddPaddingFields(paddingSize, llvmFields);
                                        totalSize += paddingSize;
                                    }

                                    TypeDesc fieldType = field.FieldType;
                                    int fieldSize = fieldType.GetElementSize().AsInt;

                                    llvmFields.Add(GetLLVMTypeForTypeDesc(fieldType));

                                    totalSize += fieldSize;
                                    lastOffset = curOffset;
                                    prevType = fieldType;
                                    nextNewOffset = curOffset + fieldSize;
                                }
                            }

                            // If explicit layout is greater than the sum of fields, add padding
                            int structSize = type.GetElementSize().AsInt;
                            if (totalSize < structSize)
                            {
                                AddPaddingFields(structSize - totalSize, llvmFields);
                            }

                            llvmStructType = LLVMTypeRef.CreateStruct(llvmFields.ToArray(), true);
                        }

                        _llvmStructs[type] = llvmStructType;
                    }
                    return llvmStructType;

                case TypeFlags.Enum:
                    return GetLLVMTypeForTypeDesc(type.UnderlyingType);

                case TypeFlags.Void:
                    return LLVMTypeRef.Void;

                default:
                    throw new UnreachableException(type.Category.ToString());
            }
        }

        private static bool ContainsIsByRef(TypeDesc type)
        {
            if (type.IsByRef || type.IsByRefLike)
            {
                return true;
            }

            foreach (var field in type.GetFields())
            {
                if (field.IsStatic)
                    continue;

                var fieldType = field.FieldType;
                if (fieldType.IsValueType)
                {
                    var fieldDefType = (DefType)fieldType;
                    if (!fieldDefType.ContainsGCPointers && !fieldDefType.IsByRefLike)
                        continue;

                    if (ContainsIsByRef(fieldType))
                    {
                        return true;
                    }
                }
            }
            return false;
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

        private static void AddPaddingFields(int paddingSize, List<LLVMTypeRef> llvmFields)
        {
            int numInts = paddingSize / 4;
            int numBytes = paddingSize - numInts * 4;
            for (int i = 0; i < numInts; i++)
            {
                llvmFields.Add(LLVMTypeRef.Int32);
            }
            for (int i = 0; i < numBytes; i++)
            {
                llvmFields.Add(LLVMTypeRef.Int8);
            }
        }

        private static bool IsStruct(TypeDesc type) => type.Category is TypeFlags.ValueType or TypeFlags.Nullable;
    }
}
