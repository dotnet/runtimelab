using System.Runtime.InteropServices;

namespace NativeExports
{
    public static unsafe class Booleans
    {
        [UnmanagedCallersOnly(EntryPoint = "validate_byte_bool_value")]
        public static byte ValidateByteBoolValue(byte expected, byte actual)
        {
            const byte TRUE = 1;
            const byte FALSE = 0;

            switch (expected, actual)
            {
                case (TRUE, TRUE): break;
                case (TRUE, FALSE): break;
                case (FALSE, TRUE): break;
                case (FALSE, FALSE): break;
                case (_, _): return 0; // Invalid input
            }

            return expected == actual ? 1 : 0;
        }

        [UnmanagedCallersOnly(EntryPoint = "validate_variant_bool_value")]
        public static byte ValidateVariantBoolValue(short expected, short actual)
        {
            // See definition of Windows' VARIANT_BOOL
            const short VARIANT_TRUE = -1;
            const short VARIANT_FALSE = 0;

            switch (expected, actual)
            {
                case (VARIANT_TRUE, VARIANT_TRUE): break;
                case (VARIANT_TRUE, VARIANT_FALSE): break;
                case (VARIANT_FALSE, VARIANT_TRUE): break;
                case (VARIANT_FALSE, VARIANT_FALSE): break;
                case (_, _): return 0; // Invalid input
            }

            return expected == actual ? 1 : 0;
        }

        [UnmanagedCallersOnly(EntryPoint = "validate_int_bool_value")]
        public static byte ValidateIntBoolValue(int expected, int actual)
        {
            const int TRUE = 1;
            const int FALSE = 0;

            switch (expected, actual)
            {
                case (TRUE, TRUE): break;
                case (TRUE, FALSE): break;
                case (FALSE, TRUE): break;
                case (FALSE, FALSE): break;
                case (_, _): return 0; // Invalid input
            }

            return expected == actual ? 1 : 0;
        }

        [UnmanagedCallersOnly(EntryPoint = "return_uint_as")]
        public static uint ReturnUIntAs(uint input)
        {
            return input;
        }
    }
}
