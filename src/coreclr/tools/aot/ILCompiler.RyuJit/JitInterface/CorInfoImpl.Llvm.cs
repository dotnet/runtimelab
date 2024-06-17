// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using ILCompiler;
using ILCompiler.DependencyAnalysis;

using Internal.IL;
using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

[assembly: InternalsVisibleTo("ILCompiler.LLVM")]

namespace Internal.JitInterface
{
    internal sealed unsafe partial class CorInfoImpl
    {
        private static readonly void*[] s_jitExports = new void*[(int)CorJitApiId.CJAI_Count + 1];

        private void* _pNativeContext; // Per-thread context pointer. Used by the Jit; opaque to the EE.

        [UnmanagedCallersOnly]
        public static void addCodeReloc(IntPtr thisHandle, void* handle)
        {
            var _this = GetThis(thisHandle);
            ISymbolNode node = (ISymbolNode)_this.HandleToObject(handle);

            _this._codeRelocs.Add(new Relocation(RelocType.IMAGE_REL_BASED_REL32, 0, node));
        }

        // So the char* in cpp is terminated.
        private static byte[] AppendNullByte(ReadOnlySpan<byte> inputArray)
        {
            byte[] nullTerminated = new byte[inputArray.Length + 1];
            inputArray.CopyTo(new Span<byte>(nullTerminated));
            nullTerminated[inputArray.Length] = 0;
            return nullTerminated;
        }

        [UnmanagedCallersOnly]
        public static byte* getMangledMethodName(IntPtr thisHandle, CORINFO_METHOD_STRUCT_* ftn)
        {
            CorInfoImpl _this = GetThis(thisHandle);
            MethodDesc method = _this.HandleToObject(ftn);
            Utf8String mangledName = _this._compilation.NameMangler.GetMangledMethodName(method);

            return (byte*)_this.GetPin(AppendNullByte(mangledName.AsSpan()));
        }

        [UnmanagedCallersOnly]
        public static byte* getMangledSymbolName(IntPtr thisHandle, void* symbolHandle)
        {
            CorInfoImpl _this = GetThis(thisHandle);
            var node = (ISymbolNode)_this.HandleToObject(symbolHandle);

            Utf8StringBuilder sb = new Utf8StringBuilder();
            node.AppendMangledName(_this._compilation.NameMangler, sb);

            sb.Append("\0");
            return (byte*)_this.GetPin(sb.UnderlyingArray);
        }

        [UnmanagedCallersOnly]
        public static byte* getMangledFilterFuncletName(IntPtr thisHandle, uint index)
        {
            CorInfoImpl _this = GetThis(thisHandle);
            Utf8StringBuilder sb = new Utf8StringBuilder();
            _this.GetMangledFilterFuncletName(sb, index);

            sb.Append("\0");
            return (byte*)_this.GetPin(sb.UnderlyingArray);
        }

        public void GetMangledFilterFuncletName(Utf8StringBuilder builder, uint index)
        {
            builder.Clear();
            _methodCodeNode.AppendMangledName(_compilation.NameMangler, builder);
            builder.Append("$F");
            builder.Append(index.ToStringInvariant());
            builder.Append("_Filter");
        }

        [UnmanagedCallersOnly]
        public static int getSignatureForMethodSymbol(IntPtr thisHandle, void* symbolHandle, CORINFO_SIG_INFO* pSig)
        {
            var _this = GetThis(thisHandle);
            var node = (ISymbolNode)_this.HandleToObject(symbolHandle);

            MethodDesc method = null;
            if (node is IMethodNode { Offset: 0 } methodNode)
            {
                method = methodNode.Method;
            }
            else if (node is ReadyToRunHelperNode { Id: ReadyToRunHelperId.VirtualCall } helperNode)
            {
                method = (MethodDesc)helperNode.Target;
            }

            if (method != null)
            {
                _this.Get_CORINFO_SIG_INFO(method, pSig, scope: null);
                if (method.IsUnmanagedCallersOnly)
                {
                    pSig->callConv = CorInfoCallConv.CORINFO_CALLCONV_UNMANAGED;
                }
                return 1;
            }

            // TODO-LLVM: below is a hack. A proper solution would involve upstream work to allow ExternSymbolNode
            // to specify whether it represents a function or data symbol (and what its signature is if the former).
            if (node is ExternSymbolNode externSymbolNode)
            {
                ReadOnlySpan<byte> name = externSymbolNode.Utf8Name.AsSpan();
                if (name.StartsWith("RhpNew"u8))
                {
                    if (name.SequenceEqual("RhpNewFast"u8) ||
                        name.SequenceEqual("RhpNewFinalizable"u8) ||
                        name.SequenceEqual("RhpNewFastAlign8"u8) ||
                        name.SequenceEqual("RhpNewFastMisalign"u8) ||
                        name.SequenceEqual("RhpNewFinalizableAlign8"u8))
                    {
                        TypeDesc pointerType = _this._compilation.TypeSystemContext.GetWellKnownType(WellKnownType.Void).MakePointerType();
                        MethodSignatureFlags signatureFlags = MethodSignatureFlags.Static;
                        MethodSignature signature = new MethodSignature(signatureFlags, 0, pointerType, [pointerType]);

                        // We're technically leaking the signature object here, but we don't get here often, so it's ok.
                        _this.Get_CORINFO_SIG_INFO(signature, pSig, scope: null);
                        return 1;
                    }
                }
            }

            return 0;
        }

