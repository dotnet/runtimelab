using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    sealed unsafe partial class CorInfoImpl
    {
        private static readonly List<IntPtr> s_allocedMemory = new List<IntPtr>();
        private static readonly void*[] s_jitExports = new void*[(int)JitApiId.Count + 1];

        private Dictionary<IntPtr, TypeDescriptor> typeDescriptorDict = new Dictionary<IntPtr, TypeDescriptor>();

        [UnmanagedCallersOnly]
        public static void addCodeReloc(IntPtr thisHandle, void* handle)
        {
            var _this = GetThis(thisHandle);
            var obj = _this.HandleToObject((IntPtr)handle);

            AddCodeRelocImpl(_this, (ISymbolNode)obj);
        }

        private static void AddCodeRelocImpl(CorInfoImpl _this, ISymbolNode node)
        {
            _this._codeRelocs.Add(new Relocation(RelocType.IMAGE_REL_BASED_REL32, 0, node));
        }

        // So the char* in cpp is terminated.
        private static byte[] AppendNullByte(byte[] inputArray)
        {
            byte[] nullTerminated = new byte[inputArray.Length + 1];
            inputArray.CopyTo(nullTerminated, 0);
            nullTerminated[inputArray.Length] = 0;
            return nullTerminated;
        }

        [UnmanagedCallersOnly]
        public static byte* getMangledMethodName(IntPtr thisHandle, CORINFO_METHOD_STRUCT_* ftn)
        {
            var _this = GetThis(thisHandle);
            MethodDesc method = _this.HandleToObject(ftn);

            return GetMangledMethodNameImpl(_this, method);
        }

        private static byte* GetMangledMethodNameImpl(CorInfoImpl _this, MethodDesc method)
        {
            Utf8String mangledName = _this._compilation.NameMangler.GetMangledMethodName(method);
            return (byte*)_this.GetPin(AppendNullByte(mangledName.UnderlyingArray));
        }

        [UnmanagedCallersOnly]
        public static byte* getMangledSymbolName(IntPtr thisHandle, IntPtr symbolHandle)
        {
            var _this = GetThis(thisHandle);
            var node = (ISymbolNode)_this.HandleToObject(symbolHandle);

            Utf8StringBuilder sb = new Utf8StringBuilder();
            node.AppendMangledName(_this._compilation.NameMangler, sb);

            sb.Append("\0");
            return (byte*)_this.GetPin(sb.UnderlyingArray);
        }

        [UnmanagedCallersOnly]
        public static int getSignatureForMethodSymbol(IntPtr thisHandle, IntPtr symbolHandle, CORINFO_SIG_INFO* pSig)
        {
            var _this = GetThis(thisHandle);
            var node = (ISymbolNode)_this.HandleToObject(symbolHandle);

            if (node is IMethodNode { Offset: 0, Method: MethodDesc method })
            {
                _this.Get_CORINFO_SIG_INFO(method, pSig, scope: null);
                if (method.IsUnmanagedCallersOnly || node is RuntimeImportMethodNode)
                {
                    pSig->callConv |= CorInfoCallConv.CORINFO_CALLCONV_UNMANAGED;
                }

                return 1;
            }

            return 0;
        }

        [UnmanagedCallersOnly]
        public static byte* getEHDispatchFunctionName(IntPtr thisHandle, CORINFO_EH_CLAUSE_FLAGS handlerType)
        {
            var _this = GetThis(thisHandle);
            string dispatchMethodName = handlerType switch
            {
                CORINFO_EH_CLAUSE_FLAGS.CORINFO_EH_CLAUSE_SAMETRY => "HandleExceptionWasmMutuallyProtectingCatches",
                CORINFO_EH_CLAUSE_FLAGS.CORINFO_EH_CLAUSE_NONE => "HandleExceptionWasmCatch",
                CORINFO_EH_CLAUSE_FLAGS.CORINFO_EH_CLAUSE_FILTER => "HandleExceptionWasmFilteredCatch",
                CORINFO_EH_CLAUSE_FLAGS.CORINFO_EH_CLAUSE_FINALLY or CORINFO_EH_CLAUSE_FLAGS.CORINFO_EH_CLAUSE_FAULT => "HandleExceptionWasmFault",
                _ => throw new NotSupportedException()
            };

            // TODO-LLVM: we are breaking the abstraction here. Compiler is not allowed to access methods from the
            // managed runtime directly and assume they are compiled into CoreLib. The dispatch routine should be
            // made into a RuntimeExport once we solve the issues around calling convention mismatch for them.
            MetadataType ehType = _this._compilation.TypeSystemContext.SystemModule.GetKnownType("System.Runtime", "EH");
            MethodDesc dispatchMethod = ehType.GetKnownMethod(dispatchMethodName, null);

            // Codegen is asking for the dispatcher; assume it'll use it.
            AddCodeRelocImpl(_this, _this._compilation.NodeFactory.MethodEntrypoint(dispatchMethod));

            return GetMangledMethodNameImpl(_this, dispatchMethod);
        }

        // IL backend does not use the mangled name.  The unmangled name is easier to read.
        [UnmanagedCallersOnly]
        public static byte* getTypeName(IntPtr thisHandle, CORINFO_CLASS_STRUCT_* structHnd)
        {
            var _this = GetThis(thisHandle);

            TypeDesc typeDesc = _this.HandleToObject(structHnd);

            Utf8StringBuilder sb = new Utf8StringBuilder();
            sb.Append(typeDesc.ToString());

            sb.Append("\0");
            return (byte*)_this.GetPin(sb.UnderlyingArray);
        }

        [UnmanagedCallersOnly]
        public static uint isRuntimeImport(IntPtr thisHandle, CORINFO_METHOD_STRUCT_* ftn)
        {
            var _this = GetThis(thisHandle);

            MethodDesc method = _this.HandleToObject(ftn);

            return method.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute") ? 1u : 0u; // bool is not blittable in .net5 so use uint, TODO: revert to bool for .net 6 (https://github.com/dotnet/runtime/issues/51170)
        }

        ILSequencePoint GetSequencePoint(uint offset)
        {
            var sequencePointsEnumerable = _debugInfo.GetSequencePoints();
            if (sequencePointsEnumerable == null) return default;

            ILSequencePoint curSequencePoint = default;

            foreach (var sequencePoint in sequencePointsEnumerable)
            {
                if (offset <= sequencePoint.Offset) // take the first sequence point in case we need to make a call to RhNewObject before the first matching sequence point
                {
                    curSequencePoint = sequencePoint;
                    break;
                }
                if (sequencePoint.Offset < offset)
                {
                    curSequencePoint = sequencePoint;
                }
            }
            return curSequencePoint;
        }

        [UnmanagedCallersOnly]
        public static byte* getDocumentFileName(IntPtr thisHandle)
        {
            var _this = GetThis(thisHandle);
            var curSequencePoint = _this.GetSequencePoint(0);
            string fullPath = curSequencePoint.Document;

            if (string.IsNullOrEmpty(fullPath))
            {
                return null;
            }

            return (byte*)_this.GetPin(StringToUTF8(fullPath));
        }

        [UnmanagedCallersOnly]
        public static uint getOffsetLineNumber(IntPtr thisHandle, uint ilOffset)
        {
            var _this = GetThis(thisHandle);

            return (uint)_this.GetSequencePoint(ilOffset).LineNumber;
        }

        [UnmanagedCallersOnly]
        public static uint structIsWrappedPrimitive(IntPtr thisHandle, CORINFO_CLASS_STRUCT_* structHnd, CorInfoType corInfoPrimitiveType)
        {
            var _this = GetThis(thisHandle);
            TypeDesc typeDesc = _this.HandleToObject(structHnd);

            TypeDesc primitiveTypeDesc;
            switch (corInfoPrimitiveType)
            {
                case CorInfoType.CORINFO_TYPE_FLOAT:
                    primitiveTypeDesc = _this._compilation.TypeSystemContext.GetWellKnownType(WellKnownType.Single);
                    break;
                case CorInfoType.CORINFO_TYPE_DOUBLE:
                    primitiveTypeDesc = _this._compilation.TypeSystemContext.GetWellKnownType(WellKnownType.Double);
                    break;
                default:
                    return 0u;
            }

            return _this._compilation.StructIsWrappedPrimitive(typeDesc, primitiveTypeDesc) ? 1u : 0u;
        }

        [UnmanagedCallersOnly]
        public static uint padOffset(IntPtr thisHandle, CORINFO_CLASS_STRUCT_* structHnd, uint atOffset)
        {
            var _this = GetThis(thisHandle);
            TypeDesc type = _this.HandleToObject(structHnd);

            return (uint)_this._compilation.PadOffset(type, (int)atOffset);
        }

        [UnmanagedCallersOnly]
        public static uint getInstanceFieldAlignment(IntPtr thisHandle, CORINFO_CLASS_STRUCT_* cls)
        {
            var _this = GetThis(thisHandle);
            DefType type = (DefType)_this.HandleToObject(cls);

            return (uint)type.InstanceFieldAlignment.AsInt;
        }

        [UnmanagedCallersOnly]
        public static byte* getAlternativeFunctionName(IntPtr thisHandle)
        {
            var _this = GetThis(thisHandle);
            IMethodNode methodNode = _this._methodCodeNode;
            RyuJitCompilation compilation = _this._compilation;

            string alternativeName = compilation.GetRuntimeExportManagedEntrypointName(methodNode.Method);
            if (alternativeName == null)
            {
                alternativeName = compilation.NodeFactory.GetSymbolAlternateName(methodNode);
            }
            if ((alternativeName == null) && methodNode.Method.IsUnmanagedCallersOnly)
            {
                // TODO-LLVM: delete once the IL backend is gone.
                alternativeName = methodNode.Method.Name;
            }

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
        private static IntPtr getLlvmHelperFuncEntrypoint(IntPtr thisHandle, CorInfoHelpLlvmFunc helperFunc)
        {
            CorInfoImpl _this = GetThis(thisHandle);
            NodeFactory factory = _this._compilation.NodeFactory;
            ISymbolNode helperFuncNode = helperFunc switch
            {
                CorInfoHelpLlvmFunc.CORINFO_HELP_LLVM_GET_OR_INIT_SHADOW_STACK_TOP => factory.ExternSymbol("RhpGetOrInitShadowStackTop"),
                CorInfoHelpLlvmFunc.CORINFO_HELP_LLVM_SET_SHADOW_STACK_TOP => factory.ExternSymbol("RhpSetShadowStackTop"),
                _ => throw new UnreachableException()
            };

            return _this.ObjectToHandle(helperFuncNode);
        }

        public struct TypeDescriptor
        {
            public uint FieldCount;
            public CORINFO_FIELD_STRUCT_** Fields; // array of CORINFO_FIELD_STRUCT_*
            public uint HasSignificantPadding; // Change to a uint flags if we need more bools
        }

        [UnmanagedCallersOnly]
        public static TypeDescriptor getTypeDescriptor(IntPtr thisHandle, CORINFO_CLASS_STRUCT_* inputType)
        {
            var _this = GetThis(thisHandle);

            if (_this.typeDescriptorDict.TryGetValue((IntPtr)inputType, out var typeDescriptor))
            {
                return typeDescriptor;
            }

            TypeDesc type = _this.HandleToObject(inputType);

            bool hasSignificantPadding = false;
            if (type is EcmaType ecmaType)
            {
                hasSignificantPadding = ecmaType.IsExplicitLayout || ecmaType.GetClassLayout().Size > 0;
            };

            uint fieldCount = 0;
            foreach (var field in type.GetFields())
            {
                if (!field.IsStatic)
                {
                    fieldCount++;
                }
            }

            //TODO-LLVM: change to NativeMemory.Alloc when upgraded to .net6
            IntPtr fieldArray = Marshal.AllocHGlobal((int)(sizeof(CORINFO_FIELD_STRUCT_*) * fieldCount));
            s_allocedMemory.Add(fieldArray);

            typeDescriptor = new TypeDescriptor
            {
                FieldCount = fieldCount,
                Fields = (CORINFO_FIELD_STRUCT_**)fieldArray,
                HasSignificantPadding = hasSignificantPadding ? 1u : 0
            };

            fieldCount = 0;
            foreach (var field in type.GetFields())
            {
                if (!field.IsStatic)
                {
                    typeDescriptor.Fields[fieldCount] = _this.ObjectToHandle(field);
                    fieldCount++;
                }
            }

            _this.typeDescriptorDict.Add((IntPtr)inputType, typeDescriptor);

            return typeDescriptor;
        }

        // These enums must be kept in sync with their unmanaged versions in "jit/llvm.cpp".
        //
        enum EEApiId
        {
            GetMangledMethodName,
            GetMangledSymbolName,
            GetSignatureForMethodSymbol,
            GetEHDispatchFunctionName,
            GetTypeName,
            AddCodeReloc,
            IsRuntimeImport,
            GetDocumentFileName,
            GetOffsetLineNumber,
            StructIsWrappedPrimitive,
            PadOffset,
            GetTypeDescriptor,
            GetInstanceFieldAlignment,
            GetAlternativeFunctionName,
            GetExternalMethodAccessor,
            GetLlvmHelperFuncEntrypoint,
            Count
        }

        enum JitApiId
        {
            StartThreadContextBoundCompilation,
            FinishThreadContextBoundCompilation,
            Count
        };

        enum CorInfoHelpLlvmFunc
        {
            CORINFO_HELP_LLVM_UNDEF = CorInfoHelpFunc.CORINFO_HELP_COUNT,
            CORINFO_HELP_LLVM_GET_OR_INIT_SHADOW_STACK_TOP,
            CORINFO_HELP_LLVM_SET_SHADOW_STACK_TOP,
            CORINFO_HELP_ANY_COUNT
        }

        [DllImport(JitLibrary)]
        private extern static void registerLlvmCallbacks(void** jitImports, void** jitExports);

        public static void JitStartCompilation()
        {
            void** jitImports = stackalloc void*[(int)EEApiId.Count + 1];
            jitImports[(int)EEApiId.GetMangledMethodName] = (delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, byte*>)&getMangledMethodName;
            jitImports[(int)EEApiId.GetMangledSymbolName] = (delegate* unmanaged<IntPtr, IntPtr, byte*>)&getMangledSymbolName;
            jitImports[(int)EEApiId.GetSignatureForMethodSymbol] = (delegate* unmanaged<IntPtr, IntPtr, CORINFO_SIG_INFO*, int>)&getSignatureForMethodSymbol;
            jitImports[(int)EEApiId.GetEHDispatchFunctionName] = (delegate* unmanaged<IntPtr, CORINFO_EH_CLAUSE_FLAGS, byte*>)&getEHDispatchFunctionName;
            jitImports[(int)EEApiId.GetTypeName] = (delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, byte*>)&getTypeName;
            jitImports[(int)EEApiId.AddCodeReloc] = (delegate* unmanaged<IntPtr, void*, void>)&addCodeReloc;
            jitImports[(int)EEApiId.IsRuntimeImport] = (delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, uint>)&isRuntimeImport;
            jitImports[(int)EEApiId.GetDocumentFileName] = (delegate* unmanaged<IntPtr, byte*>)&getDocumentFileName;
            jitImports[(int)EEApiId.GetOffsetLineNumber] = (delegate* unmanaged<IntPtr, uint, uint>)&getOffsetLineNumber;
            jitImports[(int)EEApiId.StructIsWrappedPrimitive] = (delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, CorInfoType, uint>)&structIsWrappedPrimitive;
            jitImports[(int)EEApiId.PadOffset] = (delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, uint, uint>)&padOffset;
            jitImports[(int)EEApiId.GetTypeDescriptor] = (delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, TypeDescriptor>)&getTypeDescriptor;
            jitImports[(int)EEApiId.GetInstanceFieldAlignment] = (delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, uint>)&getInstanceFieldAlignment;
            jitImports[(int)EEApiId.GetAlternativeFunctionName] = (delegate* unmanaged<IntPtr, byte*>)&getAlternativeFunctionName;
            jitImports[(int)EEApiId.GetExternalMethodAccessor] = (delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, TargetAbiType*, int, IntPtr>)&getExternalMethodAccessor;
            jitImports[(int)EEApiId.GetLlvmHelperFuncEntrypoint] = (delegate* unmanaged<IntPtr, CorInfoHelpLlvmFunc, IntPtr>)&getLlvmHelperFuncEntrypoint;
            jitImports[(int)EEApiId.Count] = (void*)0x1234;

#if DEBUG
            for (int i = 0; i < (int)EEApiId.Count; i++)
            {
                Debug.Assert(jitImports[i] != null);
            }
#endif

            fixed (void** jitExports = s_jitExports)
            {
                registerLlvmCallbacks(jitImports, jitExports);
                Debug.Assert(jitExports[(int)JitApiId.Count] == (void*)0x1234);
            }
        }

        public static void JitFinishCompilation()
        {
            foreach (var ptr in s_allocedMemory)
            {
                Marshal.FreeHGlobal(ptr);
            }

            s_allocedMemory.Clear();
        }

        public static void JitStartThreadContextBoundCompilation(string outputFileName, string triple, string dataLayout)
        {
            fixed (byte* pOutputFileName = StringToUTF8(outputFileName), pTriple = StringToUTF8(triple), pDataLayout = StringToUTF8(dataLayout))
            {
                var pExport = (delegate* unmanaged<byte*, byte*, byte*, void>)s_jitExports[(int)JitApiId.StartThreadContextBoundCompilation];
                pExport(pOutputFileName, pTriple, pDataLayout);
            }
        }

        public static void JitFinishThreadContextBoundCompilation()
        {
            ((delegate* unmanaged<void>)s_jitExports[(int)JitApiId.FinishThreadContextBoundCompilation])();
        }
    }

    public enum TargetAbiType : byte
    {
        Void,
        Int32,
        Int64,
        Float,
        Double
    }
}
