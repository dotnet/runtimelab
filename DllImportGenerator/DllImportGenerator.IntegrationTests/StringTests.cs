using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Xunit;

namespace DllImportGenerator.IntegrationTests
{
    partial class NativeExportsNE
    {
        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "ushort_return_length")]
        public static partial int ReturnLength_Default(string s);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "ushort_return_length", CharSet = CharSet.Unicode)]
        public static partial int ReturnLength_Unicode(string s);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "append_return_ushort")]
        public static partial string AppendStrings_ReturnAsDefault(string s1, string s2);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "append_return_ushort", CharSet = CharSet.Unicode)]
        public static partial string AppendStrings_ReturnAsUnicode(string s1, string s2);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "append_return_ushort")]
        [return: MarshalAs(UnmanagedType.LPWStr)]
        public static partial string AppendStrings_ReturnAsLPWStr([MarshalAs(UnmanagedType.LPWStr)] string s1, [MarshalAs(UnmanagedType.LPWStr)] string s2);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "append_return_ushort")]
        [return: MarshalAs(UnmanagedType.LPTStr)]
        public static partial string AppendStrings_ReturnAsLPTStr([MarshalAs(UnmanagedType.LPTStr)] string s1, [MarshalAs(UnmanagedType.LPTStr)] string s2);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "append_outushort", CharSet = CharSet.Unicode)]
        public static partial void AppendStrings_OutAsUnicode(string s1, string s2, out string ret);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "append_outushort")]
        public static partial void AppendStrings_OutAsLPWStr([MarshalAs(UnmanagedType.LPWStr)] string s1, [MarshalAs(UnmanagedType.LPWStr)] string s2, [MarshalAs(UnmanagedType.LPWStr)] out string ret);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "append_outushort")]
        public static partial void AppendStrings_OutAsLPTStr([MarshalAs(UnmanagedType.LPTStr)] string s1, [MarshalAs(UnmanagedType.LPTStr)] string s2, [MarshalAs(UnmanagedType.LPTStr)] out string ret);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "reverse_refushort", CharSet = CharSet.Unicode)]
        public static partial void ReverseString_InAsUnicode(in string s);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "reverse_refushort", CharSet = CharSet.Unicode)]
        public static partial void ReverseString_RefAsUnicode(ref string s);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "reverse_refushort")]
        public static partial void ReverseString_RefAsLPWStr([MarshalAs(UnmanagedType.LPWStr)] ref string s);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "reverse_refushort")]
        public static partial void ReverseString_RefAsLPTStr([MarshalAs(UnmanagedType.LPTStr)] ref string s);
    }

    public class StringTests
    {
        public static IEnumerable<object[]> UnicodeStrings() => new []
        {
            new object[] { "ABCdef 123$%^" },
            new object[] { "🍜 !! 🍜"},
            new object[] { "🌲 木 🔥 火 🌾 土 🛡 金 🌊 水" },
            new object[] { "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed vitae posuere mauris, sed ultrices leo. Suspendisse potenti. Mauris enim enim, blandit tincidunt consequat in, varius sit amet neque. Morbi eget porttitor ex. Duis mattis aliquet ante quis imperdiet. Duis sit." },
            new object[] { string.Empty },
            new object[] { null },
        };

        [Theory]
        [MemberData(nameof(UnicodeStrings))]
        public void UnicodeStringMarshalledAsExpected(string value)
        {
            int expectedLen = value != null ? value.Length : -1;
            Assert.Equal(expectedLen, NativeExportsNE.ReturnLength_Default(value));
            Assert.Equal(expectedLen, NativeExportsNE.ReturnLength_Unicode(value));
        }

        [Theory]
        [MemberData(nameof(UnicodeStrings))]
        public void UnicodeStringReturn(string value)
        {
            string expected = value + value;

            Assert.Equal(expected, NativeExportsNE.AppendStrings_ReturnAsDefault(value, value));
            Assert.Equal(expected, NativeExportsNE.AppendStrings_ReturnAsUnicode(value, value));
            Assert.Equal(expected, NativeExportsNE.AppendStrings_ReturnAsLPWStr(value, value));
            Assert.Equal(expected, NativeExportsNE.AppendStrings_ReturnAsLPTStr(value, value));

            string ret;
            NativeExportsNE.AppendStrings_OutAsUnicode(value, value, out ret);
            Assert.Equal(expected, ret);

            ret = null;
            NativeExportsNE.AppendStrings_OutAsLPWStr(value, value, out ret);
            Assert.Equal(expected, ret);

            ret = null;
            NativeExportsNE.AppendStrings_OutAsLPTStr(value, value, out ret);
            Assert.Equal(expected, ret);
        }

        [Theory]
        [MemberData(nameof(UnicodeStrings))]
        public void UnicodeStringByRef(string value)
        {
            string refValue = value;
            string expected = Reverse(value);

            NativeExportsNE.ReverseString_InAsUnicode(in refValue);
            Assert.Equal(value, refValue); // Should not be updated when using 'in'

            refValue = value;
            NativeExportsNE.ReverseString_RefAsUnicode(ref refValue);
            Assert.Equal(expected, refValue);

            refValue = value;
            NativeExportsNE.ReverseString_RefAsLPWStr(ref refValue);
            Assert.Equal(expected, refValue);

            refValue = value;
            NativeExportsNE.ReverseString_RefAsLPTStr(ref refValue);
            Assert.Equal(expected, refValue);
        }

        private static string Reverse(string s)
        {
            if (s == null)
                return null;

            // Simple reverse of the chars
            var chars = s.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }
    }
}
