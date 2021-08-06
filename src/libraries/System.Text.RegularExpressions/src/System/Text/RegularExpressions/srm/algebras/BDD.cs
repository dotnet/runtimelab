// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Represents a Binary Decision Diagram (BDD). Supports multi-terminal BDDs, i.e. ones where the leaves are
    /// something else the True or False, which are used for representing classifiers.
    /// </summary>
    internal sealed class BDD : IComparable
    {
        /// <summary>
        /// The ordinal for the True special value.
        /// </summary>
        private const int TrueOrdinal = -2;
        /// <summary>
        /// The ordinal for the False special value.
        /// </summary>
        private const int FalseOrdinal = -1;

        /// <summary>
        /// The unique BDD leaf that represents the full set or true.
        /// </summary>
        public static BDD True = new BDD(TrueOrdinal, null, null, specialValue: true);
        /// <summary>
        /// The unique BDD leaf that represents the empty set or false.
        /// </summary>
        public static BDD False = new BDD(FalseOrdinal, null, null, specialValue: true);

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
        /// Ordinal of this bit if nonleaf else MTBDD terminal value when nonnegative.
        /// </summary>
        public readonly int Ordinal;

        /// <summary>
        /// Preassigned hashcode value that respects equivalence: equivalent BDDs have equal hashcodes.
        /// </summary>
        private readonly int _hashcode;

        /// <summary>
        /// Representation of False for serialization.
        /// </summary>
        private static readonly long[] s_falseRepresentation = new long[] { 0 };

        /// <summary>
        /// Representation of True for serialization.
        /// </summary>
        private static readonly long[] s_trueRepresentation = new long[] { 1 };

        public BDD(int ordinal, BDD one, BDD zero) : this(ordinal, one, zero, specialValue: false) { }

        private BDD(int ordinal, BDD one, BDD zero, bool specialValue)
        {
            if (!specialValue && ordinal < 0)
                throw new ArgumentOutOfRangeException(nameof(ordinal), "Must be non-negative.");
            One = one;
            Zero = zero;
            Ordinal = ordinal;
            // Precompute a hash code value that respects BDD equivalence i.e.
            // two equivalent BDDs will always have the same hashcode that is
            // independent of object id values of the BDD objects.
            _hashcode = (ordinal, one, zero).GetHashCode();
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

            // Starting from all 0, bits will be flipped to 1 as necessary.
            ulong res = 0;

            // Follow the minimum path throught the branches to a True leaf.
            while (!set.IsLeaf)
            {
                if (set.Zero.IsEmpty)
                { // The bit must be set to 1 when the zero branch is False.
                    res = res | ((ulong)1 << set.Ordinal);
                    // If zero is empty then by the way BDDs are constructed one is not.
                    set = set.One;
                }
                else
                { // Otherwise, leaving the bit as 0 gives the smaller bitvector.
                    set = set.Zero;
                }
            }

            return res;
        }

        /// <summary>
        /// O(1) operation that returns the precomputed hashcode.
        /// </summary>
        public override int GetHashCode() => _hashcode;

        /// <summary>
        /// A shallow equality check that holds if ordinals are identical and one's are identical and zero's are
        /// identical. This equality is used in the _bddCache lookup.
        /// </summary>
        public override bool Equals(object? obj) =>
            obj is BDD bdd && (this == bdd || Ordinal == bdd.Ordinal && One == bdd.One && Zero == bdd.Zero);

        /// <summary>
        /// Returns a topologically sorted array of all the nodes (other than True or False) in this BDD such that, all
        /// MTBDD leaves (other than True or False) appear first in the array and all non-terminals with smaller ordinal
        /// appear before nodes with larger ordinal. So this BDD itself (if different from True or False) appears last.
        /// In the case of True or False returns the empty array.
        /// </summary>
        public BDD[] TopologicalSort()
        {
            if (IsFull || IsEmpty)
                return Array.Empty<BDD>();

            if (IsLeaf)
                return new BDD[] { this };

            // Produce the sorted nodes here.
            var sorted = new List<BDD>();
            // Non-terminals are first grouped by their ordinal into this array.
            var nonTerminals = new List<BDD>[Ordinal + 1];
            // Maintain sets of nodes to visit and already visited.
            var toVisit = new Stack<BDD>();
            var visited = new HashSet<BDD>();

            toVisit.Push(this);

            while (toVisit.Count > 0)
            {
                var node = toVisit.Pop();
                // True and False are not included in the result.
                if (node.IsFull || node.IsEmpty)
                    continue;

                if (node.IsLeaf)
                { // MTBDD terminals can be directly added to the sorted nodes.
                    sorted.Add(node);
                }
                else
                { // Non-terminals are grouped by their ordinal first.
                    if (nonTerminals[node.Ordinal] == null)
                        nonTerminals[node.Ordinal] = new List<BDD>();
                    nonTerminals[node.Ordinal].Add(node);
                    if (visited.Add(node.Zero))
                        toVisit.Push(node.Zero);
                    if (visited.Add(node.One))
                        toVisit.Push(node.One);
                }
            }

            // Flush the grouped non-terminals into the sorted nodes from smallest to highest ordinal. The highest
            // ordinal is guaranteed to have only one node, which places the root of the BDD at the end.
            for (int i = 0; i < nonTerminals.Length; i++)
            {
                if (nonTerminals[i] != null)
                    sorted.AddRange(nonTerminals[i]);
            }
            return sorted.ToArray();
        }

        #region Serialization
        /// <summary>
        /// Serialize this BDD in a flat ulong array. The BDD may have at most 2^k ordinals and 2^n nodes, st. k+2n&lt;64.
        /// BDD.False is represented by return value ulong[]{0}.
        /// BDD.True is represented by return value ulong[]{1}.
        /// Serializer uses more compacted representations when fewer bits are needed, which is reflected in the first
        /// two numbers of the return value. MTBDD terminals are represented by negated numbers as -id.
        /// </summary>
        public long[] Serialize()
        {
            if (IsEmpty)
                return s_falseRepresentation; // Represents False
            if (IsFull)
                return s_trueRepresentation; // Represents True
            if (IsLeaf)
                return new long[] { 0, 0, -Ordinal };

            BDD[] nodes = TopologicalSort();
#if DEBUG
            if (nodes[nodes.Length - 1] != this)
                throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);

            if (nodes.Length > (1 << 24))
                throw new AutomataException(AutomataExceptionKind.BDDSerializationNodeLimitViolation);
#endif
            // As few bits as possible are used to for ordinals and node identifiers for compact serialization.
            // Use at least a nibble (4 bits) to represent the ordinal and count how many are needed.
            int ordinalBits = 4;
            while (Ordinal >= (1 << ordinalBits))
                ordinalBits += 1;
            // Use at least 2 bits to represent the node identifier and count how many are needed.
            int nodeBits = 2;
            while (nodes.Length >= (1 << nodeBits))
                nodeBits += 1;

            // Reserve space for all nodes plus 2 extra: index 0 and 1 are reserved for False and True.
            long[] result = new long[nodes.Length + 2];
            // The values at indices 0 and 1 aren't actually needed for False and True, so they are used for storing
            // the number of bits used for ordinals and node identifiers in this serialization.
            result[0] = ordinalBits;
            result[1] = nodeBits;

            // Get shift amounts for the bit layout.
            int zeroNodeShift;
            int oneNodeShift;
            int ordinalShift;
            BitLayout(ordinalBits, nodeBits, out zeroNodeShift, out oneNodeShift, out ordinalShift);

            // Map each node to a unique identifier.
            var idMap = new Dictionary<BDD, long>();

            // True and False are set separately since they aren't included in the topological sort.
            idMap[True] = 1;
            idMap[False] = 0;

            // Give all nodes ascending identifiers and produce their serializations into the result.
            for (int i = 0; i < nodes.Length; i++)
            {
                BDD node = nodes[i];
                idMap[node] = i + 2; // Identifiers start from 2 since False and True are 0 and 1.

                if (node.IsLeaf)
                {
                    // This is MTBDD leaf. Negating it should make it less than or equal to zero, as True and False are
                    // excluded here and MTBDD Ordinals are required to be non-negative.
                    result[i + 2] = -node.Ordinal;
#if DEBUG
                    if (result[i + 2] > 0)
                        throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);
#endif
                }
                else
                {
                    // Combine ordinal and child identifiers according to the bit layout. Due to the topological sort
                    // the children are guaranteed to have identifiers already.
                    result[i + 2] = (((long)node.Ordinal) << ordinalShift) |
                                         (idMap[node.One] << oneNodeShift) |
                                        (idMap[node.Zero] << zeroNodeShift);
#if DEBUG
                    if (result[i + 2] <= 0)
                        throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);
#endif
                }
            }
            return result;
        }

        /// <summary>
        /// Recreates a BDD from a ulong array that has been created using Serialize. Is executed using a lock on
        /// algebra (if algebra != null) in a single thread mode. If no algebra is given (algebra == null) then creates
        /// the BDD without using a BDD algebra -- which implies that all BDD nodes other than True and False are new
        /// BDD objects that have not been internalized or cached.
        /// </summary>
        public static BDD Deserialize(long[] arcs, BDDAlgebra algebra = null)
        {
            if (arcs.Length == 1)
                return (arcs[0] == 0 ? False : True);

            if (algebra == null)
            {
                return DeserializeWithFactory(arcs, MkBDD);
            }
            else
            {
                lock (algebra)
                {
                    return DeserializeWithFactory(arcs, algebra.MkBDD);
                }
            }
        }

        // Private factory method used when deserializing without a BDD algebra.
        private static BDD MkBDD(int ordinal, BDD one, BDD zero)
        {
#if DEBUG
            if ((one == zero && one != null) || (one == null && zero != null) || (one != null && zero == null))
                throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);
