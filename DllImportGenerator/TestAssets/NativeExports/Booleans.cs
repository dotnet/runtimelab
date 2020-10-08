using System.Runtime.InteropServices;

namespace NativeExports
{
    public static unsafe class Booleans
    {
        [UnmanagedCallersOnly(EntryPoint = "return_byte_as_uint")]
        public static uint ReturnByteAsUInt(byte input)
        {
            return input;
        }

        [UnmanagedCallersOnly(EntryPoint = "return_ushort_as_uint")]
        public static uint ReturnUShortAsUInt(ushort input)
        {
            return input;
        }

        [UnmanagedCallersOnly(EntryPoint = "return_uint_as_uint")]
        public static uint ReturnUIntAsUInt(uint input)
        {
            return input;
        }
    }
}
