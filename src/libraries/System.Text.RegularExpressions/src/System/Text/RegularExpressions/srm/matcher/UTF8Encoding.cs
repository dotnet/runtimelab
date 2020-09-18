using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Microsoft.SRM
{
    /// <summary>
    /// Methods for decoding UTF8 encoded strings.
    /// </summary>
    internal static class UTF8Encoding
    {
        /// <summary>
        /// Decode the next codepoint in the input.
        /// Here input[i] is assumed to be non-ASCII.
        /// The input byte array is asssumed to be valid UTF8 encoded Unicode text.
        /// </summary>
        /// <param name="input">UTF8 encoded Unicode text</param>
        /// <param name="i">position of the current start byte</param>
        /// <param name="step">how many bytes were consumed</param>
        /// <param name="codepoint">computed Unicode codepoint</param>
        /// <returns></returns>
        internal static void DecodeNextNonASCII(byte[] input, int i, out int step, out int codepoint)
        {
            int b = input[i];
            // (b & 1110.0000 == 1100.0000)
            // so b has the form 110x.xxxx
            // startbyte of two byte encoding
            if ((b & 0xE0) == 0xC0)
            {
                codepoint = ((b & 0x1F) << 6) | (input[i + 1] & 0x3F);
                step = 2;
            }
            // (b & 1111.0000 == 1110.0000)
            // so b has the form 1110.xxxx
            // startbyte of three byte encoding
            else if ((b & 0xF0) == 0xE0)
            {
                codepoint = ((b & 0x0F) << 12) | ((input[i + 1] & 0x3F) << 6) | (input[i + 2] & 0x3F);
                step = 3;
            }
            // (b & 1111.1000 == 1111.0000)
            // so b has the form 1111.0xxx
            // must be startbyte of four byte encoding
            else
            {
                codepoint = ((b & 0x07) << 18) | ((input[i + 1] & 0x3F) << 12) | ((input[i + 2] & 0x3F) << 6) | (input[i + 3] & 0x3F);
                step = 4;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort HighSurrogate(int codepoint)
        {
            //given codepoint = ((H - 0xD800) * 0x400) + (L - 0xDC00) + 0x10000
            // compute H 
            return (ushort)(((codepoint - 0x10000) >> 10) | 0xD800);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static ushort LowSurrogate(int codepoint)
        {
            //given codepoint = ((H - 0xD800) * 0x400) + (L - 0xDC00) + 0x10000
            //compute L 
            var cp = (ushort)(((codepoint - 0x10000) & 0x3FF) | 0xDC00);
            return cp;
        }
    }
}
