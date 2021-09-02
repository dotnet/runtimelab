// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace System.Text.RegularExpressions.SRM
{
    internal sealed class RangeConverter
    {
        private readonly Dictionary<BDD, Tuple<uint, uint>[]> _rangeCache = new Dictionary<BDD, Tuple<uint, uint>[]>();

        private RangeConverter() { }

        /// <summary>
        /// Convert the set into an equivalent array of ranges.
        /// The ranges are nonoverlapping and ordered.
        /// </summary>
        public static Tuple<uint, uint>[] ToRanges(BDD set, int maxBit)
        {
            if (set.IsEmpty)
                return Array.Empty<Tuple<uint, uint>>();

            if (set.IsFull)
                return new Tuple<uint, uint>[] { new Tuple<uint, uint>(0, ((uint)1 << maxBit << 1) - 1) }; //note: maxBit could be 31

            var rc = new RangeConverter();
            return rc.LiftRanges(maxBit + 1, maxBit - set.Ordinal, rc.ToRanges1(set));
        }

        //e.g. if b = 6 and p = 2 and ranges = (in binary form) {[0000 1010, 0000 1110]} i.e. [x0A,x0E]
        //then res = {[0000 1010, 0000 1110], [0001 1010, 0001 1110],
        //            [0010 1010, 0010 1110], [0011 1010, 0011 1110]},
        private Tuple<uint, uint>[] LiftRanges(int b, int p, Tuple<uint, uint>[] ranges)
        {
            if (p == 0)
                return ranges;

            int k = b - p;
            uint maximal = ((uint)1 << k) - 1;

            Tuple<uint, uint>[] res = new Tuple<uint, uint>[(1 << p) * ranges.Length];
            int j = 0;
            for (uint i = 0; i < (1 << p); i++)
            {
                uint prefix = i << k;
                foreach (Tuple<uint, uint> range in ranges)
                    res[j++] = new Tuple<uint, uint>(range.Item1 | prefix, range.Item2 | prefix);
            }

            //the range wraps around : [0...][...2^k-1][2^k...][...2^(k+1)-1]
            if (ranges[0].Item1 == 0 && ranges[ranges.Length - 1].Item2 == maximal)
            {
                //merge consequtive ranges, we know that res has at least two elements here
                List<Tuple<uint, uint>> res1 = new List<Tuple<uint, uint>>();
                uint from = res[0].Item1;
                uint to = res[0].Item2;
                for (int i = 1; i < res.Length; i++)
                {
                    if (to == res[i].Item1 - 1)
                    {
                        to = res[i].Item2;
                    }
                    else
                    {
                        res1.Add(new Tuple<uint, uint>(from, to));
                        from = res[i].Item1;
                        to = res[i].Item2;
                    }
                }
                res1.Add(new Tuple<uint, uint>(from, to));
                res = res1.ToArray();
            }

            //CheckBug(res);
            return res;
        }

        private Tuple<uint, uint>[] ToRanges1(BDD set)
        {
            if (!_rangeCache.TryGetValue(set, out Tuple<uint, uint>[] ranges))
            {
                int b = set.Ordinal;
                uint mask = (uint)1 << b;
                if (set.Zero.IsEmpty)
                {
                    #region 0-case is empty
                    if (set.One.IsFull)
                    {
                        var range = new Tuple<uint, uint>(mask, (mask << 1) - 1);
                        ranges = new Tuple<uint, uint>[] { range };
                    }
                    else //1-case is neither full nor empty
                    {
                        Tuple<uint, uint>[] ranges1 = LiftRanges(b, b - set.One.Ordinal - 1, ToRanges1(set.One));
                        ranges = new Tuple<uint, uint>[ranges1.Length];
                        for (int i = 0; i < ranges1.Length; i++)
                        {
                            ranges[i] = new Tuple<uint, uint>(ranges1[i].Item1 | mask, ranges1[i].Item2 | mask);
                        }
                    }
                    #endregion
                }
                else if (set.Zero.IsFull)
                {
                    #region 0-case is full
                    if (set.One.IsEmpty)
                    {
                        var range = new Tuple<uint, uint>(0, mask - 1);
                        ranges = new Tuple<uint, uint>[] { range };
                    }
                    else
                    {
                        Tuple<uint, uint>[] rangesR = LiftRanges(b, b - set.One.Ordinal - 1, ToRanges1(set.One));
                        Tuple<uint, uint> range = rangesR[0];
                        if (range.Item1 == 0)
                        {
                            ranges = new Tuple<uint, uint>[rangesR.Length];
                            ranges[0] = new Tuple<uint, uint>(0, range.Item2 | mask);
                            for (int i = 1; i < rangesR.Length; i++)
                            {
                                ranges[i] = new Tuple<uint, uint>(rangesR[i].Item1 | mask, rangesR[i].Item2 | mask);
                            }
                        }
                        else
                        {
                            ranges = new Tuple<uint, uint>[rangesR.Length + 1];
                            ranges[0] = new Tuple<uint, uint>(0, mask - 1);
                            for (int i = 0; i < rangesR.Length; i++)
                            {
                                ranges[i + 1] = new Tuple<uint, uint>(rangesR[i].Item1 | mask, rangesR[i].Item2 | mask);
                            }
                        }
                    }
                    #endregion
                }
                else
                {
                    #region 0-case is neither full nor empty
                    Tuple<uint, uint>[] rangesL = LiftRanges(b, b - set.Zero.Ordinal - 1, ToRanges1(set.Zero));
                    Tuple<uint, uint> last = rangesL[rangesL.Length - 1];

                    if (set.One.IsEmpty)
                    {
                        ranges = rangesL;
                    }

                    else if (set.One.IsFull)
                    {
                        var ranges1 = new List<Tuple<uint, uint>>();
                        for (int i = 0; i < rangesL.Length - 1; i++)
                        {
                            ranges1.Add(rangesL[i]);
                        }

                        if (last.Item2 == (mask - 1))
                        {
                            ranges1.Add(new Tuple<uint, uint>(last.Item1, (mask << 1) - 1));
                        }
                        else
                        {
                            ranges1.Add(last);
                            ranges1.Add(new Tuple<uint, uint>(mask, (mask << 1) - 1));
                        }
                        ranges = ranges1.ToArray();
                    }
                    else //general case: neither 0-case, not 1-case is full or empty
                    {
                        Tuple<uint, uint>[] rangesR0 = ToRanges1(set.One);

                        Tuple<uint, uint>[] rangesR = LiftRanges(b, b - set.One.Ordinal - 1, rangesR0);

                        Tuple<uint, uint> first = rangesR[0];

                        if (last.Item2 == (mask - 1) && first.Item1 == 0) //merge together the last and first ranges
                        {
                            ranges = new Tuple<uint, uint>[rangesL.Length + rangesR.Length - 1];
                            for (int i = 0; i < rangesL.Length - 1; i++)
                            {
                                ranges[i] = rangesL[i];
                            }

                            ranges[rangesL.Length - 1] = new Tuple<uint, uint>(last.Item1, first.Item2 | mask);
                            for (int i = 1; i < rangesR.Length; i++)
                            {
                                ranges[rangesL.Length - 1 + i] = new Tuple<uint, uint>(rangesR[i].Item1 | mask, rangesR[i].Item2 | mask);
                            }
                        }
                        else
                        {
                            ranges = new Tuple<uint, uint>[rangesL.Length + rangesR.Length];
                            for (int i = 0; i < rangesL.Length; i++)
                            {
                                ranges[i] = rangesL[i];
                            }

                            for (int i = 0; i < rangesR.Length; i++)
                            {
                                ranges[rangesL.Length + i] = new Tuple<uint, uint>(rangesR[i].Item1 | mask, rangesR[i].Item2 | mask);
                            }
                        }

                    }
                    #endregion
                }
                _rangeCache[set] = ranges;
            }

            return ranges;
        }
    }
}
