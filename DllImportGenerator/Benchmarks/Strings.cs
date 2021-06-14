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
        private class EntryPoints
        {
            private const string ReturnLength = "return_length";
            private const string ReverseReturn = "reverse_return";
            private const string ReverseOut = "reverse_out";
            private const string ReverseInplace = "reverse_inplace_ref";
            private const string ReverseReplace = "reverse_replace_ref";

            private const string UShortSuffix = "_ushort";
            private const string ByteSuffix = "_byte";

            public class Byte
            {
                public const string ReturnLength = EntryPoints.ReturnLength + ByteSuffix;
                public const string ReverseReturn = EntryPoints.ReverseReturn + ByteSuffix;
                public const string ReverseOut = EntryPoints.ReverseOut + ByteSuffix;
                public const string ReverseInplace = EntryPoints.ReverseInplace + ByteSuffix;
                public const string ReverseReplace = EntryPoints.ReverseReplace + ByteSuffix;
            }

            public class UShort
            {
                public const string ReturnLength = EntryPoints.ReturnLength + UShortSuffix;
                public const string ReverseReturn = EntryPoints.ReverseReturn + UShortSuffix;
                public const string ReverseOut = EntryPoints.ReverseOut + UShortSuffix;
                public const string ReverseInplace = EntryPoints.ReverseInplace + UShortSuffix;
                public const string ReverseReplace = EntryPoints.ReverseReplace + UShortSuffix;
            }
        }

        public partial class Unicode
        {
            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReturnLength, CharSet = CharSet.Unicode)]
            public static partial int ReturnLength(string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseReturn, CharSet = CharSet.Unicode)]
            public static partial string Reverse_Return(string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseOut, CharSet = CharSet.Unicode)]
            public static partial void Reverse_Out(string s, out string ret);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseInplace, CharSet = CharSet.Unicode)]
            public static partial void Reverse_Ref(ref string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseInplace, CharSet = CharSet.Unicode)]
            public static partial void Reverse_In(in string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseReplace, CharSet = CharSet.Unicode)]
            public static partial void Reverse_Replace_Ref(ref string s);
        }

        public partial class LPTStr
        {
            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReturnLength)]
            public static partial int ReturnLength([MarshalAs(UnmanagedType.LPTStr)] string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReturnLength, CharSet = CharSet.None)]
            public static partial int ReturnLength_IgnoreCharSet([MarshalAs(UnmanagedType.LPTStr)] string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseReturn)]
            [return: MarshalAs(UnmanagedType.LPTStr)]
            public static partial string Reverse_Return([MarshalAs(UnmanagedType.LPTStr)] string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseOut)]
            public static partial void Reverse_Out([MarshalAs(UnmanagedType.LPTStr)] string s, [MarshalAs(UnmanagedType.LPTStr)] out string ret);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseInplace)]
            public static partial void Reverse_Ref([MarshalAs(UnmanagedType.LPTStr)] ref string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseInplace)]
            public static partial void Reverse_In([MarshalAs(UnmanagedType.LPTStr)] in string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseInplace)]
            public static partial void Reverse_Replace_Ref([MarshalAs(UnmanagedType.LPTStr)] ref string s);
        }

        public partial class LPWStr
        {
            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReturnLength)]
            public static partial int ReturnLength([MarshalAs(UnmanagedType.LPWStr)] string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReturnLength, CharSet = CharSet.None)]
            public static partial int ReturnLength_IgnoreCharSet([MarshalAs(UnmanagedType.LPWStr)] string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseReturn)]
            [return: MarshalAs(UnmanagedType.LPWStr)]
            public static partial string Reverse_Return([MarshalAs(UnmanagedType.LPWStr)] string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseOut)]
            public static partial void Reverse_Out([MarshalAs(UnmanagedType.LPWStr)] string s, [MarshalAs(UnmanagedType.LPWStr)] out string ret);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseInplace)]
            public static partial void Reverse_Ref([MarshalAs(UnmanagedType.LPWStr)] ref string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseInplace)]
            public static partial void Reverse_In([MarshalAs(UnmanagedType.LPWStr)] in string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseInplace)]
            public static partial void Reverse_Replace_Ref([MarshalAs(UnmanagedType.LPWStr)] ref string s);
        }

        public partial class LPUTF8Str
        {
            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReturnLength)]
            public static partial int ReturnLength([MarshalAs(UnmanagedType.LPUTF8Str)] string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReturnLength, CharSet = CharSet.None)]
            public static partial int ReturnLength_IgnoreCharSet([MarshalAs(UnmanagedType.LPUTF8Str)] string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseReturn)]
            [return: MarshalAs(UnmanagedType.LPUTF8Str)]
            public static partial string Reverse_Return([MarshalAs(UnmanagedType.LPUTF8Str)] string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseOut)]
            public static partial void Reverse_Out([MarshalAs(UnmanagedType.LPUTF8Str)] string s, [MarshalAs(UnmanagedType.LPUTF8Str)] out string ret);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseInplace)]
            public static partial void Reverse_In([MarshalAs(UnmanagedType.LPUTF8Str)] in string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseInplace)]
            public static partial void Reverse_Ref([MarshalAs(UnmanagedType.LPUTF8Str)] ref string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseInplace)]
            public static partial void Reverse_Replace_Ref([MarshalAs(UnmanagedType.LPUTF8Str)] ref string s);
        }

        public partial class Ansi
        {
            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReturnLength, CharSet = CharSet.Ansi)]
            public static partial int ReturnLength(string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseReturn, CharSet = CharSet.Ansi)]
            public static partial string Reverse_Return(string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseOut, CharSet = CharSet.Ansi)]
            public static partial void Reverse_Out(string s, out string ret);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseInplace, CharSet = CharSet.Ansi)]
            public static partial void Reverse_Ref(ref string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseInplace, CharSet = CharSet.Ansi)]
            public static partial void Reverse_In(in string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseInplace, CharSet = CharSet.Ansi)]
            public static partial void Reverse_Replace_Ref(ref string s);
        }

        public partial class LPStr
        {
            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReturnLength)]
            public static partial int ReturnLength([MarshalAs(UnmanagedType.LPStr)] string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReturnLength, CharSet = CharSet.None)]
            public static partial int ReturnLength_IgnoreCharSet([MarshalAs(UnmanagedType.LPStr)] string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseReturn)]
            [return: MarshalAs(UnmanagedType.LPStr)]
            public static partial string Reverse_Return([MarshalAs(UnmanagedType.LPStr)] string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseOut)]
            public static partial void Reverse_Out([MarshalAs(UnmanagedType.LPStr)] string s, [MarshalAs(UnmanagedType.LPStr)] out string ret);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseInplace)]
            public static partial void Reverse_Ref([MarshalAs(UnmanagedType.LPStr)] ref string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseInplace)]
            public static partial void Reverse_In([MarshalAs(UnmanagedType.LPStr)] in string s);

            [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseInplace)]
            public static partial void Reverse_Replace_Ref([MarshalAs(UnmanagedType.LPStr)] ref string s);
        }

        public partial class Auto
        {
            public partial class Unix
            {
                [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReturnLength, CharSet = CharSet.Auto)]
                public static partial int ReturnLength(string s);

                [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseReturn, CharSet = CharSet.Auto)]
                public static partial string Reverse_Return(string s);

                [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseOut, CharSet = CharSet.Auto)]
                public static partial void Reverse_Out(string s, out string ret);

                [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseInplace, CharSet = CharSet.Auto)]
                public static partial void Reverse_Ref(ref string s);

                [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseInplace, CharSet = CharSet.Auto)]
                public static partial void Reverse_In(in string s);

                [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.Byte.ReverseInplace, CharSet = CharSet.Auto)]
                public static partial void Reverse_Replace_Ref(ref string s);
            }

            public partial class Windows
            {
                [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReturnLength, CharSet = CharSet.Auto)]
                public static partial int ReturnLength(string s);

                [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseReturn, CharSet = CharSet.Auto)]
                public static partial string Reverse_Return(string s);

                [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseOut, CharSet = CharSet.Auto)]
                public static partial void Reverse_Out(string s, out string ret);

                [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseInplace, CharSet = CharSet.Auto)]
                public static partial void Reverse_Ref(ref string s);

                [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseInplace, CharSet = CharSet.Auto)]
                public static partial void Reverse_In(in string s);

                [GeneratedDllImport(NativeExportsNE_Binary, EntryPoint = EntryPoints.UShort.ReverseInplace, CharSet = CharSet.Auto)]
                public static partial void Reverse_Replace_Ref(ref string s);
            }
        }
    }

    public class Strings
    {
        public static IEnumerable<object> UnicodeStrings{ get; } = new []
        {
            "ABCdef 123$%^",
            "🍜 !! 🍜 !!",
            "🌲 木 🔥 火 🌾 土 🛡 金 🌊 水",
            "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed vitae posuere mauris, sed ultrices leo. Suspendisse potenti. Mauris enim enim, blandit tincidunt consequat in, varius sit amet neque. Morbi eget porttitor ex. Duis mattis aliquet ante quis imperdiet. Duis sit.",
            string.Empty,
            null,
        };

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public int StringByValue_CharSetUnicode(string str)
        {
            return NativeExportsNE.Unicode.ReturnLength(str);
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public int StringByValue_LPWStr(string str)
        {
            return NativeExportsNE.LPWStr.ReturnLength(str);
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public int StringByValue_LPTStr(string str)
        {
            return NativeExportsNE.LPTStr.ReturnLength(str);
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public int StringByValue_LPUTF8Str(string str)
        {
            return NativeExportsNE.LPUTF8Str.ReturnLength(str);
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public int StringByValue_LPStr(string str)
        {
            return NativeExportsNE.LPStr.ReturnLength(str);
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public int StringByValue_CharSetAnsi(string str)
        {
            return NativeExportsNE.Ansi.ReturnLength(str);
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public int StringByValue_Auto(string str)
        {
            if (OperatingSystem.IsWindows())
            {
                return NativeExportsNE.Auto.Windows.ReturnLength(str);
            }
            else
            {
                return NativeExportsNE.Auto.Unix.ReturnLength(str);
            }
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public string StringReturn_CharSetUnicode(string str)
        {
            return NativeExportsNE.Unicode.Reverse_Return(str);
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public string StringReturn_LPWStr(string str)
        {
            return NativeExportsNE.LPWStr.Reverse_Return(str);
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public string StringReturn_LPTStr(string str)
        {
            return NativeExportsNE.LPTStr.Reverse_Return(str);
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public string StringReturn_LPUTF8Str(string str)
        {
            return NativeExportsNE.LPUTF8Str.Reverse_Return(str);
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public string StringReturn_LPStr(string str)
        {
            return NativeExportsNE.LPStr.Reverse_Return(str);
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public string StringReturn_CharSetAnsi(string str)
        {
            return NativeExportsNE.Ansi.Reverse_Return(str);
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public string StringReturn_Auto(string str)
        {
            if (OperatingSystem.IsWindows())
            {
                return NativeExportsNE.Auto.Windows.Reverse_Return(str);
            }
            else
            {
                return NativeExportsNE.Auto.Unix.Reverse_Return(str);
            }
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public string StringByRef_CharSetUnicode(string str)
        {
            NativeExportsNE.Unicode.Reverse_Replace_Ref(ref str);
            return str;
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public string StringByRef_LPWStr(string str)
        {
            NativeExportsNE.LPWStr.Reverse_Replace_Ref(ref str);
            return str;
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public string StringByRef_LPTStr(string str)
        {
            NativeExportsNE.LPTStr.Reverse_Replace_Ref(ref str);
            return str;
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public string StringByRef_LPUTF8Str(string str)
        {
            NativeExportsNE.LPUTF8Str.Reverse_Replace_Ref(ref str);
            return str;
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public string StringByRef_LPStr(string str)
        {
            NativeExportsNE.LPStr.Reverse_Replace_Ref(ref str);
            return str;
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public string StringByRef_CharSetAnsi(string str)
        {
            NativeExportsNE.Ansi.Reverse_Replace_Ref(ref str);
            return str;
        }

        [Benchmark]
        [ArgumentsSource(nameof(UnicodeStrings))]
        public string StringByRef_Auto(string str)
        {
            if (OperatingSystem.IsWindows())
            {
                NativeExportsNE.Auto.Windows.Reverse_Replace_Ref(ref str);
            }
            else
            {
                NativeExportsNE.Auto.Windows.Reverse_Replace_Ref(ref str);
            }
            return str;
        }
    }
}