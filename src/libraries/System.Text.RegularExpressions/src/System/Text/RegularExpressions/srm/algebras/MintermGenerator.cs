// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Provides a generic implementation for minterm generation over a given Boolean Algebra. The minterms for a set of
    /// predicates are all their non-overlapping, satisfiable Boolean combinations. For example if the predicates are
    /// [0-9] and [0-4], then there are three minterms: [0-4], [5-9] and [^0-9]. Notably, there is no minterm
    /// corresponding to "[0-9] and not [0-4]", since that is unsatisfiable.
    /// </summary>
    /// <typeparam name="TPredicate">type of predicates</typeparam>
    internal sealed class MintermGenerator<TPredicate>
    {
        private readonly IBooleanAlgebra<TPredicate> _algebra;

        /// <summary>
        /// Constructs a minterm generator for a given Boolean Algebra.
        /// </summary>
        /// <param name="algebra">given Boolean Algebra</param>
        public MintermGenerator(IBooleanAlgebra<TPredicate> algebra)
        {
            // check that we can rely on equivalent predicates having the same hashcode, which EquivClass assumes
            Debug.Assert(algebra.HashCodesRespectEquivalence);

            _algebra = algebra;
        }

        /// <summary>
        /// Given an array of predidates {p_1, p_2, ..., p_n} where n>=0.
        /// Enumerate all satisfiable Boolean combinations Tuple({b_1, b_2, ..., b_n}, p)
        /// where p is satisfiable and equivalent to p'_1 &amp; p'_2 &amp; ... &amp; p'_n,
        /// where p'_i = p_i if b_i = true and p'_i is Not(p_i) otherwise.
        /// If n=0 return Tuple({},True).
        /// </summary>
        /// <param name="preds">array of predicates</param>
        /// <returns>all minterms of the given predicate sequence</returns>
        public IEnumerable<Tuple<bool[], TPredicate>> GenerateMinterms(params TPredicate[] preds)
        {
            if (preds.Length == 0)
            {
                yield return new Tuple<bool[], TPredicate>(Array.Empty<bool>(), _algebra.True);
                yield break;
            }

            // The minterms will be solved using non-equivalent predicates, i.e., the equivalence classes of preds. The
            // following code maps each predicate to an equivalence class and also stores for each equivalence class the
            // predicates belonging to it, so that a valuation for the original predicates may be reconstructed.

            // all the equivalence classes
            var equivClasses = new List<TPredicate>();
            // mapping from equivalcence classes to their members' indices
            var classIndices = new Dictionary<EquivalenceClass, int>();
            var memberLists = new List<List<int>>();

            for (int i = 0; i < preds.Length; i++)
            {
                // use a wrapper that overloads Equals to be logical equivalence as the key
                EquivalenceClass equiv = CreateEquivalenceClass(preds[i]);
                if (!classIndices.TryGetValue(equiv, out int classIndex))
                {
                    // if no equivalence class exists yet create a new index
                    classIndex = classIndices.Count;
                    classIndices[equiv] = classIndex;
                    equivClasses.Add(preds[i]);
                    memberLists.Add(new List<int>());
                }
                // add the index of the input predicate to its equivalence class
                memberLists[classIndex].Add(i);
            }

            var tree = new PartitionTree<TPredicate>(_algebra);
            // push each equivalence class into the partition tree
            foreach (TPredicate x in equivClasses)
            {
                tree.Refine(x);
            }

            // iterate through all minterms as the leaves of the partition tree
            foreach (PartitionTree<TPredicate> leaf in tree.GetLeaves())
            {
                // reconstruct a valuation of the original predicates for each minterm
                bool[] characteristic = new bool[preds.Length];
                // the path enumerates all the indices of all equivalence classes that are true in the minterm
                foreach (int k in leaf.GetPath())
                {
                    // for each equivalence class mark all of its members as true
                    foreach (int n in memberLists[k])
                    {
                        characteristic[n] = true;
                    }
                }

                yield return new Tuple<bool[], TPredicate>(characteristic, leaf._pred);
            }
        }

        private EquivalenceClass CreateEquivalenceClass(TPredicate set) => new EquivalenceClass(_algebra, set);

        /// <summary>
        /// Wraps a predicate as an equivalence class object whose Equals method is Equivalence checking
        /// </summary>
        private struct EquivalenceClass
        {
            private readonly TPredicate _set;
            private readonly IBooleanAlgebra<TPredicate> _algebra;

            internal EquivalenceClass(IBooleanAlgebra<TPredicate> algebra, TPredicate set)
            {
                _set = set;
                _algebra = algebra;
            }

            public override int GetHashCode() => _set.GetHashCode();

            public override bool Equals(object obj) => obj is EquivalenceClass ec && _algebra.AreEquivalent(_set, ec._set);
        }
    }

    /// <summary>
    /// A partition tree for efficiently solving minterms. Predicates are pushed into the tree with Refine(), which
    /// creates leaves in the tree for all satisfiable and non-overlapping combinations with any previously pushed
    /// predicates. At the end of the process the minterms can be read from the paths to the leaves of the tree.
    ///
    /// The valuations of the predicates are represented as follows. Given a path a^-1, a^0, a^1, ..., a^n, predicate
    /// p^i is true in the corresponding minterm if and only if a^i is the left child of a^i-1.
    ///
    /// This class assumes that all predicates passed to Refine() are non-equivalent.
    /// </summary>
    internal sealed class PartitionTree<TPredicate>
    {
        private readonly PartitionTree<TPredicate> _parent;
        private readonly int _index;
        internal readonly TPredicate _pred;
        private readonly IBooleanAlgebra<TPredicate> _solver;
        private PartitionTree<TPredicate> _left;
        private PartitionTree<TPredicate> _right; // complement

        /// <summary>
        /// Create the root of the partition tree. Nodes below this will be indexed starting from 0. The initial
        /// predicate is true.
        /// </summary>
        internal PartitionTree(IBooleanAlgebra<TPredicate> solver)
        {
            _solver = solver;
            _index = -1;
            _parent = null;
            _pred = solver.True;
            _left = null;
            _right = null;
        }

        private PartitionTree(IBooleanAlgebra<TPredicate> solver, int depth, PartitionTree<TPredicate> parent, TPredicate pred, PartitionTree<TPredicate> left, PartitionTree<TPredicate> right)
        {
            _solver = solver;
            _parent = parent;
            _index = depth;
            _pred = pred;
            _left = left;
            _right = right;
        }

        internal void Refine(TPredicate other)
        {
            if (_left == null && _right == null)
            {
                // if this is a leaf node create left and/or right children for the new predicate
                TPredicate thisAndOther = _solver.MkAnd(_pred, other);
                if (_solver.IsSatisfiable(thisAndOther))
                {
                    // the predicates overlap, now check if this is contained in other
                    TPredicate thisMinusOther = _solver.MkAnd(_pred, _solver.MkNot(other));
                    if (_solver.IsSatisfiable(thisMinusOther))
                    {
                        // this is not contained in other, both children are needed
                        _left = new PartitionTree<TPredicate>(_solver, _index + 1, this, thisAndOther, null, null);
                        // the right child corresponds to a conjunction with a negation, which matches thisMinusOther
                        _right = new PartitionTree<TPredicate>(_solver, _index + 1, this, thisMinusOther, null, null);
                    }
                    else // [[this]] subset of [[other]]
                    {
                        // other implies this, so populate the left child with this
                        _left = new PartitionTree<TPredicate>(_solver, _index + 1, this, _pred, null, null);
                    }
                }
                else // [[this]] subset of [[not(other)]]
                {
                    // negation of other implies this, so populate the right child with this
                    _right = new PartitionTree<TPredicate>(_solver, _index + 1, this, _pred, null, null); //other must be false
                }
            }
            else if (_left == null)
            {
                // no choice has to be made here, refine the single child that exists
                _right.Refine(other);
            }
            else if (_right == null)
            {
                // no choice has to be made here, refine the single child that exists
                _left.Refine(other);
            }
            else
            {
                TPredicate thisAndOther = _solver.MkAnd(_pred, other);
                if (_solver.IsSatisfiable(thisAndOther))
                {
                    // other is satisfiable in this subtree
                    TPredicate thisMinusOther = _solver.MkAnd(_pred, _solver.MkNot(other));
                    if (_solver.IsSatisfiable(thisMinusOther))
                    {
                        // but other does not imply this whole subtree, refine both children
                        _left.Refine(other);
                        _right.Refine(other);
                    }
                    else // [[this]] subset of [[other]]
                    {
                        // and other implies the whole subtree, include it in all minterms under here
                        _left.ExtendLeft();
                        _right.ExtendLeft();
                    }
                }
                else // [[this]] subset of [[not(other)]]
                {
                    // other is not satisfiable in this subtree, include its negation in all minterms under here
                    _left.ExtendRight();
                    _right.ExtendRight();
                }
            }
        }

        /// <summary>
        /// Include the next predicate in all minterms under this node. Assumes the next predicate implies the predicate
        /// of this node.
        /// </summary>
        private void ExtendLeft()
        {
            if (_left == null && _right == null)
            {
                _left = new PartitionTree<TPredicate>(_solver, _index + 1, this, _pred, null, null);
            }
            else
            {
                Debug.Assert(_left is not null || _right is not null);
                _left?.ExtendLeft();
                _right?.ExtendLeft();
            }
        }

        /// <summary>
        /// Include the negation of next predicate in all minterms under this node. Assumes the negation of the next
        /// predicate implies the predicate of this node.
        /// </summary>
        private void ExtendRight()
        {
            if (_left == null && _right == null)
            {
                _right = new PartitionTree<TPredicate>(_solver, _index + 1, this, _pred, null, null);
            }
            else
            {
                Debug.Assert(_left is not null || _right is not null);
                _left?.ExtendRight();
                _right?.ExtendRight();
            }
        }

        /// <summary>
        /// Enumerate all predicates included in this minterm in their non-negated form.
        /// </summary>
        internal IEnumerable<int> GetPath()
        {
            for (PartitionTree<TPredicate> curr = this; curr._parent != null; curr = curr._parent)
            {
                if (curr._parent._left == curr) //curr is the left child of its parent
                {
                    yield return curr._index;
                }
            }
        }

        /// <summary>
        /// Enumerate all of the leaves in the tree.
        /// </summary>
        internal IEnumerable<PartitionTree<TPredicate>> GetLeaves()
        {
            if (_left == null && _right == null)
            {
                yield return this;
            }
            else
            {
                if (_left != null)
                    foreach (PartitionTree<TPredicate> leaf in _left.GetLeaves())
                        yield return leaf;
                if (_right != null)
                    foreach (PartitionTree<TPredicate> leaf in _right.GetLeaves())
                        yield return leaf;
            }
        }
    }
}
