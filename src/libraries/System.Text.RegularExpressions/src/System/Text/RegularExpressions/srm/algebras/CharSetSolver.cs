// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
//using RestrictKeyType = System.Int64;
using System.IO;
using System.Text.RegularExpressions;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Provides functionality to build character sets, to perform boolean operations over character sets,
    /// and to construct an SFA over character sets from a regex.
    /// Character sets are represented by bitvector sets.
    /// </summary>
    internal class CharSetSolver : BDDAlgebra, ICharAlgebra<BDD>
    {
        /// <summary>
        /// BDDs for all characters for fast lookup.
        /// </summary>
        private BDD[] charPredTable = new BDD[0x10000];

        internal BDD nonascii;

        /// <summary>
        /// Construct the solver.
        /// </summary>
        public CharSetSolver()
        {
            nonascii = MkCharSetFromRange('\x80', '\uFFFF');
            _IgnoreCase = new Unicode.IgnoreCaseTransformer(this);
        }

        private Unicode.IgnoreCaseTransformer _IgnoreCase;

        /// <summary>
        /// Make a character predicate for the given character c.
        /// </summary>
        public BDD MkCharConstraint(char c, bool ignoreCase = false, string culture = null)
        {
            if (ignoreCase)
            {
                return _IgnoreCase.Apply(c, culture);
            }
            else
            {
                //individual character BDDs are always fixed
                if (charPredTable[c] == null)
                    charPredTable[c] = MkSetFrom(c, 15);
                return charPredTable[c];
            }
        }

        /// <summary>
        /// Make a CharSet from all the characters in the range from m to n.
        /// Returns the empty set if n is less than m
        /// </summary>
        public BDD MkCharSetFromRange(char m, char n)
        {
            if (m == n)
                return MkCharConstraint(m);
            return MkSetFromRange((uint)m, (uint)n, 15);
        }

        /// <summary>
        /// Make a character set that is the union of the character sets of the given ranges.
        /// </summary>
        public BDD MkCharSetFromRanges(IEnumerable<Tuple<uint, uint>> ranges)
        {
            BDD res = False;
            foreach (var range in ranges)
                res = MkOr(res, MkSetFromRange(range.Item1, range.Item2, 15));
            return res;
        }

        /// <summary>
        /// Make a character set of all the characters in the interval from c to d.
        /// If ignoreCase is true ignore cases for upper and lower case characters by including both versions.
        /// </summary>
        public BDD MkRangeConstraint(char c, char d, bool ignoreCase = false, string culture = null)
        {
            if (c == d)
                return MkCharConstraint(c, ignoreCase, culture);

            var res = MkSetFromRange((uint)c, (uint)d, 15);
            if (ignoreCase)
                res = _IgnoreCase.Apply(res, culture);
            return res;
        }

        /// <summary>
        /// Make a BDD encoding of k least significant bits of all the integers in the ranges
        /// </summary>
        internal BDD MkBddForIntRanges(IEnumerable<int[]> ranges)
        {
            BDD bdd = False;
            foreach (var range in ranges)
                bdd = MkOr(bdd, MkSetFromRange((uint)range[0], (uint)range[1], 15));
            return bdd;
        }

        /// <summary>
        /// Identity function, returns s.
        /// </summary>
        public BDD ConvertFromCharSet(BDDAlgebra _, BDD s)
        {
            return s;
        }

        /// <summary>
        /// Returns this character set solver.
        /// </summary>
        public CharSetSolver CharSetProvider
        {
            get { return this; }
        }

        public IEnumerable<char> GenerateAllCharacters(BDD bvSet, bool inReverseOrder = false)
        {
            foreach (var c in GenerateAllElements(bvSet, inReverseOrder))
                yield return (char)c;
        }

        public IEnumerable<char> GenerateAllCharacters(BDD set)
        {
            return GenerateAllCharacters(set, false);
        }


        /// <summary>
        /// Calculate the number of elements in the set.
        /// </summary>
        /// <param name="set">the given set</param>
        /// <returns>the cardinality of the set</returns>
        public ulong ComputeDomainSize(BDD set)
        {
            var card = ComputeDomainSize(set, 15);
            return card;
        }

        /// <summary>
        /// Returns true iff the set contains exactly one element.
        /// </summary>
        /// <param name="set">the given set</param>
        /// <returns>true iff the set is a singleton</returns>
        public bool IsSingleton(BDD set)
        {
            var card = ComputeDomainSize(set, 15);
            return card == (long)1;
        }

        /// <summary>
        /// Convert the set into an equivalent array of ranges. The ranges are nonoverlapping and ordered.
        /// If limit > 0 then returns null if the total number of ranges exceeds limit.
        /// </summary>
        public Tuple<uint, uint>[] ToRanges(BDD set, int limit = 0)
        {
            return ToRanges(set, 15, limit);
        }

        private IEnumerable<uint> GenerateAllCharactersInOrder(BDD set)
        {
            var ranges = ToRanges(set);
            foreach (var range in ranges)
                for (uint i = range.Item1; i <= range.Item2; i++)
                    yield return (uint)i;
        }

        private IEnumerable<uint> GenerateAllCharactersInReverseOrder(BDD set)
        {
            var ranges = ToRanges(set);
            for (int j = ranges.Length - 1; j >= 0; j--)
                for (uint i = ranges[j].Item2; i >= ranges[j].Item1; i--)
                    yield return (char)i;
        }

        /// <summary>
        /// Generate all characters that are members of the set in alphabetical order, smallest first, provided that inReverseOrder is false.
        /// </summary>
        /// <param name="set">the given set</param>
        /// <param name="inReverseOrder">if true the members are generated in reverse alphabetical order with the largest first, otherwise in alphabetical order</param>
        /// <returns>enumeration of all characters in the set, the enumeration is empty if the set is empty</returns>
        public IEnumerable<uint> GenerateAllElements(BDD set, bool inReverseOrder)
        {
            if (set == False)
                return GenerateNothing();
            else if (inReverseOrder)
                return GenerateAllCharactersInReverseOrder(set);
            else
                return GenerateAllCharactersInOrder(set);
        }

        private IEnumerable<uint> GenerateNothing()
        {
            yield break;
        }

        public BDD ConvertToCharSet(ICharAlgebra<BDD> _, BDD pred)
        {
            return pred;
        }

        public BDD[] GetPartition()
        {
            throw new NotSupportedException();
        }

        public string PrettyPrint(BDD pred)
        {
            if (pred.IsEmpty)
                return "[]";

            //check if pred is full, show this case with a dot
            if (pred.IsFull)
                return ".";

            #region try to optimize representation involving common direct use of \d \w and \s to avoid blowup of ranges
            if (SRM.Regex.s_unicode != null)
            {
                BDD digit = Regex.s_unicode.CategoryCondition(8);
                if (pred == Regex.s_unicode.WordLetterCondition)
                    return @"\w";
                if (pred == Regex.s_unicode.WhiteSpaceCondition)
                    return @"\s";
                if (pred == digit)
                    return @"\d";
                if (pred == MkNot(Regex.s_unicode.WordLetterCondition))
                    return @"\W";
                if (pred == MkNot(Regex.s_unicode.WhiteSpaceCondition))
                    return @"\S";
                if (pred == MkNot(digit))
                    return @"\D";
            }
            #endregion

            var ranges = ToRanges(pred);

            if (IsSingletonRange(ranges))
                return StringUtility.Escape((char)ranges[0].Item1);

            #region if too many ranges try to optimize the representation using \d \w etc.
            if (SRM.Regex.s_unicode != null && ranges.Length > 10)
            {
                BDD w = Regex.s_unicode.WordLetterCondition;
                BDD W = MkNot(w);
                BDD d = Regex.s_unicode.CategoryCondition(8);
                BDD D = MkNot(d);
                BDD asciiDigit = MkCharSetFromRange('0', '9');
                BDD nonasciiDigit = MkAnd(d, MkNot(asciiDigit));
                BDD s = Regex.s_unicode.WhiteSpaceCondition;
                BDD S = MkNot(s);
                BDD wD = MkAnd(w, D);
                BDD SW = MkAnd(S, W);
                //s, d, wD, SW are the 4 main large minterms
                //note: s|SW = W, d|wD = w
                //
                // Venn Diagram: s and w do not overlap, and d is contained in w
                // ------------------------------------------------
                // |                                              |
                // |              SW     ------------(w)--------  |
                // |   --------          |                     |  |
                // |   |      |          |        ----------   |  |
                // |   |  s   |          |  wD    |        |   |  |
                // |   |      |          |        |   d    |   |  |
                // |   --------          |        |        |   |  |
                // |                     |        ----------   |  |
                // |                     -----------------------  |
                // ------------------------------------------------
                //
                //-------------------------------------------------------------------
                // singletons
                //---
                if (MkOr(s, pred) == s)
                    return RepresentSetInPattern("[^\\S{0}]", MkAnd(s, MkNot(pred)));
                //---
                if (MkOr(d, pred) == d)
                    return RepresentSetInPattern("[^\\D{0}]", MkAnd(d, MkNot(pred)));
                //---
                if (MkOr(wD, pred) == wD)
                    return RepresentSetInPattern("[\\w-[\\d{0}]]", MkAnd(wD, MkNot(pred)));
                //---
                if (MkOr(SW, pred) == SW)
                    return RepresentSetInPattern("[^\\s\\w{0}]", MkAnd(SW, MkNot(pred)));
                //-------------------------------------------------------------------
                // unions of two
                // s|SW
                if (MkOr(W, pred) == W)
                {
                    string repr1 = null;
                    if (MkAnd(s, pred) == s)
                        //pred contains all of \s and is contained in \W
                        repr1 = RepresentSetInPattern("[\\s{0}]", MkAnd(S, pred));
                    //the more common case is that pred is not \w and not some specific non-word character such as ':'
                    string repr2 = RepresentSetInPattern("[^\\w{0}]", MkAnd(W, MkNot(pred)));
                    if (repr1 != null && repr1.Length < repr2.Length)
                        return repr1;
                    else
                        return repr2;
                }
                //---
                // s|d
                BDD s_or_d = MkOr(s, d);
                if (pred == s_or_d)
                    return "[\\s\\d]";
                if (MkOr(s_or_d, pred) == s_or_d)
                {
                    //check first if this is purely ascii range for digits
                    if (MkAnd(pred, s).Equals(s) && MkAnd(pred, nonasciiDigit).IsEmpty)
                        return string.Format("[\\s{0}]", RepresentRanges(ToRanges(MkAnd(pred, asciiDigit)), false));
                    else
                        return RepresentSetInPattern("[\\s\\d-[{0}]]", MkAnd(s_or_d, MkNot(pred)));
                }
                //---
                // s|wD
                BDD s_or_wD = MkOr(s, wD);
                if (MkOr(s_or_wD, pred) == s_or_wD)
                    return RepresentSetInPattern("[\\s\\w-[\\d{0}]]", MkAnd(s_or_wD, MkNot(pred)));
                //---
                // d|wD
                if (MkOr(w, pred) == w)
                    return RepresentSetInPattern("[\\w-[{0}]]", MkAnd(w, MkNot(pred)));
                //---
                // d|SW
                BDD d_or_SW = MkOr(d, SW);
                if (pred == d_or_SW)
                    return "\\d|[^\\s\\w]";
                if (MkOr(d_or_SW, pred) == d_or_SW)
                    return RepresentSetInPattern("[\\d-[{0}]]|[^\\s\\w{1}]", MkAnd(d, MkNot(pred)), MkAnd(SW, MkNot(pred)));
                // wD|SW = S&D
                BDD SD = MkOr(wD, SW);
                if (MkOr(SD, pred) == SD)
                    return RepresentSetInPattern("[^\\s\\d{0}]", MkAnd(SD, MkNot(pred)));
                //-------------------------------------------------------------------
                //unions of three
                // s|SW|wD = D
                if (MkOr(D, pred) == D)
                    return RepresentSetInPattern("[^\\d{0}]", MkAnd(D, MkNot(pred)));
                // SW|wD|d = S
                if (MkOr(S, pred) == S)
                    return RepresentSetInPattern("[^\\s{0}]", MkAnd(S, MkNot(pred)));
                // s|SW|d = complement of wD = W|d
                BDD W_or_d = MkNot(wD);
                if (MkOr(W_or_d, pred) == W_or_d)
                    return RepresentSetInPattern("[\\W\\d-[{0}]]", MkAnd(W_or_d, MkNot(pred)));
                // s|wD|d = s|w
                BDD s_or_w = MkOr(s, w);
                if (MkOr(s_or_w, pred) == s_or_w)
                    return RepresentSetInPattern("[\\s\\w-[{0}]]", MkAnd(s_or_w, MkNot(pred)));
                //-------------------------------------------------------------------
                //touches all four minterms, typically happens as the fallback arc in .* extension
            }
            #endregion

            //rpresent either the ranges or its complemet,
            //if the complement representation is more copmpact
            string ranges_repr = "[" + RepresentRanges(ranges, false) + "]";
            string ranges_compl_repr = "[^" + RepresentRanges(ToRanges(MkNot(pred)), false) + "]";
            if (ranges_repr.Length <= ranges_compl_repr.Length)
                return ranges_repr;
            else
                return ranges_compl_repr;
        }


        private string RepresentSetInPattern(string pat, BDD set)
        {
            var str = (set.IsEmpty ? "" : RepresentRanges(ToRanges(set)));
            var res = string.Format(pat, str);
            return res;
        }

        private string RepresentSetInPattern(string pat, BDD set1, BDD set2)
        {
            var str1 = (set1.IsEmpty ? "" : RepresentRanges(ToRanges(set1)));
            var str2 = (set2.IsEmpty ? "" : RepresentRanges(ToRanges(set2)));
            var res = string.Format(pat, str1, str2);
            return res;
        }

        private static string RepresentRanges(Tuple<uint, uint>[] ranges, bool checkSingletonComlement = true)
        {
            //check if ranges represents a complement of a singleton
            if (checkSingletonComlement && ranges.Length == 2 &&
                ranges[0].Item1 == 0 && ranges[1].Item2 == 0xFFFF &&
                ranges[0].Item2 + 2 == ranges[1].Item1)
                    return "^" + (StringUtility.Escape((char)(ranges[0].Item2 + 1)));

            StringBuilder sb = new();
            for (int i = 0; i < ranges.Length; i++)
            {
                if (ranges[i].Item1 == ranges[i].Item2)
                    sb.Append(StringUtility.Escape((char)ranges[i].Item1));
                else if (ranges[i].Item2 == ranges[i].Item1 + 1)
                {
                    sb.Append(StringUtility.Escape((char)ranges[i].Item1));
                    sb.Append(StringUtility.Escape((char)ranges[i].Item2));
                }
                else
                {
                    sb.Append(StringUtility.Escape((char)ranges[i].Item1));
                    sb.Append('-');
                    sb.Append(StringUtility.Escape((char)ranges[i].Item2));
                }
            }
            return sb.ToString();
        }

        private static bool IsSingletonRange(Tuple<uint, uint>[] ranges) => ranges.Length == 1 && ranges[0].Item1 == ranges[0].Item2;
    }
}
