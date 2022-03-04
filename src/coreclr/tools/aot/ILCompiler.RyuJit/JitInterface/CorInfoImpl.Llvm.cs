using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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

        private MethodDebugInformation _debugInformation;
        private Dictionary<IntPtr, TypeDescriptor> typeDescriptorDict = new Dictionary<IntPtr, TypeDescriptor>();

        [DllImport(JitLibrary)]
        private extern static void jitShutdown([MarshalAs(UnmanagedType.I1)] bool processIsTerminating);

        [UnmanagedCallersOnly]
        public static void addCodeReloc(IntPtr thisHandle, void* handle)
        {
            var _this = GetThis(thisHandle);

            var node = (ISymbolNode)_this.HandleToObject((IntPtr)handle);
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

            return (byte*)_this.GetPin(AppendNullByte(_this._compilation.NameMangler.GetMangledMethodName(method).UnderlyingArray));
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
        public static byte* getSymbolMangledNameFromHelperTarget(IntPtr thisHandle, void* handle)
        {
            var _this = GetThis(thisHandle);

            var node = (ReadyToRunHelperNode)_this.HandleToObject((IntPtr)handle);
            var method = node.Target as MethodDesc;

            // Abstract methods must require a lookup so no point passing the abstract name back
            if (method.IsAbstract || method.IsVirtual) return null;

            Utf8StringBuilder sb = new Utf8StringBuilder();
            return (byte*)_this.GetPin(AppendNullByte(_this._compilation.NameMangler.GetMangledMethodName(method).UnderlyingArray));
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
            var sequencePointsEnumerable = _debugInformation.GetSequencePoints();
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

            // Replicate the logic in \ILToLLVMImporter.cs GetLLVMTypeForTypeDesc but without Linq
            // Build the TypeDescriptor with the largest fields at each offset,
            // removing overlapping fields - i.e. union or explicit layouts
            int typeSize = type.GetElementSize().AsInt;
            FieldAndOffset[] sparseFieldLayout = new FieldAndOffset[typeSize];
            for (int i = 0; i < typeSize; i++)
            {
                sparseFieldLayout[i] = FieldAndOffset.Invalid;
            }

            // Fill the sparse array with the type fields, when a larger field is found, overwrite that slot
            foreach (var field in type.GetFields())
            {
                if (!field.IsStatic)
                {
                    int offset = field.Offset.AsInt;

                    if (sparseFieldLayout[offset].Field == null || field.FieldType.GetElementSize().AsInt >
                        sparseFieldLayout[offset].Field.FieldType.GetElementSize().AsInt)
                    {
                        sparseFieldLayout[offset] = new FieldAndOffset(field, field.Offset);
                    }
                }
            }

            // Walk the sparseFieldLayout, clearing out any fields that are overlapped by a preceding field
            int currentFieldEndExclusive = -1;
            for (int i = 0; i < typeSize; i++)
            {
                if (i < currentFieldEndExclusive)
                {
                    sparseFieldLayout[i] = FieldAndOffset.Invalid;
                }
                else if (sparseFieldLayout[i].IsValid)
                {
                    currentFieldEndExclusive = i + sparseFieldLayout[i].Field.FieldType.GetElementSize().AsInt;
                }
            }

            // sparseFieldLayout should now contain just the fields from unions or overlapping explicit layouts,
            // that we want in the LLVM struct
            uint fieldCount = 0;
            foreach (var sparseField in sparseFieldLayout)
            {
                if (sparseField.IsValid)
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

            uint fieldIx = 0;
            foreach (FieldAndOffset sparseField in sparseFieldLayout)
            {
                if (sparseField.IsValid)
                {
                    typeDescriptor.Fields[fieldIx] = _this.ObjectToHandle(sparseField.Field);
                    fieldIx++;
                }
            }

            _this.typeDescriptorDict.Add((IntPtr)inputType, typeDescriptor);

            return typeDescriptor;
        }

        [DllImport(JitLibrary)]
        private extern static void registerLlvmCallbacks(IntPtr thisHandle, byte* outputFileName, byte* triple, byte* dataLayout,
            delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, byte*> getMangedMethodNamePtr,
            delegate* unmanaged<IntPtr, void*, byte*> getSymbolMangledName,
            delegate* unmanaged<IntPtr, void*, byte*> getSymbolMangledNameFromHelperTarget,
            delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, byte*> getTypeName,
            delegate* unmanaged<IntPtr, void*, void> addCodeReloc,
            delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, uint> isRuntimeImport,
            delegate* unmanaged<IntPtr, byte*> getDocumentFileName,
            delegate* unmanaged<IntPtr, uint> firstSequencePointLineNumber,
            delegate* unmanaged<IntPtr, uint, uint> getOffsetLineNumber,
            delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, CorInfoType, uint> structIsWrappedPrimitive,
            delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, uint, uint> padOffset,
            delegate* unmanaged<IntPtr, CORINFO_SIG_INFO*, CORINFO_ARG_LIST_STRUCT_*, CORINFO_CLASS_STRUCT_**, CorInfoTypeWithMod> getArgTypeIncludingParameterized,
            delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, CORINFO_CLASS_STRUCT_**, CorInfoTypeWithMod> getParameterType,
            delegate* unmanaged<IntPtr, CORINFO_CLASS_STRUCT_*, TypeDescriptor> getTypeDescriptor
            );

        public void RegisterLlvmCallbacks(IntPtr corInfoPtr, string outputFileName, string triple, string dataLayout)
        {
            registerLlvmCallbacks(corInfoPtr, (byte*)GetPin(StringToUTF8(outputFileName)),
                (byte*)GetPin(StringToUTF8(triple)),
                (byte*)GetPin(StringToUTF8(dataLayout)),
                &getMangledMethodName,
                &getSymbolMangledName,
                &getSymbolMangledNameFromHelperTarget,
                &getTypeName,
                &addCodeReloc,
                &isRuntimeImport,
                &getDocumentFileName,
                &firstSequencePointLineNumber,
                &getOffsetLineNumber,
                &structIsWrappedPrimitive,
                &padOffset,
                &getArgTypeIncludingParameterized,
                &getParameterType,
                &getTypeDescriptor
            );
        }

        public void InitialiseDebugInfo(MethodDesc method, MethodIL methodIL)
        {
            _debugInformation = _compilation.GetDebugInfo(methodIL);
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
