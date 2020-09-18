using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;


namespace Microsoft.SRM
{
    /// <summary>
    /// Represents a bitvector
    /// </summary>
    [Serializable]
    internal class BV : IComparable, ISerializable
    {
        internal ulong first;
        internal ulong[] more;

        /// <summary>
        /// Constructs a bitvector
        /// </summary>
        /// <param name="first">first 64 bits</param>
        /// <param name="more">remaining bits in 64 increments</param>
        public BV(ulong first, params ulong[] more)
        {
            this.first = first;
            this.more = more;
        }

        /// <summary>
        /// Bitwise AND
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static BV operator &(BV x, BV y)
        {
            int k = (x.more.Length <= y.more.Length ? x.more.Length : y.more.Length);
            var first = x.first & y.first;
            var more = new ulong[k];
            for (int i = 0; i < k; i++)
            {
                more[i] = x.more[i] & y.more[i];
            }
            return new BV(first, more);
        }

        /// <summary>
        /// Bitwise OR
        /// </summary>
        public static BV operator |(BV x, BV y)
        {
            int k = (x.more.Length <= y.more.Length ? x.more.Length : y.more.Length);
            var first = x.first | y.first;
            var more = new ulong[k];
            for (int i = 0; i < k; i++)
            {
                more[i] = x.more[i] | y.more[i];
            }
            return new BV(first, more);
        }

        /// <summary>
        /// Bitwise XOR
        /// </summary>
        public static BV operator ^(BV x, BV y)
        {
            int k = (x.more.Length <= y.more.Length ? x.more.Length : y.more.Length);
            var first = x.first ^ y.first;
            var more = new ulong[x.more.Length];
            for (int i = 0; i < x.more.Length; i++)
            {
                more[i] = x.more[i] ^ y.more[i];
            }
            return new BV(first, more);
        }

        /// <summary>
        /// Bitwise NOT
        /// </summary>
        public static BV operator ~(BV x)
        {
            var first_compl = ~x.first;
            var more_compl = Array.ConvertAll(x.more, n => ~n);
            var compl = new BV(first_compl, more_compl);
            return compl;
        }

        /// <summary>
        /// less than
        /// </summary>
        public static bool operator <(BV x, BV y)
        {
            return x.CompareTo(y) < 0;
        }

        /// <summary>
        /// greater than
        /// </summary>
        public static bool operator >(BV x, BV y)
        {
            return x.CompareTo(y) > 0;
        }

        /// <summary>
        /// less than or equal
        /// </summary>
        public static bool operator <=(BV x, BV y)
        {
            return x.CompareTo(y) <= 0;
        }

        /// <summary>
        /// greater than or equal
        /// </summary>
        public static bool operator >=(BV x, BV y)
        {
            return x.CompareTo(y) >= 0;
        }

        /// <summary>
        /// Shows the serialized representation
        /// </summary>
        public override string ToString()
        {
            return Serialize();
        }

        public override bool Equals(object obj)
        {
            BV that = obj as BV;
            if (that == null)
                return false;
            if (this == that)
                return true;
            if (this.first != that.first)
                return false;
            if (that.more.Length != this.more.Length)
                return false;
            for (int i = 0; i < more.Length; i++)
            {
                if (more[i] != that.more[i])
                    return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            int h = first.GetHashCode();
            for (int i = 0; i < more.Length; i++)
            {
                h = (h << 5) ^ more[i].GetHashCode();
            }
            return h;
        }

        public int CompareTo(object obj)
        {
            BV that = obj as BV;
            if (that == null)
                return 1;
            else if (this.more.Length != that.more.Length)
            {
                return this.more.Length.CompareTo(that.more.Length);
            }
            else
            {
                int k = this.more.Length;
                if (k > 0)
                {
                    int i = k - 1;
                    while (i >= 0)
                    {
                        var comp = this.more[i].CompareTo(that.more[i]);
                        if (comp == 0)
                            i = i - 1;
                        else
                            return comp;
                    }
                }
                return this.first.CompareTo(that.first);
            }
        }

        #region serialization
        /// <summary>
        /// Serialize
        /// </summary>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("bv", Serialize());
        }
        /// <summary>
        /// Deserialize
        /// </summary>
        public BV(SerializationInfo info, StreamingContext context)
        {
            var s = info.GetString("bv");
            Deserialize_Helper(s, out first, out more);
        }

        /// <summary>
        /// Serialize BV into a string of hexadecimal numerals, separated by '.',
        /// each numeral representing an unsigned 64-bit integer in hexadecimal using lowercase a-f
        /// </summary>
        /// <returns></returns>
        public string Serialize()
        {
            string str = this.first.ToString("x") + "." + string.Join(".", Array.ConvertAll(this.more, x => x.ToString("x")));
            return str;
        }

        /// <summary>
        /// Deserialize BV from given string that was produced by Serialize
        /// </summary>
        /// <param name="s">BV in serialized form</param>
        public static BV Deserialize(string s)
        {
            ulong first;
            ulong[] rest;
            Deserialize_Helper(s, out first, out rest);
            return new BV(first, rest);
        }

        private static void Deserialize_Helper(string s, out ulong first, out ulong[] rest)
        {
            int i = s.IndexOf('.');
            first = ulong.Parse(s.Substring(0, i), System.Globalization.NumberStyles.HexNumber);
            rest = Array.ConvertAll(s.Substring(i + 1).Split('.'), x => ulong.Parse(x, System.Globalization.NumberStyles.HexNumber));
        }
        #endregion
    }
}
