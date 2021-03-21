using System;
using System.Runtime.InteropServices;
using Internal.TypeSystem;

namespace Internal.JitInterface
{
    public unsafe sealed partial class CorInfoImpl
    {
        [UnmanagedCallersOnly]
        public static byte* getMangledMethodName(IntPtr thisHandle, CORINFO_METHOD_STRUCT_* ftn)
        {
            var _this = GetThis(thisHandle);

            MethodDesc method = _this.HandleToObject(ftn);

            return (byte*)_this.GetPin(_this._compilation.NameMangler.GetMangledMethodName(method).UnderlyingArray);
        }

        [DllImport(JitLibrary)]
        private extern static void registerLlvmCallbacks(IntPtr thisHandle, byte* outputFileName, byte* triple, byte* dataLayout, delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, byte*> getMangedMethodNamePtr);

        public void RegisterLlvmCallbacks(IntPtr corInfoPtr, string outputFileName, string triple, string dataLayout)
        {
            registerLlvmCallbacks(corInfoPtr, (byte*)GetPin(StringToUTF8(outputFileName)),
                (byte*)GetPin(StringToUTF8(triple)),
                (byte*)GetPin(StringToUTF8(dataLayout)),
                (delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, byte*>) &getMangledMethodName);
        }
    }
}
