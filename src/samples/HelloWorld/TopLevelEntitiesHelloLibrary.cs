
using System;
using System.Runtime.InteropServices;
namespace HelloLibrary
{
    public class TopLevelEntities
    {
        public static void SayHello()
        {
            NativeMethodsForTopLevelEntities.PIfunc_SayHello();
        }
    }
    internal class NativeMethodsForTopLevelEntities
    {
        [DllImport("libHelloLibrary.dylib", EntryPoint = "$s12HelloLibrary03sayA0yyF")]
        internal static extern void PIfunc_SayHello();
    }
}