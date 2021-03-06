using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ILCompiler;
using Internal.TypeSystem;

namespace Internal.JitInterface
{
    public unsafe sealed partial class CorInfoImpl
    {
        [ThreadStatic]
        private static CorInfoImpl _thisStatic;

        [UnmanagedCallersOnly]
        public static byte* getMangledMethodName(IntPtr thisHandle, CORINFO_METHOD_STRUCT_* ftn)
        {
            //var _this = GetThis(thisHandle); // TODO: this doesn't work, but how does it cope anyway with this being moved by the GC?

            MethodDesc method = _thisStatic.HandleToObject(ftn);

            return (byte*)_thisStatic.GetPin(_thisStatic._compilation.NameMangler.GetMangledMethodName(method).UnderlyingArray);
        }

        [DllImport(JitLibrary)]
        private extern static void registerLlvmCallbacks(IntPtr thisHandle, byte* outputFileName, delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, byte*> getMangedMethodNamePtr);

        public void RegisterLlvmCallbacks(string outputFileName)
        {
            CorInfoImpl _this = this;
            _thisStatic = this;

            registerLlvmCallbacks((IntPtr)Unsafe.AsPointer(ref _this), (byte*)_thisStatic.GetPin(StringToUTF8(outputFileName)), (delegate* unmanaged<IntPtr, CORINFO_METHOD_STRUCT_*, byte*>) &getMangledMethodName);
        }
    }
}
