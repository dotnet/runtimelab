// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.IO;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Represents a Binary Decision Diagram.
    /// </summary>
    internal class BDD : IComparable
    {
        /// <summary>
        /// The unique BDD leaf that represents the full set or true.
        /// </summary>
        public static BDD True = new BDD(-2, null, null);
        /// <summary>
        /// The unique BDD leaf that represents the empty set or false.
        /// </summary>
        public static BDD False = new BDD(-1, null, null);

        /// <summary>
        /// The encoding of the set for lower ordinals for the case when the current bit is 1.
        /// The value is null iff IsLeaf is true.
        /// </summary>
        public readonly BDD One;

        /// <summary>
        /// The encoding of the set for lower ordinals for the case when the current bit is 0.
        /// The value is null iff IsLeaf is true.
        /// </summary>
        public readonly BDD Zero;

        /// <summary>
        /// Ordinal of this bit if nonleaf else MTBDD terminal value when nonnegative
        /// </summary>
        public readonly int Ordinal;

        /// <summary>
        /// Preassigned hashcode value that respects equivalence: equivalent BDDs have equal hashcodes
        /// </summary>
        internal readonly int hashcode;

        internal BDD(int ordinal, BDD one, BDD zero)
        {
            One = one;
            Zero = zero;
            Ordinal = ordinal;
            //precompute a hashchode value that respects BDD equivalence
            //i.e. two equivalent BDDs will always have the same hashcode
            //that is independent of object id values of the BDD objects
            hashcode = (ordinal, one, zero).GetHashCode();
        }

        /// <summary>
        /// True iff the node is a terminal (One and Zero are both null).
        /// True and False are terminals.
        /// </summary>
        public bool IsLeaf
        {
            get { return One == null; }
        }

        /// <summary>
        /// True iff the BDD is True.
        /// </summary>
        public bool IsFull
        {
            get { return this == True; }
        }

        /// <summary>
        /// True iff the BDD is False.
        /// </summary>
        public bool IsEmpty
        {
            get { return this == False; }
        }

        /// <summary>
        /// Counts the number of nodes (both terminals and nonterminals) in the BDD.
        /// </summary>
        public int CountNodes()
        {
            if (IsLeaf)
                return 1;

            HashSet<BDD> visited = new HashSet<BDD>();
            Stack<BDD> stack = new Stack<BDD>();
            stack.Push(this);
            visited.Add(this);
            while (stack.Count > 0)
            {
                BDD a = stack.Pop();
                if (!a.IsLeaf)
                {
                    if (visited.Add(a.One))
                        stack.Push(a.One);
                    if (visited.Add(a.Zero))
                        stack.Push(a.Zero);
                }
            }
            return visited.Count;
        }

        /// <summary>
        /// Gets the lexicographically minimum bitvector in this BDD as a ulong.
        /// The BDD must be nonempty.
        /// </summary>
        public ulong GetMin()
        {
            var set = this;

            if (set.IsFull)
                return 0;

            if (set.IsEmpty)
                throw new AutomataException(AutomataExceptionKind.SetIsEmpty);

            ulong res = 0;

            while (!set.IsLeaf)
            {
                if (set.Zero.IsEmpty) //the bit must be set to 1
                {
                    res = res | ((ulong)1 << set.Ordinal);
                    set = set.One;  //must follow the 1-branch
                }
                else
                    set = set.Zero;
            }

            return res;
        }

        /// <summary>
        /// Gets the lexicographically maximum bitvector in this BDD as a ulong.
        /// The BDD must be nonempty.
        /// </summary>
        public ulong GetMax()
        {
            var set = this;

            if (set.IsFull)
                return ulong.MaxValue;

            if (set.IsEmpty)
                throw new AutomataException(AutomataExceptionKind.SetIsEmpty);

            ulong res = ulong.MaxValue;

            while (!set.IsLeaf)
            {
                if (set.One.IsEmpty) //the bit must be set to 0
                {
                    res = res & ~((ulong)1 << set.Ordinal);
                    set = set.Zero; //must follow the 0-branch
                }
                else
                    set = set.One;
            }

            return res;
        }

        /// <summary>
        /// O(1) operation that returns the precomputed hashcode.
        /// </summary>
        public override int GetHashCode() => hashcode;

        /// <summary>
        /// A shallow equality check that holds if ordinals are identical and one's are identical and zero's are identical.
        /// This equality is used in the _bddCache lookup.
        /// </summary>
        public override bool Equals(object? obj) =>
            obj is BDD bdd && (this == bdd || Ordinal == bdd.Ordinal && One == bdd.One && Zero == bdd.Zero);

        /// <summary>
        /// Returns a topologically sorted array of all the nodes (other than True or False) in this BDD
        /// such that, all MTBDD leaves (other than True or False) appear first in the array
        /// and all nonterminals with smaller ordinal appear before nodes with larger ordinal.
        /// So this BDD itself (if different from True or False) appears last.
        /// In the case of True or False returns the empty array.
        /// </summary>
        public BDD[] TopSort()
        {
            if (IsFull || IsEmpty)
                return Array.Empty<BDD>();

            if (IsLeaf)
                return new BDD[] { this };

            //order the nodes according to their ordinals
            //into the nonterminals array
            var nonterminals = new List<BDD>[Ordinal + 1];
            var nodes = new List<BDD>();
            var stack = new Stack<BDD>();
            var set = new HashSet<BDD>();

            stack.Push(this);

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (node.IsFull || node.IsEmpty)
                    continue;

                if (node.IsLeaf)
                    nodes.Add(node);
                else
                {
                    if (nonterminals[node.Ordinal] == null)
                        nonterminals[node.Ordinal] = new List<BDD>();
                    nonterminals[node.Ordinal].Add(node);
                    if (set.Add(node.Zero))
                        stack.Push(node.Zero);
                    if (set.Add(node.One))
                        stack.Push(node.One);
                }
            }

            for (int i = 0; i < nonterminals.Length; i++)
                if (nonterminals[i] != null)
                    nodes.AddRange(nonterminals[i]);
            return nodes.ToArray();
        }

        #region Serialization
        private static readonly long[] s_False_repr = new long[] { 0 };
        private static readonly long[] s_True_repr = new long[] { 1 };
        /// <summary>
        /// Serialize this BDD in a flat ulong array.
        /// The BDD may have at most 2^k ordinals and 2^n nodes, st. k+2n&lt;64.
        /// BDD.False is represented by return value ulong[]{0}.
        /// BDD.True is represented by return value ulong[]{1}.
        /// Serializer uses more compacted representations when fewer bits are needed, which
        /// is reflected in the first two numbers of the return value.
        /// MTBDD terminals are represented by negated numbers as -id.
        /// </summary>
        public long[] Serialize()
        {
            if (IsEmpty)
                return s_False_repr; //represents False
            if (IsFull)
                return s_True_repr; //represents True
            if (IsLeaf)
                return new long[] { 0, 0, -Ordinal };

            BDD[] nodes = TopSort();
#if DEBUG
            if (nodes[nodes.Length - 1] != this)
                throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);

            if (nodes.Length > (1 << 24))
                throw new AutomataException(AutomataExceptionKind.BDDSerializationNodeLimitViolation);
