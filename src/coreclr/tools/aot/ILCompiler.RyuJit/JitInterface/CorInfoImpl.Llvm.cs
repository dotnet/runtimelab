using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using ILCompiler;
using ILCompiler.DependencyAnalysis;
using Internal.IL;
using Internal.Text;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace Internal.JitInterface
{
    public unsafe sealed partial class CorInfoImpl
    {
        static List<IntPtr> _allocedMemory = new List<IntPtr>();

        private Dictionary<IntPtr, TypeDescriptor> typeDescriptorDict = new Dictionary<IntPtr, TypeDescriptor>();

        [DllImport(JitLibrary)]
        private extern static void jitShutdown([MarshalAs(UnmanagedType.I1)] bool processIsTerminating);

        [UnmanagedCallersOnly]
        public static void addCodeReloc(IntPtr thisHandle, void* handle)
        {
            var _this = GetThis(thisHandle);
            var obj = _this.HandleToObject((IntPtr)handle);

            AddCodeRelocImpl(_this, obj);
        }

        private static void AddCodeRelocImpl(CorInfoImpl _this, object obj)
        {
            ISymbolNode node;
            if (obj is ISymbolNode symbolNode)
            {
                node = symbolNode;
            }
            else
            {
                node = _this._compilation.NodeFactory.MethodEntrypoint((MethodDesc)obj);
            }

            _this._codeRelocs.Add(new Relocation(RelocType.IMAGE_REL_BASED_REL32, 0, node));
            var helperNode = node as ReadyToRunHelperNode;
            if (helperNode != null)
            {
                if (helperNode.Id == ReadyToRunHelperId.VirtualCall)
                {
                    MethodDesc virtualCallTarget = (MethodDesc)helperNode.Target;
                    _this._codeRelocs.Add(new Relocation(RelocType.IMAGE_REL_BASED_REL32, 0,
                        _this._compilation.NodeFactory.MethodEntrypoint(virtualCallTarget)));
                    return;
                }

                MetadataType target = (MetadataType)helperNode.Target;
                switch (helperNode.Id)
                {
                    case ReadyToRunHelperId.GetGCStaticBase:
                        _this._codeRelocs.Add(new Relocation(RelocType.IMAGE_REL_BASED_REL32, 0,
                            _this._compilation.NodeFactory.TypeGCStaticsSymbol(target)));
                        if (_this._compilation.HasLazyStaticConstructor(target))
                        {
                            var nonGcStaticSymbolForGCStaticBase = _this._compilation.NodeFactory.TypeNonGCStaticsSymbol(target);
                            _this._codeRelocs.Add(new Relocation(RelocType.IMAGE_REL_BASED_REL32, 0, nonGcStaticSymbolForGCStaticBase));
                            _this.AddOrReturnGlobalSymbol(nonGcStaticSymbolForGCStaticBase, _this._compilation.NameMangler);
                        }

                        break;
                    case ReadyToRunHelperId.GetNonGCStaticBase:
                        var nonGcStaticSymbol = _this._compilation.NodeFactory.TypeNonGCStaticsSymbol(target);
                        _this._codeRelocs.Add(new Relocation(RelocType.IMAGE_REL_BASED_REL32, 0, nonGcStaticSymbol));
                        _this.AddOrReturnGlobalSymbol(nonGcStaticSymbol, _this._compilation.NameMangler);
                        break;
                    case ReadyToRunHelperId.GetThreadStaticBase:
                        _this._codeRelocs.Add(new Relocation(RelocType.IMAGE_REL_BASED_REL32, 0,
                            _this._compilation.NodeFactory.TypeThreadStaticIndex(target)));
                        _this._codeRelocs.Add(new Relocation(RelocType.IMAGE_REL_BASED_REL32, 0,
                            _this._compilation.NodeFactory.TypeNonGCStaticsSymbol(target)));
                        var nonGcStaticSymbolForGCStaticBase2 = _this._compilation.NodeFactory.TypeNonGCStaticsSymbol(target);
                        _this.AddOrReturnGlobalSymbol(nonGcStaticSymbolForGCStaticBase2, _this._compilation.NameMangler);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                return;
            }

            if (node is FrozenStringNode frozenStringNode)
            {
                _this.AddOrReturnGlobalSymbol(frozenStringNode, _this._compilation.NameMangler);
            }
            else if (node is EETypeNode eeTypeNode)
            {
                _this.AddOrReturnGlobalSymbol(eeTypeNode, _this._compilation.NameMangler);
            }
        }

        // so the char* in cpp is terminated
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
        public static byte* getSymbolMangledName(IntPtr thisHandle, void* handle)
        {
            var _this = GetThis(thisHandle);

            var node = (ISymbolNode)_this.HandleToObject((IntPtr)handle);
            Utf8StringBuilder sb = new Utf8StringBuilder();
            node.AppendMangledName(_this._compilation.NameMangler, sb);
            if (node is FrozenStringNode || node is EETypeNode)
            {
                sb.Append("___SYMBOL");
            }

            sb.Append("\0");
            return (byte*)_this.GetPin(sb.UnderlyingArray);
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
            AddCodeRelocImpl(_this, dispatchMethod);

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

            if (fullPath == null) return null;

            return (byte*)_this.GetPin(StringToUTF8(fullPath));
        }

        [UnmanagedCallersOnly]
        public static uint firstSequencePointLineNumber(IntPtr thisHandle)
        {
            var _this = GetThis(thisHandle);

            return (uint)_this.GetSequencePoint(0).LineNumber;
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
        public static uint padOffset(IntPtr thisHandle, CORINFO_CLASS_STRUCT_* structHnd, uint ilOffset)
        {
            var _this = GetThis(thisHandle);
            TypeDesc typeDesc = _this.HandleToObject(structHnd);

            return (uint)_this._compilation.PadOffset(typeDesc, ilOffset);
        }

        [UnmanagedCallersOnly]
        public static CorInfoTypeWithMod getArgTypeIncludingParameterized(IntPtr thisHandle, CORINFO_SIG_INFO* sig, CORINFO_ARG_LIST_STRUCT_* args, CORINFO_CLASS_STRUCT_** vcTypeRet)
        {
            var _this = GetThis(thisHandle);

            int index = (int)args;
            Object sigObj = _this.HandleToObject((IntPtr)sig->methodSignature);

            MethodSignature methodSig = sigObj as MethodSignature;
            if (methodSig != null)
            {
                TypeDesc type = methodSig[index];

                CorInfoType corInfoType = _this.asCorInfoType(type, vcTypeRet);
                if (type.IsParameterizedType)
                {
                    *vcTypeRet = _this.ObjectToHandle(type);
                }

                return (CorInfoTypeWithMod)corInfoType;
            }
            else
            {
                LocalVariableDefinition[] locals = (LocalVariableDefinition[])sigObj;
                TypeDesc type = locals[index].Type;

                CorInfoType corInfoType = _this.asCorInfoType(type, vcTypeRet);

                return (CorInfoTypeWithMod)corInfoType | (locals[index].IsPinned ? CorInfoTypeWithMod.CORINFO_TYPE_MOD_PINNED : 0);
            }
        }

        [UnmanagedCallersOnly]
        public static CorInfoTypeWithMod getParameterType(IntPtr thisHandle, CORINFO_CLASS_STRUCT_* inputType, CORINFO_CLASS_STRUCT_** vcTypeParameter)
        {
            var _this = GetThis(thisHandle);

            TypeDesc type = _this.HandleToObject(inputType);

            *vcTypeParameter = null;
            CorInfoType corInfoType = CorInfoType.CORINFO_TYPE_VOID;
            if (type.IsParameterizedType)
            {
                TypeDesc parameterType = type.GetParameterType();
                *vcTypeParameter = _this.ObjectToHandle(parameterType);
                corInfoType = _this.asCorInfoType(parameterType, vcTypeParameter);
            }

            return (CorInfoTypeWithMod)corInfoType;
        }

        [UnmanagedCallersOnly]
        public static uint getInstanceFieldAlignment(IntPtr thisHandle, CORINFO_CLASS_STRUCT_* cls)
        {
            var _this = GetThis(thisHandle);
            DefType type = (DefType)_this.HandleToObject(cls);

            return (uint)type.InstanceFieldAlignment.AsInt;
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
            _allocedMemory.Add(fieldArray);

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

        // Must be kept in sync with the unmanaged version in "jit/llvm.cpp".
        //
        enum EEApiId
        {
            GetMangledMethodName,
            GetSymbolMangledName,
            GetEHDispatchFunctionName,
            GetTypeName,
            AddCodeReloc,
            IsRuntimeImport,
            GetDocumentFileName,
            FirstSequencePointLineNumber,
            GetOffsetLineNumber,
            StructIsWrappedPrimitive,
            PadOffset,
            GetArgTypeIncludingParameterized,
            GetParameterType,
            GetTypeDescriptor,
            GetInstanceFieldAlignment,
            Count
        }

        [DllImport(JitLibrary)]
        private extern static void registerLlvmCallbacks(IntPtr thisHandle, byte* outputFileName, byte* triple, byte* dataLayout, void** callbacks);

        public void RegisterLlvmCallbacks(IntPtr corInfoPtr, string outputFileName, string triple, string dataLayout)
        {
            void** callbacks = stackalloc void*[(int)EEApiId.Count + 1];
            callbacks[(int)EEApiId.GetMangledMethodName] = (delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, byte*>)&getMangledMethodName;
            callbacks[(int)EEApiId.GetSymbolMangledName] = (delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, byte*>)&getSymbolMangledName;
            callbacks[(int)EEApiId.GetEHDispatchFunctionName] = (delegate* unmanaged<IntPtr, CORINFO_EH_CLAUSE_FLAGS, byte*>)&getEHDispatchFunctionName;
            callbacks[(int)EEApiId.GetTypeName] = (delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, byte*>)&getTypeName;
            callbacks[(int)EEApiId.AddCodeReloc] = (delegate* unmanaged<IntPtr, void*, void>)&addCodeReloc;
            callbacks[(int)EEApiId.IsRuntimeImport] = (delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, uint>)&isRuntimeImport;
            callbacks[(int)EEApiId.GetDocumentFileName] = (delegate* unmanaged<IntPtr, byte*>)&getDocumentFileName;
            callbacks[(int)EEApiId.FirstSequencePointLineNumber] = (delegate* unmanaged<IntPtr, uint>)&firstSequencePointLineNumber;
            callbacks[(int)EEApiId.GetOffsetLineNumber] = (delegate* unmanaged<IntPtr, uint, uint>)&getOffsetLineNumber;
            callbacks[(int)EEApiId.StructIsWrappedPrimitive] = (delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, CorInfoType, uint>)&structIsWrappedPrimitive;
            callbacks[(int)EEApiId.PadOffset] = (delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, uint, uint>)&padOffset;
            callbacks[(int)EEApiId.GetArgTypeIncludingParameterized] = (delegate* unmanaged<IntPtr, CORINFO_SIG_INFO*, CORINFO_ARG_LIST_STRUCT_*, CORINFO_CLASS_STRUCT_**, CorInfoTypeWithMod>)&getArgTypeIncludingParameterized;
            callbacks[(int)EEApiId.GetParameterType] = (delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, CORINFO_CLASS_STRUCT_**, CorInfoTypeWithMod>)&getParameterType;
            callbacks[(int)EEApiId.GetTypeDescriptor] = (delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, TypeDescriptor>)&getTypeDescriptor;
            callbacks[(int)EEApiId.GetInstanceFieldAlignment] = (delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, uint>)&getInstanceFieldAlignment;
            callbacks[(int)EEApiId.Count] = (void*)0x1234;

#if DEBUG
            for (int i = 0; i < (int)EEApiId.Count; i++)
            {
                Debug.Assert(callbacks[i] != null);
            }
#endif

            registerLlvmCallbacks(corInfoPtr, (byte*)GetPin(StringToUTF8(outputFileName)),
                (byte*)GetPin(StringToUTF8(triple)),
                (byte*)GetPin(StringToUTF8(dataLayout)),
                callbacks
            );
        }

        void AddOrReturnGlobalSymbol(ISymbolNode symbolNode, NameMangler nameMangler)
        {
            _compilation.AddOrReturnGlobalSymbol(symbolNode, nameMangler);
        }

        public static void FreeUnmanagedResources()
        {
            foreach (var ptr in _allocedMemory)
            {
                Marshal.FreeHGlobal(ptr);
            }

            _allocedMemory.Clear();
        }
    }
}
