using System;
using System.Collections.Generic;

namespace Microsoft.SRM
{
    /// <summary>
    /// Represents a Binary Decision Diagram.
    /// </summary>
    internal class BDD
    {
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


        public readonly BDDAlgebra algebra;

        /// <summary>
        /// Ordinal of this bit if nonleaf
        /// </summary>
        public readonly int Ordinal;

        internal BDD(BDDAlgebra algebra, int ordinal, BDD one, BDD zero)
        {
            this.One = one;
            this.Zero = zero;
            this.Ordinal = ordinal;
            this.algebra = algebra;
        }

        /// <summary>
        /// True iff the node is a terminal (One and Zero are null).
        /// </summary>
        public bool IsLeaf
        {
            get { return One == null; }
        }

        /// <summary>
        /// True iff the set is full.
        /// </summary>
        public bool IsFull
        {
            get { return this == algebra.True; }
        }

        /// <summary>
        /// True iff the set is empty.
        /// </summary>
        public bool IsEmpty
        {
            get { return this == algebra.False; }
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

        public static BDD operator >>(BDD x, int k)
        {
            return x.algebra.ShiftRight(x, k);
        }

        public static BDD operator <<(BDD x, int k)
        {
            return x.algebra.ShiftLeft(x, k);
        }

        public static BDD operator &(BDD x, BDD y)
        {
            return x.algebra.MkAnd(x, y);
        }

        public static BDD operator |(BDD x, BDD y)
        {
            return x.algebra.MkOr(x, y);
        }

        public static BDD operator !(BDD x)
        {
            return x.algebra.MkNot(x);
        }
    }
}

