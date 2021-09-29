using System.Diagnostics;
using System.Runtime.InteropServices;

namespace System.Net.Http.LowLevel
{
    internal static class BitHelpers
    {
        public static ushort AsciiToUInt16(ReadOnlySpan<char> s)
        {
            Debug.Assert(s.Length == 2);

            Span<byte> b = stackalloc byte[2];

            b[0] = (byte)s[0];
            b[1] = (byte)s[1];

            return MemoryMarshal.Read<ushort>(b);
        }

        public static uint AsciiToUInt32(ReadOnlySpan<char> s)
        {
            Debug.Assert(s.Length == 4);

            Span<byte> b = stackalloc byte[4];

            b[0] = (byte)s[0];
            b[1] = (byte)s[1];
            b[2] = (byte)s[2];
            b[3] = (byte)s[3];

            return MemoryMarshal.Read<uint>(b);
        }

        public static ulong AsciiToUInt64(ReadOnlySpan<char> s)
        {
            Debug.Assert(s.Length == 8);

            Span<byte> b = stackalloc byte[8];

            b[0] = (byte)s[0];
            b[1] = (byte)s[1];
            b[2] = (byte)s[2];
            b[3] = (byte)s[3];
            b[4] = (byte)s[4];
            b[5] = (byte)s[5];
            b[6] = (byte)s[6];
            b[7] = (byte)s[7];

            return MemoryMarshal.Read<ulong>(b);
        }
    }
}
