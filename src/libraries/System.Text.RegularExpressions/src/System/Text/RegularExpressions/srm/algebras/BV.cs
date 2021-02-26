// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;


namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Represents a bitvector of given Length (number of bits).
    /// </summary>
    internal class BV : IComparable
    {
        private ulong[] _blocks;

        private const ulong UL1 = 1;

        /// <summary>
        /// Number of bits.
        /// </summary>
        internal readonly int Length;

        /// <summary>
        /// Returns true iff the i'th bit is 1
        /// </summary>
        internal bool this[int i]
        {
            get
            {
#if DEBUG
                if (i < 0 || i >= Length)
                    throw new ArgumentOutOfRangeException(nameof(i));
#endif
                int k = i / 64;
                int j = i % 64;
                return (_blocks[k] & (UL1 << j)) != 0;
            }
            private set
            {
#if DEBUG
                if (i < 0 || i >= Length)
                    throw new ArgumentOutOfRangeException(nameof(i));
#endif
                int k = i / 64;
                int j = i % 64;
                if (value)
                    //set the j'th bit of the k'th block to 1
                    _blocks[k] |= (UL1 << j);
                else
                    //set the j'th bit of the k'th block to 0
                    _blocks[k] &= ~(UL1 << j);
            }
        }

        private BV(int K)
        {
            Length = K;
            _blocks = new ulong[((K - 1) / 64) + 1];
        }

        private BV(int K, ulong[] blocks)
        {
            Length = K;
            _blocks = blocks;
        }

        /// <summary>
        /// Constructs a bitvector of K bits initially all 0.
        /// </summary>
        public static BV MkFalse(int K) => new(K);

        /// <summary>
        /// Constructs a bitvector of K bits initially all 1.
        /// </summary>
        public static BV MkTrue(int K) => ~MkFalse(K);

        /// <summary>
        /// Returns the bitvector of length K with its i'th bit set to 1 all other bits are 0.
        /// </summary>
        public static BV MkBit1(int K, int i)
        {
            BV bv = new BV(K);
            bv[i] = true;
            return bv;
        }

        /// <summary>
        /// Constructs a bitvector with the given bit valuation.
        /// </summary>
        public static BV Mk(params bool[] bits)
        {
            var bv = new BV(bits.Length);
            //all bits are initially 0 so need not be set to 0
            for (int i = 0; i < bits.Length; i++)
                if (bits[i])
                    bv[i] = true;
            return bv;
        }

        /// <summary>
        /// Bitwise AND
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BV operator &(BV x, BV y)
        {
#if DEBUG
            if (x.Length != y.Length)
                throw new InvalidOperationException();
#endif
            ulong[] blocks = new ulong[x._blocks.Length];
            for (int i = 0; i < blocks.Length; i++)
                blocks[i] = x._blocks[i] & y._blocks[i];
            return new BV(x.Length, blocks);
        }

        /// <summary>
        /// Bitwise OR
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BV operator |(BV x, BV y)
        {
#if DEBUG
            if (x.Length != y.Length)
                throw new InvalidOperationException();
#endif
            ulong[] blocks = new ulong[x._blocks.Length];
            for (int i = 0; i < blocks.Length; i++)
                blocks[i] = x._blocks[i] | y._blocks[i];
            return new BV(x.Length, blocks);
        }

        /// <summary>
        /// Bitwise XOR
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BV operator ^(BV x, BV y)
        {
#if DEBUG
            if (x.Length != y.Length)
                throw new InvalidOperationException();
#endif
            ulong[] blocks = new ulong[x._blocks.Length];
            for (int i = 0; i < blocks.Length; i++)
                blocks[i] = x._blocks[i] ^ y._blocks[i];
            return new BV(x.Length, blocks);
        }

        /// <summary>
        /// Bitwise NOT
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BV operator ~(BV x)
        {
            ulong[] blocks = new ulong[x._blocks.Length];
            for (int i = 0; i < blocks.Length; i++)
                blocks[i] = ~x._blocks[i];
            int j = x.Length % 64;
            if (j > 0)
            {
                //the number of bits is not a precise multiple of 64
                //so the last block has extra bits that need to be reset to 0
                int last = (x.Length - 1) / 64;
                blocks[last] &= (UL1 << j) - 1;
            }
            return new BV(x.Length, blocks);
        }

        /// <summary>
        /// less than
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(BV x, BV y)
        {
            return x.CompareTo(y) < 0;
        }

        /// <summary>
        /// greater than
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(BV x, BV y)
        {
            return x.CompareTo(y) > 0;
        }

        /// <summary>
        /// less than or equal
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(BV x, BV y)
        {
            return x.CompareTo(y) <= 0;
        }

        /// <summary>
        /// greater than or equal
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(BV x, BV y)
        {
            return x.CompareTo(y) >= 0;
        }

        /// <summary>
        /// Returns the serialized representation
        /// </summary>
        public override string ToString()
        {
            return SerializeToString();
        }

        public override bool Equals(object obj)
        {
            return CompareTo(obj) == 0;
        }

        private int _hashcode;
        public override int GetHashCode()
        {
            if (_hashcode == 0)
            {
                _hashcode = Length.GetHashCode();
                for (int i = 0; i < _blocks.Length; i++)
                    _hashcode = (_hashcode << 1) ^ _blocks[i].GetHashCode();
            }
            return _hashcode;
        }

        public int CompareTo(object obj)
        {
            BV that = obj as BV;
            if (that == null)
                return 1;
            else if (Length != that.Length)
                return Length.CompareTo(that.Length);
            else
            {
                for (int i = _blocks.Length - 1; i >= 0; i--)
                {
                    if (_blocks[i] < that._blocks[i])
                        return -1;
                    else if (_blocks[i] > that._blocks[i])
                        return 1;
                }
                //all blocks were equal
                return 0;
            }
        }

        #region serialization
        /// <summary>
        /// Serialize BV into a string of hexadecimal numerals separated by '-',
        /// each numeral representing an unsigned 64-bit integer in hexadecimal in [0-9A-F]+.
        /// The serialization starts with the Length and is ordered so that more significant blocks come first.
        /// </summary>
        public void Serialize(StringBuilder sb)
        {
            //start with the length
            sb.Append(Length.ToString("X"));
            sb.Append('-');
            //then consider bits in higher blocks as being more significant in order
            for (int i = _blocks.Length - 1; i > 0 ; i--)
            {
                sb.Append(_blocks[i].ToString("X"));
                sb.Append('-');
            }
            sb.Append(_blocks[0].ToString("X"));
        }

        public string SerializeToString()
        {
            StringBuilder sb = new StringBuilder();
            Serialize(sb);
            return sb.ToString();
        }

        /// <summary>
        /// Deserialize BV from given string s that was produced by Serialize
        /// </summary>
        public static BV Deserialize(string s)
        {
            int i = s.IndexOf('-');
            int K = int.Parse(s.Substring(0, i), Globalization.NumberStyles.HexNumber);
            ulong[] blocks = Array.ConvertAll(s.Substring(i + 1).Split('-'), x => ulong.Parse(x, Globalization.NumberStyles.HexNumber));
            return new BV(K, blocks);
        }
        #endregion
    }
}