#endif
            //use fewer bits when possible, starting with nibble size
            int ordinal_bits = 4;
            while (Ordinal >= (1 << ordinal_bits))
                ordinal_bits += 1;
            //use as few bits as possible for node identifiers, starting with 2 bits
            //this will give smaller and more compact serialized representations
            int node_bits = 2;
            while (nodes.Length >= (1 << node_bits))
                node_bits += 1;

            //add 2 extra positions: index 0 and 1 are reserved for False and True
            long[] res = new long[nodes.Length + 2];
            res[0] = Ordinal;
            res[1] = node_bits;

            //use the following bit layout
            int zero_node_shift;
            int one_node_shift;
            int ordinal_shift;
            bitlayout(ordinal_bits, node_bits, out zero_node_shift, out one_node_shift, out ordinal_shift);

            //here we know that bdd is neither False nor True
            //but it could still be a MTBDD leaf if both children are null
            var idmap = new Dictionary<BDD, int>();
            idmap[True] = 1;
            idmap[False] = 0;

            for (int i = 0; i < nodes.Length; i++)
            {
                BDD node = nodes[i];
                idmap[node] = i + 2;

                if (node.IsLeaf)
                    //this is MTBDD leaf: negate the value (it may be 0)
                    //because True and False are excluded from TopSort()
                    res[i + 2] = -node.Ordinal;
                else
                    //children ids are well-defined due to the topological order of nodes
                    res[i + 2] = (node.Ordinal << ordinal_shift) | (idmap[node.One] << one_node_shift) | (idmap[node.Zero] << zero_node_shift);
            }
            return res;
        }

        /// <summary>
        /// Recreates a BDD from a ulong array that has been created using Serialize.
        /// Is executed using a lock on algebra (if algebra != null) in a single thread mode.
        /// If no algebra is given (algebra == null) then creates the BDD without using a BDD algebra --
        /// which implies that all BDD nodes other than True and False are new BDD objects
        /// that have not been internalized or cached.
        /// </summary>
        public static BDD Deserialize(long[] arcs, BDDAlgebra algebra = null)
        {
            if (arcs.Length == 1)
                return (arcs[0] == 0 ? False : True);

            if (algebra == null)
                return Deserialize_(arcs, MkBDD);
            else
                lock (algebra)
                    return Deserialize_(arcs, algebra.MkBDD);
        }

        private static BDD MkBDD(int ordinal, BDD one, BDD zero)
        {
            return new BDD(ordinal, one, zero);
        }

        private static BDD Deserialize_(long[] arcs, Func<int, BDD, BDD, BDD> mkBDD)
        {
            int k = arcs.Length;
            int maxordinal = (int)arcs[0]; //the root ordinal
            int ordinal_bits = 4;
            while (maxordinal >= (1 << ordinal_bits))
                ordinal_bits += 1;
            long ordinal_mask = (1 << ordinal_bits) - 1;
            int node_bits = (int)arcs[1];    //how many bits are used in a node id
            long node_mask = (1 << node_bits) - 1;
            int zero_node_shift;
            int one_node_shift;
            int ordinal_shift;
            bitlayout(ordinal_bits, node_bits, out zero_node_shift, out one_node_shift, out ordinal_shift);
            BDD[] nodes = new BDD[k];
            nodes[0] = False;
            nodes[1] = True;
            for (int i = 2; i < k; i++)
            {
                long arc = arcs[i];
                if (arc <= 0)
                    nodes[i] = mkBDD((int)-arc, null, null);
                else
                {
                    int ord = (int)((arc >> ordinal_shift) & ordinal_mask);
                    int oneId = (int)((arc >> one_node_shift) & node_mask);
                    int zeroId = (int)((arc >> zero_node_shift) & node_mask);
                    nodes[i] = mkBDD(ord, nodes[oneId], nodes[zeroId]);
                }
            }
            //the result is the final BDD in the nodes array
            return nodes[k - 1];
        }

        /// <summary>
        /// Use this bit layout in the serialization
        /// </summary>
        private static void bitlayout(int ordinal_bits, int node_bits, out int zero_node_shift, out int one_node_shift, out int ordinal_shift)
        {
            //this bit layout seems to work best: zero,one,ord
            zero_node_shift = ordinal_bits + node_bits;
            one_node_shift = ordinal_bits;
            ordinal_shift = 0;
        }

        /// <summary>
        /// Invokes Serialize and appends the array as code_0.code_1. ... .code_{N-1} to sb
        /// where N is the length of the array returned by Serialize() and code_i is the cencoding of the i'th element.
        /// Uses '.' as separator.
        /// </summary>
        public void Serialize(StringBuilder sb)
        {
            long[] res = Serialize();
            sb.Append(Int64ToString(res[0]));
            for (int i = 1; i < res.Length; i++)
            {
                sb.Append('.');
                sb.Append(Int64ToString(res[i]));
            }
        }

        public string SerializeToString()
        {
            StringBuilder sb = new StringBuilder();
            Serialize(sb);
            return sb.ToString();
        }

        #region Custom Base64 representation for long
        private static char[] s_customBase64alphabet = new char[64] {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
            '+', '/'
        };
        private static byte[] s_customBase64lookup = new byte[128] {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            62, //'+' maps to 62
            0, 0, 0,
            63, //'/' maps to 63
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9, //digits map to 0..9
            0, 0, 0, 0, 0, 0, 0,
            10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, //uppercase letters
            0, 0, 0, 0, 0, 0,
            36, 37, 38, 39, 40, 41, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, //lowercase letters
            0, 0, 0, 0, 0
        };

        /// <summary>
        /// Custom Base64 serializer for long.
        /// </summary>
        private static string Int64ToString(long n)
        {
            if (n == 0)
                return "0";

            string negSign = (n < 0 ? "-" : "");
            if (n < 0) n = -n;

            string res = "";
            while (n > 0)
            {
                res = s_customBase64alphabet[n & 0x3F] + res;
                n = n >> 6;
            }
            return  negSign + res;
        }

        /// <summary>
        /// Custom Base64 deserializer for long.
        /// </summary>
        private static long Int64FromString(string s)
        {
            bool isNegative = (s[0] == '-');
            int start = (isNegative ? 1 : 0);
            long res = 0;
            for (int i = start; i < s.Length; i++)
                res = (res << 6) | s_customBase64lookup[s[i]];
            return res;
        }
        #endregion

        /// <summary>
        /// Recreates a BDD from an input string that has been created using Serialize.
        /// Is executed using a lock on the algebra (if algebra != null) in a single thread mode.
        /// If no algebra is given (algebra == null) then creates the BDD without using any BDD algebra --
        /// which implies that all BDD nodes other than True and False are new BDD objects
        /// that have not been internalized or cached. When created without any algebra the BDD
        /// can still be used as a classifier with Find that does not use or require any algebra.
        /// </summary>
        public static BDD Deserialize(string input, BDDAlgebra algebra = null)
        {
            string[] elems = input.Split('.');
            long[] arcs = Array.ConvertAll(elems, Int64FromString);
            return Deserialize(arcs, algebra);
        }

        #endregion

        /// <summary>
        /// Finds the terminal for the input in a Multi-Terminal-BDD.
        /// Bits of the input are used to determine the path in the BDD.
        /// Returns -1 if False is reached and -2 if True is reached,
        /// else returns the MTBDD terminal number that is reached.
        /// If this is a nonterminal, Find does not care about input bits &gt; Ordinal.
        /// </summary>
        public int Find(int input)
        {
            if (IsLeaf)
                return Ordinal;
            else if ((input & (1 << Ordinal)) == 0)
                return Zero.Find(input);
            else
                return One.Find(input);
        }

        /// <summary>
        /// Finds the terminal for the input in a Multi-Terminal-BDD.
        /// Bits of the input are used to determine the path in the BDD.
        /// Returns -1 if False is reached and 0 if True is reached,
        /// else returns the MTBDD terminal number that is reached.
        /// If this is a nonterminal, Find does not care about input bits &gt; Ordinal.
        /// </summary>
        public int Find(ulong input)
        {
            if (IsLeaf)
                return Ordinal;
            else if ((input & ((ulong)1 << Ordinal)) == 0)
                return Zero.Find(input);
            else
                return One.Find(input);
        }

        /// <summary>
        /// Returns the serialized form of the BDD. Useful for Debugging.
        /// </summary>
        public override string ToString()
        {
            return SerializeToString();
        }

        /// <summary>
        /// Assumes BDD is not MTBDD and returns true iff it contains the input.
        /// (Otherwise use BDD.Find if this is if fact a MTBDD.)
        /// </summary>
        public bool Contains(int input)
        {
            return Find(input) == -2; //-2 is the Ordinal of BDD.True
        }

        /// <summary>
        /// Returns true if the only other terminal besides False is a MTBDD terminal that is different from True.
        /// If this is the case, outputs that terminal.
        /// </summary>
        public bool IsEssentiallyBoolean(out BDD terminalActingAsTrue)
        {
            if (IsFull || IsEmpty)
            {
                terminalActingAsTrue = null;
                return false;
            }

            if (IsLeaf)
            {
                terminalActingAsTrue = this;
                return true;
            }

            var stack = new Stack<BDD>();
            var set = new HashSet<BDD>();

            stack.Push(this);

            BDD leaf = null;

            while (stack.Count > 0)
            {
                var node = stack.Pop();
                if (node.IsEmpty)
                    continue;

                if (node.IsFull)
                {
                    //contains the True leaf
                    terminalActingAsTrue = null;
                    return false;
                }

                if (node.IsLeaf)
                {
                    if (leaf == null)
                        //first time that we see a MTBDD terminal
                        leaf = node;
                    else if (leaf != node)
                    {
                        //there are two different MTBDD leaves present
                        terminalActingAsTrue = null;
                        return false;
                    }
                }
                else
                {
                    if (set.Add(node.Zero))
                        stack.Push(node.Zero);
                    if (set.Add(node.One))
                        stack.Push(node.One);
                }
            }
#if DEBUG
            if (leaf == null)
                //this should never happen because there must exist another leaf besides False
                throw new AutomataException(AutomataExceptionKind.InternalError);
#endif
            terminalActingAsTrue = leaf;
            return true;
        }

        /// <summary>
        /// All terminals precede all nonterminals. Compares Ordinals for terminals.
        /// Compare non-terminals by comparing their minimal elements.
        /// If minimal elements are the same, compare Ordinals.
        /// This provides a total order for partitions.
        /// </summary>
        public int CompareTo(object? obj)
        {
            BDD bdd = obj as BDD;
            if (bdd == null)
                return -1;
            if (IsLeaf && bdd.IsLeaf)
                return (Ordinal < bdd.Ordinal ? -1 : (Ordinal == bdd.Ordinal ? 0 : 1));
            if (IsLeaf && !bdd.IsLeaf)
                return -1;
            if (!IsLeaf && bdd.IsLeaf)
                return 1;
            ulong min = GetMin();
            ulong bdd_min = bdd.GetMin();
            return (min < bdd_min ? -1 : (bdd_min < min ? 1 : Ordinal.CompareTo(bdd.Ordinal)));
        }
    }
}