        [UnmanagedCallersOnly]
        public static CorInfoType getPrimitiveTypeForTrivialWasmStruct(IntPtr thisHandle, CORINFO_CLASS_STRUCT_* structHnd)
        {
            var _this = GetThis(thisHandle);
            TypeDesc structType = _this.HandleToObject(structHnd);
            if (_this._compilation.GetPrimitiveTypeForTrivialWasmStruct(structType) is TypeDesc primitiveType)
            {
                return _this.asCorInfoType(primitiveType);
            }

            return CorInfoType.CORINFO_TYPE_UNDEF;
        }

        [UnmanagedCallersOnly]
        public static byte* getAlternativeFunctionName(IntPtr thisHandle)
        {
            CorInfoImpl _this = GetThis(thisHandle);
            IMethodNode methodNode = _this._methodCodeNode;
            RyuJitCompilation compilation = _this._compilation;

            string alternativeName = compilation.NodeFactory.GetSymbolAlternateName(methodNode);
            return (alternativeName != null) ? (byte*)_this.GetPin(StringToUTF8(alternativeName)) : null;
        }

        [UnmanagedCallersOnly]
        public static IntPtr getExternalMethodAccessor(IntPtr thisHandle, CORINFO_METHOD_STRUCT_* methodHandle, TargetAbiType* sig, int sigLength)
        {
            CorInfoImpl _this = GetThis(thisHandle);
            MethodDesc method = _this.HandleToObject(methodHandle);
            ISymbolNode accessorNode = _this._compilation.GetExternalMethodAccessor(method, new ReadOnlySpan<TargetAbiType>(sig, sigLength));

            return _this.ObjectToHandle(accessorNode);
        }

        [UnmanagedCallersOnly]
        private static void* getSingleThreadedCompilationContext(IntPtr thisHandle)
        {
            return GetThis(thisHandle)._pNativeContext;
        }

        [UnmanagedCallersOnly]
        private static CorInfoLlvmEHModel getExceptionHandlingModel(IntPtr thisHandle)
        {
            return GetThis(thisHandle)._compilation.GetLlvmExceptionHandlingModel();
        }

        [UnmanagedCallersOnly]
        private static IntPtr getExceptionThrownVariable(IntPtr thisHandle)
        {
            CorInfoImpl _this = GetThis(thisHandle);
            ISymbolNode node = _this._compilation.NodeFactory.ExternSymbol("RhpExceptionThrown");
            return _this.ObjectToHandle(node);
        }

        public struct CORINFO_LLVM_EH_CLAUSE
        {
            public CORINFO_EH_CLAUSE_FLAGS Flags;
            public uint EnclosingIndex;
            public mdToken ClauseTypeToken;
            public uint FilterIndex;
        }

