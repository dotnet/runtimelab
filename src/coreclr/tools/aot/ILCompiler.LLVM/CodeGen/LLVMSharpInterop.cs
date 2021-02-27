using LLVMSharp.Interop;

namespace Internal.IL
{
    internal class LLVMSharpInterop
    {
        ///
        /// Wrapper while waiting for https://github.com/microsoft/LLVMSharp/pull/144
        /// 
        internal static unsafe void DISetSubProgram(LLVMValueRef function, LLVMMetadataRef diFunction)
        {
            LLVM.SetSubprogram(function, diFunction);
        }
    }
}
