using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Demo
{
    partial class NativeExportsNE
    {
        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "sumi")]
        public static partial int Sum(int a, int b);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "sumrefi")]
        public static partial void Sum(int a, ref int b);
    }

    unsafe class Program
    {
        static void Main(string[] args)
        {
            int a = 12;
            int b = 13;
            int c = NativeExportsNE.Sum(a, b);
            Console.WriteLine($"{a} + {b} = {c}");

            c = b;
            NativeExportsNE.Sum(a, ref c);
            Console.WriteLine($"{a} + {b} = {c}");
        }
    }
}
