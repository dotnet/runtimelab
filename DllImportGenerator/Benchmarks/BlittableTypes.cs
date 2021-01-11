using BenchmarkDotNet.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Benchmarks
{
    partial class NativeExportsNE
    {
        public const string NativeExportsNE_Binary = "Microsoft.Interop.Tests." + nameof(NativeExportsNE);

        [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "sumi")]
        public static partial int Sum(int a, int b);

        [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "sumouti")]
        public static partial void Sum(int a, int b, out int c);

        [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "sumrefi")]
        public static partial void Sum(int a, ref int b);
    }

    public class BlittableTypes
    {
        [Benchmark]
        public int IntByValue()
        {
            return NativeExportsNE.Sum(3, 42);
        }

        [Benchmark]
        public int IntOutByRef()
        {
            NativeExportsNE.Sum(3, 42, out int result);
            return result;
        }
    }
}
