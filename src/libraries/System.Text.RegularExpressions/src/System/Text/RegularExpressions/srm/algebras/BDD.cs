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
        public static BDD True = new BDD(-1);
        /// <summary>
        /// The unique BDD leaf that represents the full set or false.
        /// </summary>
        public static BDD False = new BDD(-2);

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
        private readonly int hashcode;

        /// <summary>
        /// Create a leaf
        /// </summary>
        private BDD(int ordinal)
        {
            Ordinal = ordinal;
            //let the ordinal also be the hashcode
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
            hashcode = (ordinal, one.hashcode, zero.hashcode).GetHashCode();
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

        public override int GetHashCode() => hashcode;
    }
}
