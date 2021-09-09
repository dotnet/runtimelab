// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace System.Text.RegularExpressions.Symbolic
{
    /// <summary>
    /// Custom serialization and deserialization for numbers and arrays of numbers using a base-64 alphabet similar to a decimal alphabet.
    /// </summary>
    internal static class Base64
    {
        private static readonly char[] s_customBase64encoding = new char[64] {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
            '+', '/'
        };

        private static ReadOnlySpan<byte> CustomBase64Decoding => new byte[128] {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            62, //'+' maps to 62
            0, 0, 0,
            63, //'/' maps to 63
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, //digits map to 0..9
            0, 0, 0, 0, 0, 0, 0,
            10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, //uppercase letters
            0, 0, 0, 0, 0, 0,
            36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, //lowercase letters
            0, 0, 0, 0, 0
        };

        /// <summary>
        /// Custom Base64 encoder for int.
        /// It uses the standard base 64 alphabet but reordered to 0-9A-Za-z+/ so numbers up to 15 (0-9A-F) coincide with hexadecimal digits.
        /// Negative numbers start with '-'.
        /// </summary>
        public static void Encode(int n, StringBuilder builder)
        {
            if (n == 0)
            {
                builder.Append(s_customBase64encoding[0]);
            }

            if (n < 0)
            {
                builder.Append('-');
                n = -n;
            }

            Span<char> span = stackalloc char[6];
            int pos = span.Length;
            while (n > 0)
            {
                span[--pos] = s_customBase64encoding[n & 0x3F];
                n >>= 6;
            }
            builder.Append(span.Slice(pos));
        }

        /// <summary>
        /// Custom Base64 encoder for long.
        /// It uses the standard base 64 alphabet but reordered to 0-9A-Za-z+/ so numbers up to 15 (0-9A-F) coincide with hexadecimal digits.
        /// Negative numbers start with '-'.
        /// </summary>
        public static void Encode(long n, StringBuilder builder)
        {
            if (n == 0)
            {
                builder.Append(s_customBase64encoding[0]);
            }

            if (n < 0)
            {
                builder.Append('-');
                n = -n;
            }

            Span<char> span = stackalloc char[11];
            int pos = span.Length;
            while (n > 0)
            {
                span[--pos] = s_customBase64encoding[n & 0x3F];
                n >>= 6;
            }
            builder.Append(span.Slice(pos));
        }

        /// <summary>
        /// Custom Base64 encoder for ulong.
        /// It uses the standard base 64 alphabet but reordered to 0-9A-Za-z+/ so numbers up to 15 (0-9A-F) coincide with hexadecimal digits.
        /// </summary>
        public static void Encode(ulong n, StringBuilder builder)
        {
            if (n == 0)
            {
                builder.Append(s_customBase64encoding[0]);
            }

            Span<char> span = stackalloc char[11];
            int pos = span.Length;
            while (n > 0)
            {
                span[--pos] = s_customBase64encoding[n & 0x3F];
                n >>= 6;
            }
            builder.Append(span.Slice(pos));
        }

        /// <summary>
        /// Custom Base64 encoder for ulong[].
        /// Calls Encode for each number. Numbers are separated by '.'.
        /// Appends the serialized output to sb.
        /// </summary>
        public static void Encode(ulong[] arr, StringBuilder sb)
        {
            if (arr.Length == 0)
            {
                return;
            }

            Encode(arr[0], sb);
            for (int i = 1; i < arr.Length; i++)
            {
                sb.Append('.');
                Encode(arr[i], sb);
            }
        }

        /// <summary>
        /// Custom Base64 encoder for long[].
        /// Calls Encode for each number. Numbers are separated by '.'.
        /// Appends the serialized output to sb.
        /// </summary>
        public static void Encode(long[] arr, StringBuilder sb)
        {
            if (arr.Length == 0)
            {
                return;
            }

            Encode(arr[0], sb);
            for (int i = 1; i < arr.Length; i++)
            {
                sb.Append('.');
                Encode(arr[i], sb);
            }
        }

        /// <summary>
        /// Custom Base64 encoder for int[].
        /// Calls Encode for each number. Numbers are separated by '.'.
        /// Appends the serialized output to sb.
        /// </summary>
        public static void Encode(int[] arr, StringBuilder sb)
        {
            if (arr.Length == 0)
            {
                return;
            }

            Encode(arr[0], sb);
            for (int i = 1; i < arr.Length; i++)
            {
                sb.Append('.');
                Encode(arr[i], sb);
            }
        }

        /// <summary>
        /// Custom Base64 encoder for char[].
        /// Calls Encode for each character code. Numbers are separated by '.'.
        /// Appends the serialized output to sb.
        /// </summary>
        public static void Encode(ReadOnlySpan<char> span, StringBuilder sb)
        {
            if (span.Length == 0)
            {
                return;
            }

            Encode(span[0], sb);
            for (int i = 1; i < span.Length; i++)
            {
                sb.Append('.');
                Encode(span[i], sb);
            }
        }

        /// <summary>
        /// Custom Base64 deserializer for strings that are stored as int[] of character codes.
        /// </summary>
        public static string DecodeString(ReadOnlySpan<char> s)
        {
            if (s.Length == 0)
            {
                return string.Empty;
            }

            var vsb = new ValueStringBuilder(stackalloc char[256]);
            while (true)
            {
                int periodPos = s.IndexOf('.');
                if (periodPos == -1)
                {
                    vsb.Append((char)DecodeUInt(s));
                    break;
                }

                vsb.Append((char)DecodeUInt(s[0..periodPos]));
                s = s.Slice(periodPos + 1);
            }

            return vsb.ToString();
        }

        /// <summary>
        /// Custom Base64 deserializer for int.
        /// </summary>
        public static int DecodeInt(ReadOnlySpan<char> s)
        {
            bool isNegative = false;
            if (s[0] == '-')
            {
                isNegative = true;
                s = s.Slice(1);
            }

            int res = 0;
            for (int i = 0; i < s.Length; i++)
            {
                res = (res << 6) | CustomBase64Decoding[s[i] & 0x7F];
            }

            return isNegative ? -res : res;
        }

        /// <summary>
        /// Custom Base64 deserializer for uint.
        /// </summary>
        public static uint DecodeUInt(ReadOnlySpan<char> s)
        {
            uint res = 0;
            for (int i = 0; i < s.Length; i++)
            {
                res = (res << 6) | CustomBase64Decoding[s[i] & 0x7F];
            }

            return res;
        }

        /// <summary>
        /// Custom Base64 deserializer for char.
        /// </summary>
        public static char DecodeChar(ReadOnlySpan<char> s)
        {
            uint res = 0;
            for (int i = 0; i < s.Length; i++)
            {
                res = (res << 6) | CustomBase64Decoding[s[i] & 0x7F];
            }

            return (char)res;
        }

        /// <summary>
        /// Custom Base64 deserializer for long.
        /// </summary>
        public static long DecodeInt64(ReadOnlySpan<char> s)
        {
            bool isNegative = false;
            if (s[0] == '-')
            {
                isNegative = true;
                s = s.Slice(1);
            }

            long res = 0;
            for (int i = 0; i < s.Length; i++)
            {
                res = (res << 6) | CustomBase64Decoding[s[i] & 0x7F];
            }

            return isNegative ? -res : res;
        }

        /// <summary>
        /// Custom Base64 deserializer for ulong.
        /// </summary>
        public static ulong DecodeUInt64(ReadOnlySpan<char> s)
        {
            ulong res = 0;
            for (int i = 0; i < s.Length; i++)
            {
                res = (res << 6) | CustomBase64Decoding[s[i] & 0x7F];
            }

            return res;
        }

        /// <summary>
        /// Custom Base64 deserializer for int[].
        /// </summary>
        public static int[] DecodeIntArray(ReadOnlySpan<char> s) => DecodeArray(s, span => DecodeInt(span));

        /// <summary>
        /// Custom Base64 deserializer for unit[].
        /// </summary>
        public static uint[] DecodeUIntArray(ReadOnlySpan<char> s) => DecodeArray(s, span => DecodeUInt(span));

        /// <summary>
        /// Custom Base64 deserializer for long[].
        /// </summary>
        public static long[] DecodeInt64Array(ReadOnlySpan<char> s) => DecodeArray(s, span => DecodeInt64(span));

        /// <summary>
        /// Custom Base64 deserializer for ulong[].
        /// </summary>
        public static ulong[] DecodeUInt64Array(ReadOnlySpan<char> s) => DecodeArray(s, span => DecodeUInt64(span));

        /// <summary>
        /// Custom Base64 deserializer for char[].
        /// </summary>
        public static char[] DecodeCharArray(ReadOnlySpan<char> s) => DecodeArray(s, span => DecodeChar(span));

        private static T[] DecodeArray<T>(ReadOnlySpan<char> s, DecodeFunc<T> decode) where T : unmanaged
        {
            if (s.Length == 0)
            {
                return Array.Empty<T>();
            }

            using var results = new ValueListBuilder<T>(stackalloc T[64]);
            while (true)
            {
                int periodPos = s.IndexOf('.');
                if (periodPos == -1)
                {
                    results.Append(decode(s));
                    break;
                }

                results.Append(decode(s[0..periodPos]));
                s = s.Slice(periodPos + 1);
            }

            return results.AsSpan().ToArray();
        }

        private delegate T DecodeFunc<T>(ReadOnlySpan<char> span);
    }
}
