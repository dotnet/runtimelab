using System;
using System.Collections.Generic;
//using RestrictKeyType = System.Int64;
using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.SRM
{
    /// <summary>
    /// Provides functionality to build character sets, to perform boolean operations over character sets,
    /// and to construct an SFA over character sets from a regex.
    /// Character sets are represented by bitvector sets.
    /// </summary>
    internal class CharSetSolver : BDDAlgebra, ICharAlgebra<BDD>
    {

        private int _bw;

        public BitWidth Encoding
        {
            get { return (BitWidth)_bw; }
        }

        /// <summary>
        /// Construct the solver for BitWidth.BV16
        /// </summary>
        public CharSetSolver() : this(BitWidth.BV16)
        {
        }

        /// <summary>
        /// Construct a character set solver for the given character encoding (nr of bits).
        /// </summary>
        public CharSetSolver(BitWidth bits) : base()
        {
            if (!CharacterEncodingTool.IsSpecified(bits))
                throw new AutomataException(AutomataExceptionKind.CharacterEncodingIsUnspecified);
            _bw = (int)bits;
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

        private BDD[] charPredTable = new BDD[1 << 16];

        /// <summary>
        /// Make a character containing the given character c.
        /// If c is a lower case or upper case character and ignoreCase is true
        /// then add both the upper case and the lower case characters.
        /// </summary>
        public BDD MkCharConstraint(char c, bool ignoreCase = false)
        {
            int i = (int)c;
            if (charPredTable[i] == null)
                charPredTable[i] = MkSetFrom((uint)c, _bw - 1);
            if (ignoreCase)
                return IgnoreCase.Apply(charPredTable[i]);
            return charPredTable[i];
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
            //return SerializeBasedOnRanges(bdd);
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
            //return DeserializeBasedOnRanges(arcs);
            return DeserializeCompact2(arcs);
        }

        /// <summary>
        /// Recreates a BDD from an int array that has been created using SerializeCompact
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
                        var bdd = MkBvSet(x, oneBranch, zeroBranch);
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
        public BDD ConvertFromCharSet(BDD s)
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

        public BDD ConvertToCharSet(BDDAlgebra alg, BDD pred)
        {
            return pred;
        }

        #region code generation

        public BDD[] GetPartition()
        {
            throw new NotSupportedException();
        }

        #endregion

        public override string SerializePredicate(BDD s)
        {
            throw new NotImplementedException();
        }

        public override BDD DeserializePredicate(string s)
        {
            throw new NotImplementedException();
        }
    }
}
