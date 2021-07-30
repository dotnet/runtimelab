using System;
using System.Linq;
using System.Runtime.InteropServices;
using ILCompiler;
using ILCompiler.DependencyAnalysis;
using Internal.IL;
using Internal.Text;
using Internal.TypeSystem;

namespace Internal.JitInterface
{
    public unsafe sealed partial class CorInfoImpl
    {
        private MethodIL _methodIL;
        private MethodDebugInformation _debugInformation;

        [UnmanagedCallersOnly]
        public static void addCodeReloc(IntPtr thisHandle, void* handle)
        {
            var _this = GetThis(thisHandle);

            var node = (ISymbolNode)_this.HandleToObject((IntPtr)handle);
            _this._codeRelocs.Add(new Relocation(RelocType.IMAGE_REL_BASED_REL32, 0, node));
            var helperNode = node as ReadyToRunHelperNode;
            if (helperNode != null)
            {
                MetadataType target = (MetadataType)helperNode.Target;
                switch (helperNode.Id)
                {
                    case ReadyToRunHelperId.GetGCStaticBase:
                        _this._codeRelocs.Add(new Relocation(RelocType.IMAGE_REL_BASED_REL32, 0,
                            _this._compilation.NodeFactory.TypeGCStaticsSymbol(target)));
                        if (_this._compilation.HasLazyStaticConstructor(target))
                        {
                            var nonGcStaticSymbol = _this._compilation.NodeFactory.TypeNonGCStaticsSymbol(target);
                            _this._codeRelocs.Add(new Relocation(RelocType.IMAGE_REL_BASED_REL32, 0, nonGcStaticSymbol));
                            _this.AddOrReturnGlobalSymbol(nonGcStaticSymbol, _this._compilation.NameMangler);
                        }

                        break;
                    case ReadyToRunHelperId.GetNonGCStaticBase:
                        _this._codeRelocs.Add(new Relocation(RelocType.IMAGE_REL_BASED_REL32, 0,
                            _this._compilation.NodeFactory.TypeNonGCStaticsSymbol(target)));
                        break;
                    case ReadyToRunHelperId.GetThreadStaticBase:
                        _this._codeRelocs.Add(new Relocation(RelocType.IMAGE_REL_BASED_REL32, 0,
                            _this._compilation.NodeFactory.TypeThreadStaticsSymbol(target)));
                        break;
                    default:
                        throw new NotImplementedException();
                }
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
            if (node is FrozenStringNode)
            {
                sb.Append("___SYMBOL");
            }

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
            ILSequencePoint curSequencePoint = default;
            foreach (var sequencePoint in _debugInformation.GetSequencePoints() ?? Enumerable.Empty<ILSequencePoint>())
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

        [DllImport(JitLibrary)]
        private extern static void registerLlvmCallbacks(IntPtr thisHandle, byte* outputFileName, byte* triple, byte* dataLayout,
            delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, byte*> getMangedMethodNamePtr,
            delegate* unmanaged<IntPtr, void*, byte*> getSymbolMangledName,
            delegate* unmanaged<IntPtr, void*, void> addCodeReloc,
            delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, uint> isRuntimeImport,
            delegate* unmanaged<IntPtr, byte*> getDocumentFileName,
            delegate* unmanaged<IntPtr, uint> firstSequencePointLineNumber,
            delegate* unmanaged<IntPtr, uint, uint> getOffsetLineNumber
        );

        public void RegisterLlvmCallbacks(IntPtr corInfoPtr, string outputFileName, string triple, string dataLayout)
        {
            registerLlvmCallbacks(corInfoPtr, (byte*)GetPin(StringToUTF8(outputFileName)),
                (byte*)GetPin(StringToUTF8(triple)),
                (byte*)GetPin(StringToUTF8(dataLayout)),
                &getMangledMethodName,
                &getSymbolMangledName,
                &addCodeReloc,
                &isRuntimeImport,
                &getDocumentFileName,
                &firstSequencePointLineNumber,
                &getOffsetLineNumber
            );
        }

        public void InitialiseDebugInfo(MethodDesc method, MethodIL methodIL)
        {
            MethodIL uninstantiatiedMethodIL = methodIL.GetMethodILDefinition();
            if (methodIL != uninstantiatiedMethodIL)
            {
                MethodDesc sharedMethod = method.GetSharedRuntimeFormMethodTarget();
                _methodIL = new InstantiatedMethodIL(sharedMethod, uninstantiatiedMethodIL);
            }
            else
            {
                _methodIL = methodIL;
            }
            _debugInformation = _compilation.GetDebugInfo(_methodIL);
        }

        void AddOrReturnGlobalSymbol(ISortableSymbolNode gcStaticSymbol, NameMangler nameMangler)
        {
            _compilation.AddOrReturnGlobalSymbol(gcStaticSymbol, nameMangler);
        }
    }
}
