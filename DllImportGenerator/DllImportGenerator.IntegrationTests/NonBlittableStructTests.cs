using System.Runtime.InteropServices;

using SharedTypes;

using Xunit;

namespace DllImportGenerator.IntegrationTests
{
    partial class NativeExportsNE
    {
        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "stringcontainer_deepduplicate")]
        public static partial void DeepDuplicateStrings(StringContainer strings, out StringContainer pStringsOut);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "get_long_bytes_as_double")]
        public static partial double GetLongBytesAsDouble([MarshalUsing(typeof(DoubleToLongMarshaler))] double d);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "negate_bools")]
        public static partial void NegateBools(
            BoolStruct boolStruct,
            out BoolStruct pBoolStructOut);

        [GeneratedDllImport(nameof(NativeExportsNE), EntryPoint = "double_int_ref")]
        public static partial IntWrapper DoubleIntRef(IntWrapper pInt);
    }

    public class NonBlittableStructTests
    {
        [Fact]
        public void NonBlittableStructWithFree()
        {
            var stringContainer = new StringContainer
            {
                str1 = "Foo",
                str2 = "Bar"
            };

            NativeExportsNE.DeepDuplicateStrings(stringContainer, out var stringContainer2);

            Assert.Equal(stringContainer, stringContainer2);
        }

        [Fact]
        public void MarshalUsing()
        {
            double d = 1234.56789;

            Assert.Equal(d, NativeExportsNE.GetLongBytesAsDouble(d));
        }

        [Fact]
        public void NonBlittableStructWithoutAllocation()
        {
            var boolStruct = new BoolStruct
            {
                b1 = true,
                b2 = false,
                b3 = true
            };

            NativeExportsNE.NegateBools(boolStruct, out BoolStruct boolStructNegated);

            Assert.Equal(!boolStruct.b1, boolStructNegated.b1);
            Assert.Equal(!boolStruct.b2, boolStructNegated.b2);
            Assert.Equal(!boolStruct.b3, boolStructNegated.b3);
        }

        [Fact]
        public void GetPinnableReferenceMarshalling()
        {
            int originalValue = 42;
            var wrapper = new IntWrapper { i = originalValue };

            var retVal = NativeExportsNE.DoubleIntRef(wrapper);

            Assert.Equal(originalValue * 2, wrapper.i);
            Assert.Equal(originalValue * 2, retVal.i);
        }
    }
}
