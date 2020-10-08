using System.Runtime.InteropServices;

using Xunit;

namespace DllImportGenerator.IntegrationTests
{
    partial class NativeExportsNE
    {
        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "validate_byte_bool_value")]
        [return: MarshalAs(UnmanagedType.U1)]
        public static partial bool ValidateByteBoolValue(
            byte expected,
            [MarshalAs(UnmanagedType.U1)] bool actual);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "validate_variant_bool_value")]
        [return: MarshalAs(UnmanagedType.U1)]
        public static partial bool ValidateVariantBoolValue(
            short expected,
            [MarshalAs(UnmanagedType.VariantBool)] bool actual);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "validate_int_bool_value")]
        [return: MarshalAs(UnmanagedType.U1)]
        public static partial bool ValidateIntBoolValue(
            int expected,
            [MarshalAs(UnmanagedType.Bool)] bool actual);
    }

    public class BooleanTests
    {
        [Fact]
        public void ValidateByteBoolMarshalling()
        {
            Assert.True(NativeExportsNE.ValidateByteBoolValue(1, true));
            Assert.True(NativeExportsNE.ValidateByteBoolValue(0, false));
            Assert.False(NativeExportsNE.ValidateByteBoolValue(0, true));
            Assert.False(NativeExportsNE.ValidateByteBoolValue(1, false));
        }

        [Fact]
        public void ValidateVariantBoolMarshalling()
        {
            // See definition of Windows' VARIANT_BOOL
            const short VARIANT_TRUE = -1;
            const short VARIANT_FALSE = 0;

            Assert.True(NativeExportsNE.ValidateVariantBoolValue(VARIANT_TRUE, true));
            Assert.True(NativeExportsNE.ValidateVariantBoolValue(VARIANT_FALSE, false));
            Assert.False(NativeExportsNE.ValidateVariantBoolValue(VARIANT_FALSE, true));
            Assert.False(NativeExportsNE.ValidateVariantBoolValue(VARIANT_TRUE, false));
        }

        [Fact]
        public void ValidateIntBoolMarshalling()
        {
            Assert.True(NativeExportsNE.ValidateIntBoolValue(1, true));
            Assert.True(NativeExportsNE.ValidateIntBoolValue(0, false));
            Assert.False(NativeExportsNE.ValidateByteBoolValue(0, true));
            Assert.False(NativeExportsNE.ValidateByteBoolValue(1, false));
        }
    }
}
