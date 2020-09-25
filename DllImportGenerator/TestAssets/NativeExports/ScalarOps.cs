using System;
using System.Runtime.InteropServices;

namespace NativeExportsNE
{
    public unsafe class ScalarOps
    {
        [UnmanagedCallersOnly(EntryPoint = "sumi")]
        public static int Sum(int a, int b)
        {
            return a + b;
        }

        [UnmanagedCallersOnly(EntryPoint = "sumrefi")]
        public static void SumRef(int a, int* b)
        {
            *b += a;
        }
    }
}
