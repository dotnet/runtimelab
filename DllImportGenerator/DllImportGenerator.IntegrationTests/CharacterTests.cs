using System.Runtime.InteropServices;

using Xunit;

namespace DllImportGenerator.IntegrationTests
{
    partial class NativeExportsNE
    {
        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "unicode_return_as_uint", CharSet = CharSet.Unicode)]
        public static partial uint ReturnUnicodeAsUInt(char input);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "char_return_as_uint", CharSet = CharSet.Unicode)]
        public static partial char ReturnUIntAsUnicode(uint input);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "char_return_as_refuint", CharSet = CharSet.Unicode)]
        public static partial void ReturnUIntAsRefUnicode(uint input, ref char res);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "char_return_as_refuint", CharSet = CharSet.Unicode)]
        public static partial void ReturnUIntAsOutUnicode(uint input, out char res);
    }

    public class CharacterTests
    {
        [Theory]
        [InlineData(new object[] { 'A', 0x41 })]
        [InlineData(new object[] { 'E', 0x45 })]
        [InlineData(new object[] { 'J', 0x4a })]
        [InlineData(new object[] { 'ß', 0xdf })]
        [InlineData(new object[] { '✅', 0x2705 })]
        [InlineData(new object[] { '鸟', 0x9e1f })]
        public void ValidateUnicodeCharIsMarshalledAsExpected(char value, uint expected)
        {
            Assert.Equal(expected, NativeExportsNE.ReturnUnicodeAsUInt(value));
        }

        [Theory]
        [InlineData(new object[] { 0x41, 'A' })]
        [InlineData(new object[] { 0x45, 'E' })]
        [InlineData(new object[] { 0x4a, 'J' })]
        [InlineData(new object[] { 0xdf, 'ß' })]
        [InlineData(new object[] { 0x2705, '✅' })]
        [InlineData(new object[] { 0x9e1f, '鸟' })]
        public void ValidateUnicodeReturns(uint value, char expected)
        {
            Assert.Equal(expected, NativeExportsNE.ReturnUIntAsUnicode(value));

            char result = '\u0000';
            NativeExportsNE.ReturnUIntAsRefUnicode(value, ref result);
            Assert.Equal(expected, result);

            result = '\u0000';
            NativeExportsNE.ReturnUIntAsOutUnicode(value, out result);
            Assert.Equal(expected, result);
        }
    }
}