        [UnmanagedCallersOnly]
        private static IntPtr getExceptionHandlingTable(IntPtr thisHandle, CORINFO_LLVM_EH_CLAUSE* pClauses, int count)
        {
            CorInfoImpl _this = GetThis(thisHandle);
            RyuJitCompilation compilation = _this._compilation;
            MethodIL methodIL = (MethodIL)_this.HandleToObject((void*)_this._methodScope);
            if (count == 1 && pClauses[0].Flags == CORINFO_EH_CLAUSE_FLAGS.CORINFO_EH_CLAUSE_NONE && pClauses[0].EnclosingIndex == 0)
            {
                TypeDesc type = (TypeDesc)methodIL.GetObject((int)pClauses[0].ClauseTypeToken);
                ISymbolNode symbol = compilation.NecessaryTypeSymbolIfPossible(type);

                return _this.ObjectToHandle(symbol);
            }

            uint maxEclosingIndex = 0;
            for (int i = 0; i < count; i++)
            {
                maxEclosingIndex = Math.Max(pClauses[i].EnclosingIndex, maxEclosingIndex);
            }

            const int MetadataFilter = 1;
            const int MetadataShift = 1;

            int align = compilation.NodeFactory.Target.PointerSize;
            ObjectDataBuilder builder = new(compilation.NodeFactory, relocsOnly: true);
            builder.RequireInitialAlignment(align);

            bool isSmallFormat = maxEclosingIndex <= (byte.MaxValue >> MetadataShift);
            if (isSmallFormat)
            {
                builder.EmitZeros(align - count % align);
            }
            else
            {
                builder.EmitZeros(align - 4 * count % align);
            }

            for (int i = count - 1; i >= 0; i--)
            {
                CORINFO_LLVM_EH_CLAUSE* pClause = &pClauses[i];
                uint metadata = pClause->EnclosingIndex << MetadataShift;
                if ((pClause->Flags & CORINFO_EH_CLAUSE_FLAGS.CORINFO_EH_CLAUSE_FILTER) != 0)
                {
                    metadata |= MetadataFilter;
                }

                if (isSmallFormat)
                {
                    Debug.Assert((byte)metadata == metadata);
                    builder.EmitByte((byte)metadata);
                }
                else
                {
                    builder.EmitUInt(metadata);
                }
            }

            // This is the offset at which which the EH info symbol will be defined.
            int symbolDefOffset = builder.CountBytes + (isSmallFormat ? 1 : 2);
            Debug.Assert(builder.CountBytes % align == 0);

            Utf8StringBuilder sb = new();
            for (int i = 0; i < count; i++)
            {
                CORINFO_LLVM_EH_CLAUSE* pClause = &pClauses[i];

                ISymbolNode symbol;
                if ((pClause->Flags & CORINFO_EH_CLAUSE_FLAGS.CORINFO_EH_CLAUSE_FILTER) != 0)
                {
                    _this.GetMangledFilterFuncletName(sb, pClause->FilterIndex);
                    symbol = compilation.NodeFactory.ExternSymbol(sb.ToString());
                }
                else
                {
                    TypeDesc type = (TypeDesc)methodIL.GetObject((int)pClause->ClauseTypeToken);
                    symbol = compilation.NecessaryTypeSymbolIfPossible(type);
                }

                builder.EmitPointerReloc(symbol);
            }

            ILLVMMethodCodeNode methodNode = ((ILLVMMethodCodeNode)_this._methodCodeNode);
            ObjectNode.ObjectData ehInfo = builder.ToObjectData();
            ISymbolNode ehInfoNode = methodNode.InitializeEHInfoLLVM(ehInfo, symbolDefOffset);

            return _this.ObjectToHandle(ehInfoNode);
        }

        public enum CorInfoLlvmJitTestKind
        {
            CORINFO_JIT_TEST_LSSA = 1
        }

        public struct CORINFO_LLVM_JIT_TEST_INFO
        {
            public byte* ExpectedLssaAllocation;
        }

        [UnmanagedCallersOnly]
        private static void getJitTestInfo(IntPtr thisHandle, CorInfoLlvmJitTestKind kind, CORINFO_LLVM_JIT_TEST_INFO* pInfo)
        {
            *pInfo = default;

            CorInfoImpl _this = GetThis(thisHandle);
            if (_this._isFallbackBodyCompilation)
            {
                // The tests indicate failure by throwing BADCODE. Don't attempt to test anything "on the way out".
                return;
            }

            if ((kind & CorInfoLlvmJitTestKind.CORINFO_JIT_TEST_LSSA) != 0)
            {
                string expectedAllocation = null;
                if (_this._methodCodeNode.Method is EcmaMethod ecmaMethod &&
                    ecmaMethod.GetDecodedCustomAttribute("System.Runtime.JitTesting", "LSSATestAttribute") is { } attribute)
                {
                    expectedAllocation = (string)attribute.FixedArguments[0].Value;
                    pInfo->ExpectedLssaAllocation = (byte*)_this.GetPin(StringToUTF8(expectedAllocation));
                }
            }
        }

