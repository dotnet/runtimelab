using System;
using System.Runtime.InteropServices;

namespace NativeExports
{
    public static unsafe class Strings
    {
        [UnmanagedCallersOnly(EntryPoint = "ushort_return_length")]
        public static int ReturnLengthOfUShortString(ushort* input)
        {
            if (input == null)
                return -1;

            return GetLength(input);
        }

        [UnmanagedCallersOnly(EntryPoint = "byte_return_length")]
        public static int ReturnLengthOfByteString(byte* input)
        {
            if (input == null)
                return -1;

            return GetLength(input);
        }

        [UnmanagedCallersOnly(EntryPoint = "append_return_ushort")]
        public static ushort* AppendReturnAsUShort(ushort* a, ushort* b)
        {
            return Append(a, b);
        }

        [UnmanagedCallersOnly(EntryPoint = "append_return_byte")]
        public static byte* AppendReturnAsByte(byte* a, byte* b)
        {
            return Append(a, b);
        }

        [UnmanagedCallersOnly(EntryPoint = "append_outushort")]
        public static void AppendReturnAsOutUShort(ushort* a, ushort* b, ushort** ret)
        {
            *ret = Append(a, b);
        }

        [UnmanagedCallersOnly(EntryPoint = "append_outbyte")]
        public static void AppendReturnAsOutByte(byte* a, byte* b, byte** ret)
        {
            *ret = Append(a, b);
        }

        [UnmanagedCallersOnly(EntryPoint = "reverse_refushort")]
        public static void ReverseRefUShort(ushort** refInput)
        {
            if (*refInput == null)
                return;

            int len = GetLength(*refInput);
            var span = new Span<ushort>(*refInput, len);
            span.Reverse();
        }

        [UnmanagedCallersOnly(EntryPoint = "reverse_refbyte")]
        public static void ReverseRefByte(byte** refInput)
        {
            int len = GetLength(*refInput);
            var span = new Span<byte>(*refInput, len);
            span.Reverse();
        }

        private static ushort* Append(ushort* a, ushort* b)
        {
            int lenA = GetLength(a);
            int lenB = GetLength(b);
            ushort* res = (ushort*)Marshal.AllocCoTaskMem((lenA + lenB + 1) * sizeof(ushort));
            new Span<ushort>(a, lenA).CopyTo(new Span<ushort>(res, lenA));
            new Span<ushort>(b, lenB).CopyTo(new Span<ushort>(res + lenA, lenB));
            res[lenA + lenB] = 0;
            return res;
        }

        private static byte* Append(byte* a, byte* b)
        {
            int lenA = GetLength(a);
            int lenB = GetLength(b);
            byte* res = (byte*)Marshal.AllocCoTaskMem((lenA + lenB + 1) * sizeof(byte));
            new Span<byte>(a, lenA).CopyTo(new Span<byte>(res, lenA));
            new Span<byte>(b, lenB).CopyTo(new Span<byte>(res + lenA, lenB));
            res[lenA + lenB] = 0;
            return res;
        }

        private static int GetLength(ushort* input)
        {
            if (input == null)
                return 0;

            int len = 0;
            while (*input != 0)
            {
                input++;
                len++;
            }

            return len;
        }

        private static int GetLength(byte* input)
        {
            if (input == null)
                return 0;

            int len = 0;
            while (*input != 0)
            {
                input++;
                len++;
            }

            return len;
        }
    }
}
