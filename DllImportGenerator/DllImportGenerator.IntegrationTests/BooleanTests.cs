using System.Collections.Generic;
using System.Runtime.InteropServices;

using Xunit;

namespace DllImportGenerator.IntegrationTests
{
    partial class NativeExportsNE
    {
        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "return_byte_as_uint")]
        public static partial uint ReturnByteBoolAsUInt([MarshalAs(UnmanagedType.U1)] bool input);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "return_byte_as_uint")]
        public static partial uint ReturnSByteBoolAsUInt([MarshalAs(UnmanagedType.I1)] bool input);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "return_ushort_as_uint")]
        public static partial uint ReturnVariantBoolAsUInt([MarshalAs(UnmanagedType.VariantBool)] bool input);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "return_uint_as_uint")]
        public static partial uint ReturnIntBoolAsUInt([MarshalAs(UnmanagedType.I4)] bool input);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "return_uint_as_uint")]
        public static partial uint ReturnUIntBoolAsUInt([MarshalAs(UnmanagedType.U4)] bool input);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "return_uint_as_uint")]
        public static partial uint ReturnWinBoolAsUInt([MarshalAs(UnmanagedType.Bool)] bool input);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "return_uint_as_uint")]
        [return: MarshalAs(UnmanagedType.U1)]
        public static partial bool ReturnUIntAsByteBool(uint input);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "return_uint_as_uint")]
        [return: MarshalAs(UnmanagedType.VariantBool)]
        public static partial bool ReturnUIntAsVariantBool(uint input);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "return_uint_as_uint")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ReturnUIntAsWinBool(uint input);
    }

    public class BooleanTests
    {
        // See definition of Windows' VARIANT_BOOL
        const ushort VARIANT_TRUE = unchecked((ushort)-1);
        const ushort VARIANT_FALSE = 0;

        [Fact]
        public void ValidateBoolIsMarshalledAsExpected()
        {
            Assert.Equal((uint)1, NativeExportsNE.ReturnByteBoolAsUInt(true));
            Assert.Equal((uint)0, NativeExportsNE.ReturnByteBoolAsUInt(false));
            Assert.Equal((uint)1, NativeExportsNE.ReturnSByteBoolAsUInt(true));
            Assert.Equal((uint)0, NativeExportsNE.ReturnSByteBoolAsUInt(false));
            Assert.Equal(VARIANT_TRUE, NativeExportsNE.ReturnVariantBoolAsUInt(true));
            Assert.Equal(VARIANT_FALSE, NativeExportsNE.ReturnVariantBoolAsUInt(false));
            Assert.Equal((uint)1, NativeExportsNE.ReturnIntBoolAsUInt(true));
            Assert.Equal((uint)0, NativeExportsNE.ReturnIntBoolAsUInt(false));
            Assert.Equal((uint)1, NativeExportsNE.ReturnUIntBoolAsUInt(true));
            Assert.Equal((uint)0, NativeExportsNE.ReturnUIntBoolAsUInt(false));
            Assert.Equal((uint)1, NativeExportsNE.ReturnWinBoolAsUInt(true));
            Assert.Equal((uint)0, NativeExportsNE.ReturnWinBoolAsUInt(false));
        }

        public static IEnumerable<object[]> ByteBoolReturns()
        {
            yield return new object[] { 0, false };
            yield return new object[] { 1, true };
            yield return new object[] { 37, true };
            yield return new object[] { 0xff, true };
            yield return new object[] { 0xffffff00, false };
        }

        [Theory]
        [MemberData(nameof(ByteBoolReturns))]
        public void ValidateByteBoolReturns(uint value, bool expected)
        {
            Assert.Equal(expected, NativeExportsNE.ReturnUIntAsByteBool(value));
        }

        public static IEnumerable<object[]> VariantBoolReturns()
        {
            yield return new object[] { 0, false };
            yield return new object[] { 1, false };
            yield return new object[] { 0xff, false };
            yield return new object[] { VARIANT_TRUE, true };
            yield return new object[] { 0xffffffff, true };
            yield return new object[] { 0xffff0000, false };
        }

        [Theory]
        [MemberData(nameof(VariantBoolReturns))]
        public void ValidateVariantBoolReturns(uint value, bool expected)
        {
            Assert.Equal(expected, NativeExportsNE.ReturnUIntAsVariantBool(value));
        }

        public static IEnumerable<object[]> WinBoolReturns()
        {
            yield return new object[] { 0, false };
            yield return new object[] { 1, true};
            yield return new object[] { 37, true };
            yield return new object[] { 0xffffffff, true };
            yield return new object[] { 0x80000000, true };
        }

        [Theory]
        [MemberData(nameof(WinBoolReturns))]
        public void ValidateWinBoolReturns(uint value, bool expected)
        {
            Assert.Equal(expected, NativeExportsNE.ReturnUIntAsWinBool(value));
        }
    }
}
