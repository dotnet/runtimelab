// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;


namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Represents a bitvector of given Length (number of bits).
    /// </summary>
    internal sealed class BV : IComparable
    {
        private readonly ulong[] _blocks;

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
                Debug.Assert(i >= 0 && i < Length);
                int k = i / 64;
                int j = i % 64;
                return (_blocks[k] & (UL1 << j)) != 0;
            }
            private set
            {
                Debug.Assert(i >= 0 && i < Length);
                int k = i / 64;
                int j = i % 64;
                if (value)
                {
                    //set the j'th bit of the k'th block to 1
                    _blocks[k] |= (UL1 << j);
                }
                else
                {
                    //set the j'th bit of the k'th block to 0
                    _blocks[k] &= ~(UL1 << j);
                }
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
            Debug.Assert(x.Length == y.Length);

            var blocks = new ulong[x._blocks.Length];
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i] = x._blocks[i] & y._blocks[i];
            }
            return new BV(x.Length, blocks);
        }

        /// <summary>
        /// Bitwise OR
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BV operator |(BV x, BV y)
        {
            Debug.Assert(x.Length == y.Length);

            var blocks = new ulong[x._blocks.Length];
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i] = x._blocks[i] | y._blocks[i];
            }
            return new BV(x.Length, blocks);
        }

        /// <summary>
        /// Bitwise XOR
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BV operator ^(BV x, BV y)
        {
            Debug.Assert(x.Length == y.Length);

            var blocks = new ulong[x._blocks.Length];
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i] = x._blocks[i] ^ y._blocks[i];
            }
            return new BV(x.Length, blocks);
        }

        /// <summary>
        /// Bitwise NOT
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BV operator ~(BV x)
        {
            var blocks = new ulong[x._blocks.Length];
            for (int i = 0; i < blocks.Length; i++)
            {
                blocks[i] = ~x._blocks[i];
            }

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
        public static bool operator <(BV x, BV y) => x.CompareTo(y) < 0;

        /// <summary>
        /// greater than
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(BV x, BV y) => x.CompareTo(y) > 0;

        /// <summary>
        /// less than or equal
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(BV x, BV y) => x.CompareTo(y) <= 0;

        /// <summary>
        /// greater than or equal
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(BV x, BV y) => x.CompareTo(y) >= 0;

        /// <summary>
        /// Returns the serialized representation
        /// </summary>
        public override string ToString() => SerializeToString();

        public override bool Equals(object obj) => CompareTo(obj) == 0;

        private int _hashcode;
        public override int GetHashCode()
        {
            if (_hashcode == 0)
            {
                _hashcode = Length.GetHashCode();
                for (int i = 0; i < _blocks.Length; i++)
                {
                    _hashcode = (_hashcode << 1) ^ _blocks[i].GetHashCode();
                }
            }

            return _hashcode;
        }

        public int CompareTo(object obj)
        {
            if (obj is not BV that)
                return 1;

            if (Length != that.Length)
                return Length.CompareTo(that.Length);

            for (int i = _blocks.Length - 1; i >= 0; i--)
            {
                if (_blocks[i] < that._blocks[i])
                    return -1;

                if (_blocks[i] > that._blocks[i])
                    return 1;
            }

            //all blocks were equal
            return 0;
        }

        #region serialization
        /// <summary>
        /// Serialize BV into a string of Base64 numerals, number of bits separated by '-' at the start,
        /// </summary>
        public void Serialize(StringBuilder sb)
        {
            //start with the length, i.e., the number of bits
            Base64.Encode(Length, sb);
            sb.Append('-');
            Base64.Encode(_blocks, sb);
        }

        public string SerializeToString()
        {
            StringBuilder sb = new();
            Serialize(sb);
            return sb.ToString();
        }

        /// <summary>
        /// Deserialize BV from given string s that was produced by Serialize
        /// </summary>
        public static BV Deserialize(string s)
        {
            int i = s.IndexOf('-');
            int K = Base64.DecodeInt(s.AsSpan(0, i));
            ulong[] blocks = Base64.DecodeUInt64Array(s.AsSpan(i + 1));
            return new BV(K, blocks);
        }
        #endregion
    }
}
