using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Microsoft.SRM
{
    /// <summary>
    /// Represents a sorted finite set of finite intervals representing characters
    /// </summary>
    [Serializable]
    internal class IntervalSet : ISerializable
    {
        private Tuple<uint, uint>[] intervals;

        /// <summary>
        /// Create a new interval set
        /// </summary>
        /// <param name="intervals">given intervals</param>
        public IntervalSet(params Tuple<uint, uint>[] intervals)
        {
            this.intervals = intervals;
        }

        /// <summary>
        /// Gets the index'th element where index is in [0..Count-1].
        /// Throws IndexOutOfRangeException() if index is out of range.
        /// </summary>
        public uint this[int index]
        {
            get
            {
                int k = index;
                for (int i = 0; i < intervals.Length; i++)
                {
                    int ith_size = (int)intervals[i].Item2 - (int)intervals[i].Item1 + 1;
                    if (k < ith_size)
                        return intervals[i].Item1 + (uint)k;
                    else
                        k = k - ith_size;
                }
                throw new IndexOutOfRangeException();
            }
        }

        private int count = -1;

        /// <summary>
        /// Number of elements in the set
        /// </summary>
        public int Count
        {
            get
            {
                if (count == -1)
                {
                    int s = 0;
                    for (int i = 0; i < intervals.Length; i++)
                    {
                        s += (int)intervals[i].Item2 - (int)intervals[i].Item1 + 1;
                    }
                    count = s;
                }
                return count;
            }
        }

        public bool IsEmpty
        {
            get { return Count == 0; }
        }

        private static int CompareTuples(Tuple<uint, uint> x, Tuple<uint, uint> y)
        {
            return x.Item1.CompareTo(y.Item1);
        }

        internal static IntervalSet Merge(IEnumerable<IntervalSet> sets)
        {
            List<Tuple<uint, uint>> merged = new List<Tuple<uint, uint>>();
            foreach (var set in sets)
                merged.AddRange(set.intervals);

            merged.Sort(CompareTuples);
            return new IntervalSet(merged.ToArray());
        }

        public BDD AsBDD(BDDAlgebra alg)
        {
            var res = alg.False;
            for (int i = 0; i < intervals.Length; i++)
                res = res | alg.MkSetFromRange(intervals[i].Item1, intervals[i].Item2, 15);
            return res;
        }

        public IEnumerable<uint> Enumerate()
        {
            for (int i = 0; i < intervals.Length; i++)
            {
                for (uint j = intervals[i].Item1; j < intervals[i].Item2; j++)
                    yield return j;
                yield return intervals[i].Item2;
            }
        }

        internal string ToCharacterClass(bool isComplement)
        {
            if (IsEmpty)
                return "[0-[0]]";

            string res = "";
            uint m = intervals[0].Item1;
            uint n = intervals[0].Item2;
            for (int i = 1; i < intervals.Length; i++)
            {
                if (intervals[i].Item1 == n + 1)
                    n = intervals[i].Item2;
                else
                {
                    res += ToCharacterClassInterval(m, n);
                    m = intervals[i].Item1;
                    n = intervals[i].Item2;
                }
            }
            res += ToCharacterClassInterval(m, n);
            if (isComplement || res.Length > 1)
            {
                res = "[" + (isComplement ? "^" : "") + res + "]";
            }
            return res;
        }

        private static string ToCharacterClassInterval(uint m, uint n)
        {
            if (m == 0 && n == 0xFFFF)
                return ".";

            if (m == n)
                return StringUtility.Escape((char)m);

            string res = StringUtility.Escape((char)m);
            if (n > m + 1)
                res += "-";
            res += StringUtility.Escape((char)n);
            return res;
        }

        public override string ToString()
        {
            return ToCharacterClass(false);
        }

        #region custom serialization
        /// <summary>
        /// Serialize
        /// </summary>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            string s = Serialize();
            info.AddValue("i", s);
        }
        /// <summary>
        /// Deserialize
        /// </summary>
        public IntervalSet(SerializationInfo info, StreamingContext context)
        {
            string s = info.GetString("i");
            intervals = Deserialize(s);
        }

        /// <summary>
        /// Returns a string that can be parsed back to IntervalSet
        /// </summary>
        public string Serialize()
        {
            string s = "";
            for (int i=0; i < intervals.Length; i++)
            {
                if (i > 0)
                    s += ",";
                s += intervals[i].Item1.ToString();
                s += "-";
                s += intervals[i].Item2.ToString();
            }
            return s;
        }

        private static Tuple<uint, uint>[] Deserialize(string s)
        {
            Func<string, Tuple<uint, uint>> f = pair =>
            {
                string[] vals = pair.Split('-');
                return new Tuple<uint, uint>(uint.Parse(vals[0]), uint.Parse(vals[1]));
            };
            var intervals = Array.ConvertAll(s.Split(','), pair => f(pair));
            return intervals;
        }

        /// <summary>
        /// Parse the interval set from a string s that was produced with Serialize
        /// </summary>
        /// <param name="s">given serialization</param>
        public static IntervalSet Parse(string s)
        {
            var intervals = Deserialize(s);
            return new IntervalSet(intervals);
        }
        #endregion
    }
}
