using BenchmarkDotNet.Attributes;
using Microsoft.Win32.SafeHandles;
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
        public partial class Arrays
        {
            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "sum_int_array")]
            public static partial int Sum(int[] values, int numValues);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "sum_int_array_ref")]
            public static partial int SumInArray(in int[] values, int numValues);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "duplicate_int_array")]
            public static partial void Duplicate([MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ref int[] values, int numValues);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "create_range_array")]
            [return:MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)]
            public static partial int[] CreateRange(int start, int end, out int numValues);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "create_range_array_out")]
            public static partial void CreateRange_Out(int start, int end, out int numValues, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] out int[] res);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "sum_string_lengths")]
            public static partial int SumStringLengths([MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] strArray);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "reverse_strings_replace")]
            public static partial void ReverseStrings_Ref([MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 1)] ref string[] strArray, out int numElements);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "reverse_strings_return")]
            [return: MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 1)]
            public static partial string[] ReverseStrings_Return([MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] strArray, out int numElements);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "reverse_strings_out")]
            public static partial void ReverseStrings_Out([MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] strArray, out int numElements, [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 1)] out string[] res);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "get_long_bytes")]
            [return:MarshalAs(UnmanagedType.LPArray, SizeConst = sizeof(long))]
            public static partial byte[] GetLongBytes(long l);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = "append_int_to_array")]
            public static partial void Append([MarshalAs(UnmanagedType.LPArray, SizeConst = 1, SizeParamIndex = 1)] ref int[] values, int numOriginalValues, int newValue);
        }
    }

    public class Arrays
    {
        public int[] IntArray => new[] { 1, 2, 3, 6, 243, 42 };

        public string[] StringArray => new[]
        {
            "ABCdef 123$%^",
            "🍜 !! 🍜 !!",
            "🌲 木 🔥 火 🌾 土 🛡 金 🌊 水",
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed vitae posuere mauris, sed ultrices leo. Suspendisse potenti. Mauris enim enim, blandit tincidunt consequat in, varius sit amet neque. Morbi eget porttitor ex. Duis mattis aliquet ante quis imperdiet. Duis sit.",
            string.Empty,
            null,
        };

        [Benchmark]
        public int BlittableArrayByValue()
        {
            int[] local = IntArray;
            return NativeExportsNE.Arrays.Sum(local, local.Length);
        }

        [Benchmark]
        public int NullArrayByValue()
        {
            return NativeExportsNE.Arrays.Sum(null, 0);
        }

        [Benchmark]
        public int ZeroLengthArrayByValue()
        {
            return NativeExportsNE.Arrays.Sum(Array.Empty<int>(), 0);
        }

        [Benchmark]
        public int[] BlittableArrayByRef()
        {
            int[] local = IntArray;
            NativeExportsNE.Arrays.Duplicate(ref local, local.Length);
            return local;
        }

        [Benchmark]
        public int[] BlittableArrayByRef_SizeParamIndex_SizeConst()
        {
            int[] local = IntArray;
            NativeExportsNE.Arrays.Append(ref local, local.Length, 17);
            return local;
        }

        [Benchmark]
        public int NonBlittableArrayByValue()
        {
            return NativeExportsNE.Arrays.SumStringLengths(StringArray);
        }

        [Benchmark]
        public string[] NonBlittableArrayByRef()
        {
            string[] local = StringArray;
            NativeExportsNE.Arrays.ReverseStrings_Ref(ref local, out _);
            return local;
        }
    }
}