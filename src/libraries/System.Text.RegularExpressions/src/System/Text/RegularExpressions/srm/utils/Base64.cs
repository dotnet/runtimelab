// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Custom serialization and deserialization for numbers and arrays of numbers using a base-64 alphabet similar to a decimal alphabet.
    /// </summary>
    internal static class Base64
    {
        private static char[] s_customBase64encoding = new char[64] {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
            '+', '/'
        };
        private static byte[] s_customBase64decoding = new byte[128] {
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
        public static string Encode(int n)
        {
            if (n == 0)
                return s_customBase64encoding[0].ToString();

            string negSign = (n < 0 ? "-" : "");
            if (n < 0) n = -n;

            string res = "";
            while (n > 0)
            {
                res = s_customBase64encoding[n & 0x3F] + res;
                n = n >> 6;
            }
            return negSign + res;
        }

        /// <summary>
        /// Custom Base64 encoder for long.
        /// It uses the standard base 64 alphabet but reordered to 0-9A-Za-z+/ so numbers up to 15 (0-9A-F) coincide with hexadecimal digits.
        /// Negative numbers start with '-'.
        /// </summary>
        public static string Encode(long n)
        {
            if (n == 0)
                return s_customBase64encoding[0].ToString();

            string negSign = (n < 0 ? "-" : "");
            if (n < 0) n = -n;

            string res = "";
            while (n > 0)
            {
                res = s_customBase64encoding[n & 0x3F] + res;
                n = n >> 6;
            }
            return negSign + res;
        }

        /// <summary>
        /// Custom Base64 encoder for ulong.
        /// It uses the standard base 64 alphabet but reordered to 0-9A-Za-z+/ so numbers up to 15 (0-9A-F) coincide with hexadecimal digits.
        /// </summary>
        public static string Encode(ulong n)
        {
            if (n == 0)
                return s_customBase64encoding[0].ToString();

            string res = "";
            while (n > 0)
            {
                res = s_customBase64encoding[n & 0x3F] + res;
                n = n >> 6;
            }
            return res;
        }

        /// <summary>
        /// Custom Base64 encoder for ulong[].
        /// Calls Encode for each number. Numbers are separated by '.'.
        /// Appends the serialized output to sb.
        /// </summary>
        public static void Encode(ulong[] arr, StringBuilder sb)
        {
            if (arr.Length == 0)
                return;

            sb.Append(Encode(arr[0]));
            for (int i=1; i< arr.Length; i++)
            {
                sb.Append('.');
                sb.Append(Encode(arr[i]));
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
                return;

            sb.Append(Encode(arr[0]));
            for (int i = 1; i < arr.Length; i++)
            {
                sb.Append('.');
                sb.Append(Encode(arr[i]));
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
                return;

            sb.Append(Encode(arr[0]));
            for (int i = 1; i < arr.Length; i++)
            {
                sb.Append('.');
                sb.Append(Encode(arr[i]));
            }
        }

        /// <summary>
        /// Custom Base64 encoder for char[].
        /// Calls Encode for each character code. Numbers are separated by '.'.
        /// Appends the serialized output to sb.
        /// </summary>
        public static void Encode(char[] arr, StringBuilder sb)
        {
            if (arr.Length == 0)
                return;

            sb.Append(Encode(arr[0]));
            for (int i = 1; i < arr.Length; i++)
            {
                sb.Append('.');
                sb.Append(Encode(arr[i]));
            }
        }

        /// <summary>
        /// Custom Base64 encoder for string.
        /// Calls Encode for each character code. Numbers are separated by '.'.
        /// Appends the serialized output to sb.
        /// </summary>
        public static void Encode(string s, StringBuilder sb)
        {
            if (s.Length == 0)
                return;

            sb.Append(Encode(s[0]));
            for (int i = 1; i < s.Length; i++)
            {
                sb.Append('.');
                sb.Append(Encode(s[i]));
            }
        }

        /// <summary>
        /// Custom Base64 deserializer for strings that are stored as int[] of character codes.
        /// </summary>
        public static string DecodeString(string s)
        {
            if (s.Length == 0)
                return string.Empty;
            else
                return new(Array.ConvertAll(s.Split('.'), x => (char)DecodeUInt(x)));
        }

        /// <summary>
        /// Custom Base64 deserializer for int.
        /// </summary>
        public static int DecodeInt(string s)
        {
            bool isNegative = (s[0] == '-');
            int start = (isNegative ? 1 : 0);
            int res = 0;
            for (int i = start; i < s.Length; i++)
                res = (res << 6) | s_customBase64decoding[s[i] & 0x7F];
            return (isNegative ? -res : res);
        }

        /// <summary>
        /// Custom Base64 deserializer for uint.
        /// </summary>
        public static uint DecodeUInt(string s)
        {
            uint res = 0;
            for (int i = 0; i < s.Length; i++)
                res = (res << 6) | s_customBase64decoding[s[i] & 0x7F];
            return res;
        }

        /// <summary>
        /// Custom Base64 deserializer for char.
        /// </summary>
        public static char DecodeChar(string s)
        {
            uint res = 0;
            for (int i = 0; i < s.Length; i++)
                res = (res << 6) | s_customBase64decoding[s[i] & 0x7F];
            return (char)res;
        }

        /// <summary>
        /// Custom Base64 deserializer for long.
        /// </summary>
        public static long DecodeInt64(string s)
        {
            bool isNegative = (s[0] == '-');
            int start = (isNegative ? 1 : 0);
            long res = 0;
            for (int i = start; i < s.Length; i++)
                res = (res << 6) | s_customBase64decoding[s[i] & 0x7F];
            return (isNegative ? -res : res);
        }

        /// <summary>
        /// Custom Base64 deserializer for ulong.
        /// </summary>
        public static ulong DecodeUInt64(string s)
        {
            ulong res = 0;
            for (int i = 0; i < s.Length; i++)
                res = (res << 6) | s_customBase64decoding[s[i] & 0x7F];
            return res;
        }

        /// <summary>
        /// Custom Base64 deserializer for int[].
        /// </summary>
        public static int[] DecodeIntArray(string s) => (s == string.Empty ? Array.Empty<int>() : Array.ConvertAll(s.Split('.'), DecodeInt));

        /// <summary>
        /// Custom Base64 deserializer for unit[].
        /// </summary>
        public static uint[] DecodeUIntArray(string s) => (s == string.Empty ? Array.Empty<uint>() : Array.ConvertAll(s.Split('.'), DecodeUInt));

        /// <summary>
        /// Custom Base64 deserializer for long[].
        /// </summary>
        public static long[] DecodeInt64Array(string s) => (s == string.Empty ? Array.Empty<long>() : Array.ConvertAll(s.Split('.'), DecodeInt64));

        /// <summary>
        /// Custom Base64 deserializer for ulong[].
        /// </summary>
        public static ulong[] DecodeUInt64Array(string s) => (s == string.Empty ? Array.Empty<ulong>() : Array.ConvertAll(s.Split('.'), DecodeUInt64));

        /// <summary>
        /// Custom Base64 deserializer for char[].
        /// </summary>
        public static char[] DecodeCharArray(string s) => (s == string.Empty ? Array.Empty<char>() : Array.ConvertAll(s.Split('.'), DecodeChar));
    }
}
