using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.SRM
{
    /// <summary>
    /// Number of bits used in bitvectors.
    /// </summary>
    internal enum BitWidth
    {
        /// <summary>
        /// 7 bit ASCII encoding
        /// </summary>
        BV7 = 7,
        /// <summary>
        /// 8 bit Extended ASCII encoding
        /// </summary>
        BV8 = 8,
        /// <summary>
        /// 16 bit bit-vector encoding
        /// </summary>
        BV16 = 16,
        /// <summary>
        /// 32 bit bit-vector encoding
        /// </summary>
        BV32 = 32,
        ///// <summary>
        ///// 64 bit bit-vector encoding
        ///// </summary>
        BV64 = 64
    }

    /// <summary>
    /// Provides functionality for character encodings. 
    /// </summary>
    internal static class CharacterEncodingTool
    {
        /// <summary>
        /// Maps ASCII to 7, extended ASCII to 8, and other encodings to 16.
        /// Throws AutomataException if IsSpecified(encoding) is false.
        /// </summary>
        /// <param name="encoding"></param>
        /// <returns>either 7, 8, or 16</returns>
        public static int Truncate(BitWidth encoding)
        {
            switch (encoding)
            {
                case BitWidth.BV7: return 7;
                case BitWidth.BV8: return 8;
                case BitWidth.BV16: return 16;
                case BitWidth.BV32: return 16;
                case BitWidth.BV64: return 16;
                default:
                    throw new AutomataException(AutomataExceptionKind.CharacterEncodingIsUnspecified);
            }
        }

        /// <summary>
        /// Returns true iff encoding equals to one of the enums in CharacterEncoding.
        /// </summary>
        public static bool IsSpecified(BitWidth encoding)
        {
            return (encoding == BitWidth.BV7 ||
                encoding == BitWidth.BV32 ||
                encoding == BitWidth.BV8 ||
                encoding == BitWidth.BV64 ||
                encoding == BitWidth.BV16);
        }
    }
}