        public struct TypeDescriptor
        {
            public uint Size;
            public uint FieldCount;
            public CORINFO_FIELD_STRUCT_** Fields; // array of CORINFO_FIELD_STRUCT_*
            public uint HasSignificantPadding; // Change to a uint flags if we need more bools
        }

        [UnmanagedCallersOnly]
        public static void getTypeDescriptor(IntPtr thisHandle, CORINFO_CLASS_STRUCT_* inputType, TypeDescriptor* pTypeDescriptor)
        {
            var _this = GetThis(thisHandle);
            TypeDesc type = _this.HandleToObject(inputType);

            uint fieldCount = 0;
            foreach (var field in type.GetFields())
            {
                if (!field.IsStatic)
                {
                    fieldCount++;
                }
            }

            CORINFO_FIELD_STRUCT_*[] fields = new CORINFO_FIELD_STRUCT_*[fieldCount];

            fieldCount = 0;
            foreach (var field in type.GetFields())
            {
                if (!field.IsStatic)
                {
                    fields[fieldCount] = _this.ObjectToHandle(field);
                    fieldCount++;
                }
            }

            bool hasSignificantPadding = false;
            if (type is EcmaType ecmaType)
            {
                hasSignificantPadding = ecmaType.IsExplicitLayout || ecmaType.GetClassLayout().Size > 0;
            };

            pTypeDescriptor->Size = (uint)type.GetElementSize().AsInt;
            pTypeDescriptor->FieldCount = fieldCount;
            pTypeDescriptor->Fields = (CORINFO_FIELD_STRUCT_**)_this.GetPin(fields);
            pTypeDescriptor->HasSignificantPadding = hasSignificantPadding ? 1u : 0u;
        }

        [UnmanagedCallersOnly]
        private static CORINFO_LLVM_DEBUG_TYPE_HANDLE getDebugTypeForType(IntPtr thisHandle, CORINFO_CLASS_STRUCT_* typeHandle)
        {
            return GetThis(thisHandle).GetDebugTypeForType(typeHandle);
        }

        [UnmanagedCallersOnly]
        private static void getDebugInfoForDebugType(IntPtr thisHandle, CORINFO_LLVM_DEBUG_TYPE_HANDLE debugTypeHandle, CORINFO_LLVM_TYPE_DEBUG_INFO* pInfo)
        {
            GetThis(thisHandle).GetDebugInfoForDebugType(debugTypeHandle, pInfo);
        }

        [UnmanagedCallersOnly]
        private static void getDebugInfoForCurrentMethod(IntPtr thisHandle, CORINFO_LLVM_METHOD_DEBUG_INFO* pInfo)
        {
            GetThis(thisHandle).GetDebugInfoForMethod(pInfo);
        }

        // These enums must be kept in sync with their unmanaged versions in "jit/llvm.cpp".
        //
        private enum EEApiId
        {
            GetMangledMethodName,
            GetMangledSymbolName,
            GetMangledFilterFuncletName,
            GetSignatureForMethodSymbol,
            AddCodeReloc,
            GetPrimitiveTypeForTrivialWasmStruct,
            GetTypeDescriptor,
            GetAlternativeFunctionName,
            GetExternalMethodAccessor,
            GetDebugTypeForType,
            GetDebugInfoForDebugType,
            GetDebugInfoForCurrentMethod,
            GetSingleThreadedCompilationContext,
            GetExceptionHandlingModel,
            GetExceptionThrownVariable,
            GetExceptionHandlingTable,
            GetJitTestInfo,
            Count
        }

        [DllImport(JitLibrary)]
        private static extern int registerLlvmCallbacks(void** jitImports, void** jitExports);

