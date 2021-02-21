// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Represents a Binary Decision Diagram.
    /// </summary>
    internal class BDD
    {
        /// <summary>
        /// The unique BDD leaf that represents the empty set or true.
        /// </summary>
        public static BDD True = new BDD(-2);
        /// <summary>
        /// The unique BDD leaf that represents the full set or false.
        /// </summary>
        public static BDD False = new BDD(-1);

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
        /// Ordinal of this bit if nonleaf
        /// </summary>
        public readonly int Ordinal;

        /// <summary>
        /// Preassigned hashcode value that respects equivalence
        /// </summary>
        internal readonly int hashcode;

        /// <summary>
        /// Create a leaf
        /// </summary>
        private BDD(int ordinal)
        {
            Ordinal = ordinal;
            hashcode = (ordinal, 0, 0).GetHashCode();
        }

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
        /// Assumes that this BDD is nonempty and that its ordinal is at most 63.
        /// </summary>
        public ulong GetMin()
        {
            var set = this;

            if (set.IsFull)
                return (ulong)0;

            if (set.IsEmpty)
                throw new AutomataException(AutomataExceptionKind.SetIsEmpty);

            if (set.Ordinal > 63)
                throw new AutomataException(AutomataExceptionKind.OrdinalIsTooLarge);

            ulong res = 0;

            while (!set.IsLeaf)
            {
                if (set.Zero.IsEmpty) //the bit must be set to 1
                {
                    res = res | ((ulong)1 << set.Ordinal);
                    set = set.One;
                }
                else
                    set = set.Zero;
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
        /// Returns a topologically sorted array of all the nodes in this BDD
        /// such that, all leaves appear first in the array
        /// and all nonterminals with smaller ordinal
        /// appear before nodes with larger ordinal.
        /// So this BDD itself appears last.
        /// </summary>
        public BDD[] TopSort()
        {
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

        /// <summary>
        /// Serialize this BDD in a flat ulong array.
        /// The BDD may have at most 2^16 bits and 2^24 nodes.
        /// The ordinal values must fit within 16 bits also for multi-terminal leaves.
        /// BDD.False is represented by return value ulong[]{0}.
        /// BDD.True is represented by return value ulong[]{0,0}.
        /// In all other cases:
        /// Index 0 represents False.
        /// Index 1 represents True.
        /// and entry at index i&gt;1 is node i and has the structure:
        /// BDD: (ordinal &lt;&lt; 48) | (oneNode &lt;&lt; 24) | zeroNode.
        /// or MTBDD Leaf: (ordinal &lt;&lt; 48) that has both children == 0
        /// This BDD (when different from True and False) is the final element.
        /// </summary>
        public ulong[] Serialize()
        {
            if (IsEmpty)
                return new ulong[] { 0 }; //False
            if (IsFull)
                return new ulong[] { 0, 0 }; //True

            BDD[] nodes = TopSort();
#if DEBUG
            if (nodes[nodes.Length - 1] != this)
                throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);

            if (nodes.Length > (1 << 24))
                throw new AutomataException(AutomataExceptionKind.BDDSerializationNodeLimitViolation);
#endif

            //add 2 extra positions (just in case) index 0 and 1 are reserved for False and True
            //in MTBDD case it may happen that 2 more solts are needed (0 and 1)
            //but this is only possible when neither True nor False occur in this BDD
            //in all other situations res will have the same length as nodes
            List<ulong> res = new List<ulong>(nodes.Length + 2);

            //here we know that bdd is neither False nor True
            //but it could still be a MTBDD leaf if both children are null
            var idmap = new Dictionary<BDD, int>();
            idmap[True] = 1;
            idmap[False] = 0;

            //the next avaliable node id
            int id = 2;

            for (int i = 0; i < nodes.Length; i++)
            {
                BDD node = nodes[i];
                if (node == True || node == False)
                    continue;

                int node_id = id++;
                idmap[node] = node_id;

                if (node.IsLeaf)
                    //MTBDD leaf
                    res[node_id] = ((ulong)node.Ordinal) << 48;
                else
                    //children ids are well-defined due to the topological order of nodes
                    res[node_id] = (((ulong)node.Ordinal) << 48) | (((ulong)idmap[node.One]) << 24) | ((uint)idmap[node.Zero]);
            }
            return res.ToArray();
        }

        /// <summary>
        /// Recreates a BDD from a ulong array that has been created using Serialize.
        /// Is executed using a lock on algebra (if algebra != null) in a single thread mode.
        /// If no algebra is given (algebra == null) then creates the BDD without using a BDD algebra --
        /// which implies that all BDD nodes other than True and False are new BDD objects
        /// that have not been internalized or cached.
        /// </summary>
        public static BDD Deserialize(ulong[] arcs, BDDAlgebra algebra = null)
        {
            if (arcs.Length == 1)
                return False;
            if (arcs.Length == 2)
                return True;

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

        private static BDD Deserialize_(ulong[] arcs, Func<int, BDD, BDD, BDD> mkBDD)
        {
            int k = arcs.Length;
            BDD[] nodes = new BDD[k];
            nodes[0] = False;
            nodes[1] = True;
            for (int i = 2; i < k; i++)
            {
                ushort ord = (ushort)(arcs[i] >> 48);
                int oneId = (int)((arcs[i] >> 24) & 0xFFFFFF);
                int zeroId = (int)(arcs[i] & 0xFFFFFF);
                if (oneId == 0 && zeroId == 0)
                    //make a MTBDD terminal
                    nodes[i] = mkBDD(ord, null, null);
                else
                    nodes[i] = mkBDD(ord, nodes[oneId], nodes[zeroId]);
            }
            //the result is the final BDD in the nodes array
            return nodes[k - 1];
        }

        /// <summary>
        /// Invokes Serialize and appends the array as code_0.code_1. ... .code_{N-1} to sb
        /// where N is the length of the array returned by Serialize() and code_i is the
        /// hexadecimal encoding of the i'th element using [0-9A-F] digits.
        /// Uses '.' as separator.
        /// </summary>
        public void Serialize(StringBuilder sb)
        {
            ulong[] res = Serialize();
            sb.Append(res[0].ToString("X"));
            for (int i = 1; i < res.Length; i++)
            {
                sb.Append('.');
                sb.Append(res[i].ToString("X"));
            }
        }

        /// <summary>
        /// Recreates a BDD from an input string that has been created using Serialize.
        /// Is executed using a lock on algebra (if algebra != null) in a single thread mode.
        /// If no algebra is given (algebra == null) then creates the BDD without using any BDD algebra --
        /// which implies that all BDD nodes other than True and False are new BDD objects
        /// that have not been internalized or cached.
        /// </summary>
        public static BDD Deserialize(string input, BDDAlgebra algebra = null)
        {
            string[] elems = input.Split('.');
            ulong[] arcs = Array.ConvertAll(elems, ulong.Parse);
            return Deserialize(arcs, algebra);
        }

        /// <summary>
        /// Finds the terminal for the input in a Multi-Terminal-BDD.
        /// Bits of the input are used to determine the path in the BDD.
        /// Returns -1 if False is reached and -2 if True is reached,
        /// else returns the MTBDD terminal id that is reached.
        /// </summary>
        public int FindTerminal(int input)
        {
            if (IsLeaf)
                return Ordinal;
            else if ((input & (1 << Ordinal)) == 0)
                return Zero.FindTerminal(input);
            else
                return One.FindTerminal(input);
        }

        /// <summary>
        /// Finds the terminal for the input in a Multi-Terminal-BDD.
        /// Bits of the input are used to determine the path in the BDD.
        /// Returns -1 if False is reached and -2 if True is reached,
        /// else returns the MTBDD terminal id that is reached.
        /// </summary>
        public int FindTerminal(ulong input)
        {
            if (IsLeaf)
                return Ordinal;
            else if ((input & ((ulong)1 << Ordinal)) == 0)
                return Zero.FindTerminal(input);
            else
                return One.FindTerminal(input);
        }
    }
}
