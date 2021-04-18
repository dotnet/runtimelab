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
        /// bit-width is now fixed to 16 --- essentially characters are 16-bit unsigned numbers
        /// </summary>
        private const int _bw = 16;

        /// <summary>
        /// Bound on precomputed character BDD, bound must be such that ToUpper and ToLower are in this range.
        /// </summary>
        private const int charPredTable_Length = 128;
        /// <summary>
        /// BDDs for all ASCII characters for fast lookup.
        /// </summary>
        private BDD[] charPredTable = new BDD[charPredTable_Length];
        /// <summary>
        /// BDDs for all ASCII characters in Case Insensitive mode for fast lookup.
        /// </summary>
        private BDD[] charPredTableIgnoreCase = new BDD[charPredTable_Length];

        internal const char Turkish_dotless_i = '\u0130';
        internal const char Kelvin_sign = '\u212A';

        internal BDD nonascii;

        /// <summary>
        /// Construct the solver for BitWidth.BV16
        /// </summary>
        public CharSetSolver()
        {
            //prefill the arrays: charPredTable and charPredTableIgnoreCase for ASCII
            for (char c = '\x00'; c < charPredTable_Length; c++)
                charPredTable[c] = MkSetFrom(c, _bw - 1);
            for (char c = '\x00'; c < charPredTable_Length; c++)
            {
                if (c == 'I')
                    charPredTableIgnoreCase[c] = MkOr(MkOr(charPredTable['I'], charPredTable['i']), MkSetFrom(Turkish_dotless_i, _bw - 1));
                else if (c == 'K')
                    charPredTableIgnoreCase[c] = MkOr(MkOr(charPredTable['K'], charPredTable['k']), MkSetFrom(Kelvin_sign, _bw - 1));
                else if (char.IsLetter(c))
                    charPredTableIgnoreCase[c] = MkOr(charPredTable[char.ToUpper(c)], charPredTable[char.ToLower(c)]);
                else
                    charPredTableIgnoreCase[c] = charPredTable[c];
            }
            nonascii = MkCharSetFromRange('\x80', '\uFFFF');
        }

        private IgnoreCaseTransformer _IgnoreCase;
        private IgnoreCaseTransformer IgnoreCase
        {
            get
            {
                if (_IgnoreCase == null)
                    _IgnoreCase = new IgnoreCaseTransformer(this);
                return _IgnoreCase;
            }
        }

        /// <summary>
        /// Make a character predicate for the given character c.
        /// If c is a lower case or upper case character and ignoreCase is true
        /// then add both the upper case and the lower case characters into the predicate.
        /// </summary>
        public BDD MkCharConstraint(char c, bool ignoreCase = false)
        {
            if (c < charPredTable_Length)
                return ignoreCase ? charPredTableIgnoreCase[c] : charPredTable[c];
            else
            {
                var bdd = MkSetFrom(c, _bw - 1);
                if (ignoreCase)
                    return IgnoreCase.Apply(bdd);
                else
                    return bdd;
            }
        }

        /// <summary>
        /// Make a CharSet from all the characters in the range from m to n.
        /// Returns the empty set if n is less than m
        /// </summary>
        public BDD MkCharSetFromRange(char m, char n)
        {
            return MkSetFromRange((uint)m, (uint)n, _bw-1);
        }

        /// <summary>
        /// Make a character set that is the union of the character sets of the given ranges.
        /// </summary>
        public BDD MkCharSetFromRanges(IEnumerable<Tuple<uint, uint>> ranges)
        {
            BDD res = False;
            foreach (var range in ranges)
                res = MkOr(res, MkSetFromRange(range.Item1, range.Item2, _bw -1));
            return res;
        }

        /// <summary>
        /// Make a character set of all the characters in the interval from c to d.
        /// If ignoreCase is true ignore cases for upper and lower case characters by including both versions.
        /// </summary>
        public BDD MkRangeConstraint(char c, char d, bool ignoreCase = false)
        {
            var res = MkSetFromRange((uint)c, (uint)d, _bw - 1);
            if (ignoreCase)
                res = IgnoreCase.Apply(res);
            return res;
        }

        /// <summary>
        /// Make a BDD encoding of k least significant bits of all the integers in the ranges
        /// </summary>
        internal BDD MkBddForIntRanges(IEnumerable<int[]> ranges)
        {
            BDD bdd = False;
            foreach (var range in ranges)
                bdd = MkOr(bdd, MkSetFromRange((uint)range[0], (uint)range[1], _bw - 1));
            return bdd;
        }

        #region Serialializing and deserializing BDDs

        /// <summary>
        /// Represent the set as an integer array.
        /// Assumes that the bdd has less than 2^14 nodes and at most 16 variables.
        /// </summary>
        internal int[] SerializeCompact(BDD bdd)
        {
            return SerializeCompact2(bdd);
        }

        /// <summary>
        /// Represent the set as an integer array.
        /// Assumes that the bdd has at most 2^14 nodes and at most 16 variables.
        /// </summary>
        private int[] SerializeCompact2(BDD bdd)
        {
            // encode the bdd directly
            //
            // the element at index 0 is the false node
            // the element at index 1 is the true node
            // and entry at index i>1 is node i and has the structure
            // (ordinal trueNode falseNode)
            // where ordinal uses 4 bits and trueNode and falseNode each use 14 bits
            // Assumes that the bdd has less than 2^14 nodes and at most 16 variables.
            // BDD.False is represented by int[]{0}.
            // BDD.True is represented by int[]{0,0}.
            // The root of the BDD (Other than True or False) is node 2

            if (bdd.IsEmpty)
                return new int[] { 0 };
            if (bdd.IsFull)
                return new int[] { 0, 0 };

            int nrOfNodes = bdd.CountNodes();

            if (nrOfNodes > (1 << 14))
                throw new AutomataException(AutomataExceptionKind.CompactSerializationNodeLimitViolation);

            int[] res = new int[nrOfNodes];


            //here we know that bdd is neither empty nor full
            var done = new Dictionary<BDD, int>();
            done[False] = 0;
            done[True] = 1;

            Stack<BDD> stack = new Stack<BDD>();
            stack.Push(bdd);
            done[bdd] = 2;

            int doneCount = 3;

            while (stack.Count > 0)
            {
                BDD b = stack.Pop();
                if (!done.ContainsKey(b.One))
                {
                    done[b.One] = (doneCount++);
                    stack.Push(b.One);
                }
                if (!done.ContainsKey(b.Zero))
                {
                    done[b.Zero] = (doneCount++);
                    stack.Push(b.Zero);
                }
                int bId = done[b];
                int fId = done[b.Zero];
                int tId = done[b.One];

                if (b.Ordinal > 15)
                    throw new AutomataException(AutomataExceptionKind.CompactSerializationBitLimitViolation);

                res[bId] = (b.Ordinal << 28) | (tId << 14) | fId;
            }
            return res;
        }

        /// <summary>
        /// Recreates a BDD from an int array that has been created using SerializeCompact
        /// </summary>
        internal BDD DeserializeCompact(int[] arcs)
        {
            lock (this)
            {
                return DeserializeCompact2(arcs);
            }
        }

        /// <summary>
        /// Recreates a BDD from an int array that has been created using SerializeCompact.
        /// Is executed in a single thread mode.
        /// </summary>
        private BDD DeserializeCompact2(int[] arcs)
        {
            if (arcs.Length == 1)
                return False;
            if (arcs.Length == 2)
                return True;

            //organized by order
            //note that all arcs are strictly increasing in levels
            var levels = new List<int>[16];

            BDD[] bddMap = new BDD[arcs.Length];
            bddMap[0] = False;
            bddMap[1] = True;

            for (int i = 2; i < arcs.Length; i++)
            {
                int x = ((arcs[i] >> 28) & 0xF);
                if (levels[x] == null)
                    levels[x] = new List<int>();
                levels[x].Add(i);
            }

            //create the BDD nodes according to the levels x
            //this is to ensure proper internalization
            for (int x = 0; x < 16; x++)
            {
                if (levels[x] != null)
                {
                    foreach (int i in levels[x])
                    {
                        int one = ((arcs[i] >> 14) & 0x3FFF);
                        int zero = (arcs[i] & 0x3FFF);
                        if (one > bddMap.Length || zero > bddMap.Length)
                            throw new AutomataException(AutomataExceptionKind.CompactDeserializationError);
                        var oneBranch = bddMap[one];
                        var zeroBranch = bddMap[zero];
                        var bdd = MkBDD((ushort)x, oneBranch, zeroBranch);
                        bddMap[i] = bdd;
                        if (bdd.Ordinal <= bdd.One.Ordinal || bdd.Ordinal <= bdd.Zero.Ordinal)
                            throw new AutomataException(AutomataExceptionKind.CompactDeserializationError);
                    }
                }
            }

            return bddMap[2];
        }
        #endregion

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

        /// <summary>
        /// Returns pred.
        /// </summary>
        public BDD MkCharPredicate(string name, BDD pred)
        {
            return pred;
        }

        public IEnumerable<char> GenerateAllCharacters(BDD bvSet, bool inRevereseOrder = false)
        {
            foreach (var c in GenerateAllElements(bvSet, inRevereseOrder))
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
            var card = ComputeDomainSize(set, _bw - 1);
            return card;
        }

        /// <summary>
        /// Returns true iff the set contains exactly one element.
        /// </summary>
        /// <param name="set">the given set</param>
        /// <returns>true iff the set is a singleton</returns>
        public bool IsSingleton(BDD set)
        {
            var card = ComputeDomainSize(set, _bw - 1);
            return card == (long)1;
        }

        /// <summary>
        /// Convert the set into an equivalent array of ranges. The ranges are nonoverlapping and ordered.
        /// If limit > 0 then returns null if the total number of ranges exceeds limit.
        /// </summary>
        public Tuple<uint, uint>[] ToRanges(BDD set, int limit = 0)
        {
            return ToRanges(set, _bw - 1, limit);
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

            var ranges = ToRanges(pred);
            //check if ranges represents a complement of a singleton
            if (ranges.Length == 2 && ranges[0].Item1 == 0 && ranges[1].Item2 == 0xFFFF &&
                ranges[0].Item2 + 2 == ranges[1].Item1)
                return "[^" + ((char)(ranges[0].Item2 + 1)) + "]";

            StringBuilder sb = new();
            for (int i = 0; i < ranges.Length; i++)
            {
                if (ranges[i].Item1 == ranges[i].Item2)
                    sb.Append((char)ranges[i].Item1);
                else if (ranges[i].Item2 == ranges[i].Item1 + 1)
                {
                    sb.Append((char)ranges[i].Item1);
                    sb.Append((char)ranges[i].Item2);
                }
                else
                {
                    sb.Append((char)ranges[i].Item1);
                    sb.Append('-');
                    sb.Append((char)ranges[i].Item2);
                }
            }
            if (ranges.Length > 1 || ranges[0].Item1 != ranges[0].Item2)
                return "[" + sb.ToString() + "]";
            else
                return sb.ToString();
        }
    }
}
