using System;
using System.Runtime.InteropServices;
using ILCompiler;
using ILCompiler.DependencyAnalysis;
using Internal.Text;
using Internal.TypeSystem;

namespace Internal.JitInterface
{
    public unsafe sealed partial class CorInfoImpl
    {
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

        [UnmanagedCallersOnly]
        public static byte* getMangledMethodName(IntPtr thisHandle, CORINFO_METHOD_STRUCT_* ftn)
        {
            var _this = GetThis(thisHandle);

            MethodDesc method = _this.HandleToObject(ftn);

            return (byte*)_this.GetPin(_this._compilation.NameMangler.GetMangledMethodName(method).UnderlyingArray);
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
            return (byte*)_this.GetPin(sb.UnderlyingArray);
        }

        [UnmanagedCallersOnly]
        public static bool isRuntimeImport(IntPtr thisHandle, CORINFO_METHOD_STRUCT_* ftn)
        {
            var _this = GetThis(thisHandle);

            MethodDesc method = _this.HandleToObject(ftn);

            return method.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute");
        }

        [DllImport(JitLibrary)]
        private extern static void registerLlvmCallbacks(IntPtr thisHandle, byte* outputFileName, byte* triple, byte* dataLayout,
            delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, byte*> getMangedMethodNamePtr,
            delegate* unmanaged<IntPtr, void*, byte*> getSymbolMangledName,
            delegate* unmanaged<IntPtr, void*, void> addCodeReloc,
            delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, bool> isRuntimeImport
        );

        public void RegisterLlvmCallbacks(IntPtr corInfoPtr, string outputFileName, string triple, string dataLayout)
        {
            registerLlvmCallbacks(corInfoPtr, (byte*)GetPin(StringToUTF8(outputFileName)),
                (byte*)GetPin(StringToUTF8(triple)),
                (byte*)GetPin(StringToUTF8(dataLayout)),
                &getMangledMethodName,
                &getSymbolMangledName,
                &addCodeReloc,
                &isRuntimeImport
                );
        }

        void AddOrReturnGlobalSymbol(ISortableSymbolNode gcStaticSymbol, NameMangler nameMangler)
        {
            _compilation.AddOrReturnGlobalSymbol(gcStaticSymbol, nameMangler);
        }
    }
}