        private static void JitInitializeLlvm()
        {
            void** jitImports = stackalloc void*[(int)EEApiId.Count + 1];
            jitImports[(int)EEApiId.GetMangledMethodName] = (delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, byte*>)&getMangledMethodName;
            jitImports[(int)EEApiId.GetMangledSymbolName] = (delegate* unmanaged<IntPtr, void*, byte*>)&getMangledSymbolName;
            jitImports[(int)EEApiId.GetMangledFilterFuncletName] = (delegate* unmanaged<IntPtr, uint, byte*>)&getMangledFilterFuncletName;
            jitImports[(int)EEApiId.GetSignatureForMethodSymbol] = (delegate* unmanaged<IntPtr, void*, CORINFO_SIG_INFO*, int>)&getSignatureForMethodSymbol;
            jitImports[(int)EEApiId.AddCodeReloc] = (delegate* unmanaged<IntPtr, void*, void>)&addCodeReloc;
            jitImports[(int)EEApiId.GetPrimitiveTypeForTrivialWasmStruct] = (delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, CorInfoType>)&getPrimitiveTypeForTrivialWasmStruct;
            jitImports[(int)EEApiId.GetTypeDescriptor] = (delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, TypeDescriptor*, void>)&getTypeDescriptor;
            jitImports[(int)EEApiId.GetAlternativeFunctionName] = (delegate* unmanaged<IntPtr, byte*>)&getAlternativeFunctionName;
            jitImports[(int)EEApiId.GetExternalMethodAccessor] = (delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, TargetAbiType*, int, IntPtr>)&getExternalMethodAccessor;
            jitImports[(int)EEApiId.GetDebugTypeForType] = (delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, CORINFO_LLVM_DEBUG_TYPE_HANDLE>)&getDebugTypeForType;
            jitImports[(int)EEApiId.GetDebugInfoForDebugType] = (delegate* unmanaged<IntPtr, CORINFO_LLVM_DEBUG_TYPE_HANDLE, CORINFO_LLVM_TYPE_DEBUG_INFO*, void>)&getDebugInfoForDebugType;
            jitImports[(int)EEApiId.GetDebugInfoForCurrentMethod] = (delegate* unmanaged<IntPtr, CORINFO_LLVM_METHOD_DEBUG_INFO*, void>)&getDebugInfoForCurrentMethod;
            jitImports[(int)EEApiId.GetSingleThreadedCompilationContext] = (delegate* unmanaged<IntPtr, void*>)&getSingleThreadedCompilationContext;
            jitImports[(int)EEApiId.GetExceptionHandlingModel] = (delegate* unmanaged<IntPtr, CorInfoLlvmEHModel>)&getExceptionHandlingModel;
            jitImports[(int)EEApiId.GetExceptionThrownVariable] = (delegate* unmanaged<IntPtr, IntPtr>)&getExceptionThrownVariable;
            jitImports[(int)EEApiId.GetExceptionHandlingTable] = (delegate* unmanaged<IntPtr, CORINFO_LLVM_EH_CLAUSE*, int, IntPtr>)&getExceptionHandlingTable;
            jitImports[(int)EEApiId.GetJitTestInfo] = (delegate* unmanaged<IntPtr, CorInfoLlvmJitTestKind, CORINFO_LLVM_JIT_TEST_INFO*, void>)&getJitTestInfo;
            jitImports[(int)EEApiId.Count] = (void*)0x1234;

#if DEBUG
            for (int i = 0; i < (int)EEApiId.Count; i++)
            {
                Debug.Assert(jitImports[i] != null);
            }
#endif

            fixed (void** jitExports = s_jitExports)
            {
                // "registerLlvmCallbacks" returning zero means the Jit was built without LLVM support.
                if (registerLlvmCallbacks(jitImports, jitExports) != 0)
                {
                    Debug.Assert(jitExports[(int)CorJitApiId.CJAI_Count] == (void*)0x1234);
                }
            }
        }

        public void JitStartSingleThreadedCompilation(string outputFileName, string triple, string dataLayout)
        {
            fixed (byte* pOutputFileName = StringToUTF8(outputFileName), pTriple = StringToUTF8(triple), pDataLayout = StringToUTF8(dataLayout))
            {
                var pExport = (delegate* unmanaged<byte*, byte*, byte*, void*>)GetJitExport(CorJitApiId.CJAI_StartSingleThreadedCompilation);
                _pNativeContext = pExport(pOutputFileName, pTriple, pDataLayout);
            }
        }

        public void JitFinishSingleThreadedCompilation()
        {
            ((delegate* unmanaged<void*, void>)GetJitExport(CorJitApiId.CJAI_FinishSingleThreadedCompilation))(_pNativeContext);
        }

        internal static void* GetJitExport(CorJitApiId id) => s_jitExports[(int)id];
    }

    public enum TargetAbiType : byte
    {
        Void,
        Int32,
        Int64,
        Float,
        Double
    }

    public enum CorInfoLlvmEHModel
    {
        Cpp,
        Wasm,
        Emulated
    };
}