#endif
            return new BDD(ordinal, one, zero);
        }

        private static BDD DeserializeWithFactory(long[] arcs, Func<int, BDD, BDD, BDD> mkBDD)
        {
            // The number of bits used for ordinals and node identifiers are stored in the first two values.
            int ordinalBits = (int)arcs[0];
            int nodeBits = (int)arcs[1];
            // Create bit masks for the sizes of ordinals and node identifiers.
            long ordinalMask = (1 << ordinalBits) - 1;
            long nodeMask = (1 << nodeBits) - 1;
            // Get the shift amounts for the bit layout.
            int zeroNodeShift;
            int oneNodeShift;
            int ordinalShift;
            BitLayout(ordinalBits, nodeBits, out zeroNodeShift, out oneNodeShift, out ordinalShift);

            // Store BDD nodes by their id when they are created.
            BDD[] nodes = new BDD[arcs.Length];

            // False and True aren't included in the serialization, so set them separately.
            nodes[0] = False;
            nodes[1] = True;

            for (int i = 2; i < arcs.Length; i++)
            {
                long arc = arcs[i];
                if (arc <= 0)
                {
                    // This is an MTBDD leaf. Its ordinal was serialized negated.
                    nodes[i] = mkBDD((int)-arc, null, null);
                }
                else
                {
                    // Reconstruct the ordinal and child identifiers for a non-terminal.
                    int ord = (int)((arc >> ordinalShift) & ordinalMask);
                    int oneId = (int)((arc >> oneNodeShift) & nodeMask);
                    int zeroId = (int)((arc >> zeroNodeShift) & nodeMask);
                    // The BDD nodes for the children are guaranteed to exist already due to the topological order.
                    nodes[i] = mkBDD(ord, nodes[oneId], nodes[zeroId]);
                }
            }
            // The root of the BDD is the last node.
            return nodes[nodes.Length - 1];
        }

        /// <summary>
        /// Gives the bit layout used in serialization.
        /// </summary>
        private static void BitLayout(int ordinalBits, int nodeBits, out int zeroNodeShift, out int oneNodeShift, out int ordinalShift)
        {
            // This bit layout seems to work best: zero,one,ordinal
            zeroNodeShift = ordinalBits + nodeBits;
            oneNodeShift = ordinalBits;
            ordinalShift = 0;
        }

        /// <summary>
        /// Invokes Serialize and appends the array as code_0.code_1. ... .code_{N-1} to sb where N is the length of the
        /// array returned by Serialize() and code_i is the encoding of the i'th element. Uses '.' as separator.
        /// </summary>
        public void Serialize(StringBuilder sb) => Base64.Encode(Serialize(), sb);

        public string SerializeToString()
        {
            StringBuilder sb = new StringBuilder();
            Serialize(sb);
            return sb.ToString();
        }

        /// <summary>
        /// Recreates a BDD from an input string that has been created using Serialize. Is executed using a lock on the
        /// algebra (if algebra != null) in a single thread mode. If no algebra is given (algebra == null) then creates
        /// the BDD without using any BDD algebra -- which implies that all BDD nodes other than True and False are new
        /// BDD objects that have not been internalized or cached.
        /// IMPORTANT: When created without any algebra the BDD can still be used as a classifier with Find that does
        /// not use or require any algebra.
        /// </summary>
        public static BDD Deserialize(string input, BDDAlgebra algebra = null) =>
            Deserialize(Base64.DecodeInt64Array(input), algebra);
        #endregion

        /// <summary>
        /// Finds the terminal for the input in a Multi-Terminal-BDD. Bits of the input are used to determine the path
        /// in the BDD. Returns -1 if False is reached and -2 if True is reached, else returns the MTBDD terminal number
        /// that is reached. If this is a nonterminal, Find does not care about input bits &gt; Ordinal.
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
        /// Finds the terminal for the input in a Multi-Terminal-BDD. Bits of the input are used to determine the path
        /// in the BDD. Returns -1 if False is reached and 0 if True is reached, else returns the MTBDD terminal number
        /// that is reached. If this is a nonterminal, Find does not care about input bits &gt; Ordinal.
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
            return Find(input) == TrueOrdinal;
        }

        /// <summary>
        /// Returns true if the only other terminal besides False is a MTBDD terminal that is different from True.
        /// If this is the case, outputs that terminal.
        /// </summary>
        public bool IsEssentiallyBoolean(out BDD terminalActingAsTrue)
        {
            // True and False are not MTBDD leaves.
            if (IsFull || IsEmpty)
            {
                terminalActingAsTrue = null;
                return false;
            }
            else if (IsLeaf) // Otherwise any leaf is an MTBDD leaf.
            {
                terminalActingAsTrue = this;
                return true;
            }

            // This will hold the unique MTBDD leaf.
            BDD leaf = null;

            // Maintain sets of nodes to visit and already visited.
            var toVisit = new Stack<BDD>();
            var visited = new HashSet<BDD>();

            toVisit.Push(this);

            // Consider all nodes to find an MTBDD leaf and check that it is unique (apart from False).
            while (toVisit.Count > 0)
            {
                var node = toVisit.Pop();
                if (node.IsEmpty)
                    continue;

                if (node.IsFull)
                {
                    // Contains the True leaf, so not essentially boolean in this method's sense.
                    terminalActingAsTrue = null;
                    return false;
                }

                if (node.IsLeaf)
                {
                    if (leaf == null)
                    {
                        // Remember the first MTBDD leaf seen.
                        leaf = node;
                    }
                    else if (leaf != node)
                    {
                        // Found two different MTBDD leaves.
                        terminalActingAsTrue = null;
                        return false;
                    }
                }
                else
                {
                    if (visited.Add(node.Zero))
                        toVisit.Push(node.Zero);
                    if (visited.Add(node.One))
                        toVisit.Push(node.One);
                }
            }
#if DEBUG
            // This should never happen because there must exist another leaf besides False since False itself was
            // handled separately.
            if (leaf == null)
                throw new AutomataException(AutomataExceptionKind.InternalError);
#endif
            // Found an MTBDD leaf and didn't find any other (non-False) leaves.
            terminalActingAsTrue = leaf;
            return true;
        }

        /// <summary>
        /// All terminals precede all non-terminals. Compares Ordinals for terminals. Compare non-terminals by comparing
        /// their minimal elements. If minimal elements are the same, compare Ordinals. This provides a total order for
        /// partitions.
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
            ulong bddMin = bdd.GetMin();
            return (min < bddMin ? -1 : (bddMin < min ? 1 : Ordinal.CompareTo(bdd.Ordinal)));
        }
    }
}
