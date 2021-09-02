// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Represents a Binary Decision Diagram.
    /// </summary>
    internal sealed class BDD : IComparable
    {
        /// <summary>
        /// The unique BDD leaf that represents the full set or true.
        /// </summary>
        public static readonly BDD True = new BDD(-2, null, null);
        /// <summary>
        /// The unique BDD leaf that represents the empty set or false.
        /// </summary>
        public static readonly BDD False = new BDD(-1, null, null);

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
        internal readonly int _hashcode;

        internal BDD(int ordinal, BDD one, BDD zero)
        {
            One = one;
            Zero = zero;
            Ordinal = ordinal;

            // Precompute a hashchode value that respects BDD equivalence.
            // Two equivalent BDDs will always have the same hashcode
            // that is independent of object id values of the BDD objects.
            _hashcode = HashCode.Combine(ordinal, one, zero);
        }

        /// <summary>
        /// True iff the node is a terminal (One and Zero are both null).
        /// True and False are terminals.
        /// </summary>
        public bool IsLeaf => One is null;

        /// <summary>
        /// True iff the BDD is True.
        /// </summary>
        public bool IsFull => this == True;

        /// <summary>
        /// True iff the BDD is False.
        /// </summary>
        public bool IsEmpty => this == False;

        /// <summary>
        /// Counts the number of nodes (both terminals and nonterminals) in the BDD.
        /// </summary>
        public int CountNodes()
        {
            if (IsLeaf)
                return 1;

            HashSet<BDD> visited = new();
            Stack<BDD> stack = new();
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
            BDD set = this;

            if (set.IsFull)
                return 0;

            if (set.IsEmpty)
                throw new AutomataException(AutomataExceptionKind.SetIsEmpty);

            ulong res = 0;

            while (!set.IsLeaf)
            {
                if (set.Zero.IsEmpty) //the bit must be set to 1
                {
                    res |= (ulong)1 << set.Ordinal;
                    set = set.One;  //must follow the 1-branch
                }
                else
                {
                    set = set.Zero;
                }
            }

            return res;
        }

        /// <summary>
        /// Gets the lexicographically maximum bitvector in this BDD as a ulong.
        /// The BDD must be nonempty.
        /// </summary>
        public ulong GetMax()
        {
            BDD set = this;

            if (set.IsFull)
                return ulong.MaxValue;

            if (set.IsEmpty)
                throw new AutomataException(AutomataExceptionKind.SetIsEmpty);

            ulong res = ulong.MaxValue;

            while (!set.IsLeaf)
            {
                if (set.One.IsEmpty) //the bit must be set to 0
                {
                    res &= ~((ulong)1 << set.Ordinal);
                    set = set.Zero; //must follow the 0-branch
                }
                else
                {
                    set = set.One;
                }
            }

            return res;
        }

        /// <summary>
        /// O(1) operation that returns the precomputed hashcode.
        /// </summary>
        public override int GetHashCode() => _hashcode;

        /// <summary>
        /// A shallow equality check that holds if ordinals are identical and one's are identical and zero's are identical.
        /// This equality is used in the _bddCache lookup.
        /// </summary>
        public override bool Equals(object? obj) =>
            obj is BDD bdd &&
            (this == bdd || Ordinal == bdd.Ordinal && One == bdd.One && Zero == bdd.Zero);

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
                BDD node = stack.Pop();
                if (node.IsFull || node.IsEmpty)
                    continue;

                if (node.IsLeaf)
                {
                    nodes.Add(node);
                }
                else
                {
                    (nonterminals[node.Ordinal] ??= new List<BDD>()).Add(node);

                    if (set.Add(node.Zero))
                        stack.Push(node.Zero);

                    if (set.Add(node.One))
                        stack.Push(node.One);
                }
            }

            for (int i = 0; i < nonterminals.Length; i++)
            {
                if (nonterminals[i] != null)
                {
                    nodes.AddRange(nonterminals[i]);
                }
            }

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

            Debug.Assert(nodes[nodes.Length - 1] == this);
            Debug.Assert(nodes.Length <= (1 << 24));

            //use fewer bits when possible, starting with nibble size
            int ordinal_bits = 4;
            while (Ordinal >= (1 << ordinal_bits))
            {
                ordinal_bits += 1;
            }

            //use as few bits as possible for node identifiers, starting with 2 bits
            //this will give smaller and more compact serialized representations
            int node_bits = 2;
            while (nodes.Length >= (1 << node_bits))
            {
                node_bits += 1;
            }

            //add 2 extra positions: index 0 and 1 are reserved for False and True
            long[] res = new long[nodes.Length + 2];
            res[0] = ordinal_bits;
            res[1] = node_bits;

            //use the following bit layout
            BitLayout(ordinal_bits, node_bits, out int zero_node_shift, out int one_node_shift, out int ordinal_shift);

            //here we know that bdd is neither False nor True
            //but it could still be a MTBDD leaf if both children are null
            var idmap = new Dictionary<BDD, long>
            {
                [True] = 1,
                [False] = 0
            };

            for (int i = 0; i < nodes.Length; i++)
            {
                BDD node = nodes[i];
                idmap[node] = i + 2;

                if (node.IsLeaf)
                {
                    //this is MTBDD leaf: negate the value (it may be 0)
                    //because True and False are excluded from TopSort()
                    res[i + 2] = -node.Ordinal;
                }
                else
                {
                    long v = (((long)node.Ordinal) << ordinal_shift) | (idmap[node.One] << one_node_shift) | (idmap[node.Zero] << zero_node_shift);
                    Debug.Assert(v >= 0);
                    res[i + 2] = v; //children ids are well-defined due to the topological order of nodes
                }
            }
            return res;
        }

        /// <summary>
        /// Recreates a BDD from a ulong array that has been created using Serialize.
        /// Is executed using a lock on algebra (if algebra != null) in a single thread mode.
        /// If no algebra is given (algebra is null) then creates the BDD without using a BDD algebra --
        /// which implies that all BDD nodes other than True and False are new BDD objects
        /// that have not been internalized or cached.
        /// </summary>
        public static BDD Deserialize(long[] arcs, BDDAlgebra algebra = null)
        {
            if (arcs.Length == 1)
                return arcs[0] == 0 ? False : True;

            if (algebra is null)
                return Deserialize_(arcs, MkBDD);

            lock (algebra)
            {
                return Deserialize_(arcs, algebra.MkBDD);
            }
        }

        private static BDD MkBDD(int ordinal, BDD one, BDD zero)
        {
            Debug.Assert(one != zero || one is null);
            Debug.Assert(one is not null || zero is null);
            Debug.Assert(one is null || zero is not null);
            return new BDD(ordinal, one, zero);
        }

        private static BDD Deserialize_(long[] arcs, Func<int, BDD, BDD, BDD> mkBDD)
        {
            int k = arcs.Length;
            int ordinal_bits = (int)arcs[0];
            long ordinal_mask = (1 << ordinal_bits) - 1;
            int node_bits = (int)arcs[1];    //how many bits are used in a node id
            long node_mask = (1 << node_bits) - 1;
            BitLayout(ordinal_bits, node_bits, out int zero_node_shift, out int one_node_shift, out int ordinal_shift);

            BDD[] nodes = new BDD[k];
            nodes[0] = False;
            nodes[1] = True;

            for (int i = 2; i < k; i++)
            {
                long arc = arcs[i];
                if (arc <= 0)
                {
                    nodes[i] = mkBDD((int)-arc, null, null);
                }
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
        private static void BitLayout(int ordinal_bits, int node_bits, out int zero_node_shift, out int one_node_shift, out int ordinal_shift)
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
        public void Serialize(StringBuilder sb) => Base64.Encode(Serialize(), sb);

        public string SerializeToString()
        {
            var sb = new StringBuilder();
            Serialize(sb);
            return sb.ToString();
        }

        /// <summary>
        /// Recreates a BDD from an input string that has been created using Serialize.
        /// Is executed using a lock on the algebra (if algebra != null) in a single thread mode.
        /// If no algebra is given (algebra is null) then creates the BDD without using any BDD algebra --
        /// which implies that all BDD nodes other than True and False are new BDD objects
        /// that have not been internalized or cached.
        /// IMPORTANT: When created without any algebra the BDD
        /// can still be used as a classifier with Find that does not use or require any algebra.
        /// </summary>
        public static BDD Deserialize(ReadOnlySpan<char> input, BDDAlgebra algebra = null) =>
            Deserialize(Base64.DecodeInt64Array(input), algebra);
        #endregion

        /// <summary>
        /// Finds the terminal for the input in a Multi-Terminal-BDD.
        /// Bits of the input are used to determine the path in the BDD.
        /// Returns -1 if False is reached and -2 if True is reached,
        /// else returns the MTBDD terminal number that is reached.
        /// If this is a nonterminal, Find does not care about input bits &gt; Ordinal.
        /// </summary>
        public int Find(int input) =>
            IsLeaf ? Ordinal :
            (input & (1 << Ordinal)) == 0 ? Zero.Find(input) :
            One.Find(input);

        /// <summary>
        /// Finds the terminal for the input in a Multi-Terminal-BDD.
        /// Bits of the input are used to determine the path in the BDD.
        /// Returns -1 if False is reached and 0 if True is reached,
        /// else returns the MTBDD terminal number that is reached.
        /// If this is a nonterminal, Find does not care about input bits &gt; Ordinal.
        /// </summary>
        public int Find(ulong input) =>
            IsLeaf ? Ordinal :
            (input & ((ulong)1 << Ordinal)) == 0 ? Zero.Find(input) :
            One.Find(input);

        /// <summary>
        /// Returns the serialized form of the BDD. Useful for Debugging.
        /// </summary>
        public override string ToString() => SerializeToString();

        /// <summary>
        /// Assumes BDD is not MTBDD and returns true iff it contains the input.
        /// (Otherwise use BDD.Find if this is if fact a MTBDD.)
        /// </summary>
        public bool Contains(int input) => Find(input) == -2; //-2 is the Ordinal of BDD.True

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
                BDD node = stack.Pop();
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
                    if (leaf is null)
                    {
                        //first time that we see a MTBDD terminal
                        leaf = node;
                    }
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

            Debug.Assert(leaf is not null, "this should never happen because there must exist another leaf besides False");
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
            if (obj is not BDD bdd)
                return -1;

            if (IsLeaf)
            {
                if (bdd.IsLeaf)
                {
                    return
                        Ordinal < bdd.Ordinal ? -1 :
                        Ordinal == bdd.Ordinal ? 0 :
                        1;
                }

                return -1;
            }

            if (bdd.IsLeaf)
                return 1;

            ulong min = GetMin();
            ulong bdd_min = bdd.GetMin();
            return
                min < bdd_min ? -1 :
                bdd_min < min ? 1 :
                Ordinal.CompareTo(bdd.Ordinal);
        }
    }
}
