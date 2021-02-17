// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace System.Text.RegularExpressions.SRM
{

    /// <summary>
    /// Provides a generic implementation for minterm generation over a given Boolean Algebra.
    /// </summary>
    /// <typeparam name="PRED">type of predicates</typeparam>
    internal class MintermGenerator<PRED>
    {
        private IBooleanAlgebra<PRED> _ba;

        /// <summary>
        /// Constructs a minterm generator for a given Boolean Algebra.
        /// </summary>
        /// <param name="ba">given Boolean Algebra</param>
        public MintermGenerator(IBooleanAlgebra<PRED> ba)
        {
#if DEBUG
            if (!ba.HashCodesRespectEquivalence)
                //cannot rely on equivalent predicates having the same hashcode
                //so all predicates would end up in the same bucket that causes a linear search
                //with Equals to check equivalence --- this case must never arise here
                throw new AutomataException(AutomataExceptionKind.InternalError_SymbolicRegex);
#endif
            _ba = ba;
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
        public IEnumerable<Tuple<bool[], PRED>> GenerateMinterms(params PRED[] preds)
        {
            if (preds.Length == 0)
            {
                yield return new Tuple<bool[], PRED>(Array.Empty<bool>(), _ba.True);
            }
            else
            {
                var count = preds.Length;

                List<PRED> nonequivalentSets = new List<PRED>();

                //work only with nonequivalent sets as distinct elements
                var indexLookup = new Dictionary<int, int>();
                var newIndexMap = new Dictionary<EquivClass, int>();
                var equivs = new List<List<int>>();

                for (int i = 0; i < count; i++)
                {
                    int newIndex;
                    EquivClass equiv = CreateEquivalenceClass(preds[i]);
                    if (!newIndexMap.TryGetValue(equiv, out newIndex))
                    {
                        newIndex = newIndexMap.Count;
                        newIndexMap[equiv] = newIndex;
                        nonequivalentSets.Add(preds[i]);
                        equivs.Add(new List<int>());
                    }
                    indexLookup[i] = newIndex;
                    equivs[newIndex].Add(i);
                }

                //var pairs = new List<Tuple<IntSet, PRED>>(GenerateMinterms1(nonequivalentSets.ToArray()));
                //foreach (var pair in pairs)
                //{
                //    var characteristic = new bool[preds.Length];
                //    for (int i = 0; i < count; i++)
                //        if (pair.First.Contains(indexLookup[i]))
                //            characteristic[i] = true;
                //    yield return
                //        new Tuple<bool[], PRED>(characteristic, pair.Second);
                //}

                var tree = new PartitonTree<PRED>(_ba);
                foreach (var psi in nonequivalentSets)
                    tree.Refine(psi);
                foreach (var leaf in tree.GetLeaves())
                {
                    var characteristic = new bool[preds.Length];
                    foreach (var k in leaf.GetPath())
                        foreach (var n in equivs[k])
                            characteristic[n] = true;
                    yield return
                        new Tuple<bool[], PRED>(characteristic, leaf.phi);
                }
            }
        }

        private EquivClass CreateEquivalenceClass(PRED set)
        {
            return new EquivClass(_ba, set);
        }

        /// <summary>
        /// Wraps a predicate as an equivalence class object whose Equals method is Equivalence checking
        /// </summary>
        private class EquivClass
        {
            private PRED _set;
            private IBooleanAlgebra<PRED> _ba;

            internal EquivClass(IBooleanAlgebra<PRED> ba, PRED set)
            {
                _set = set;
                _ba = ba;
            }

            public override int GetHashCode()
            {
                return _set.GetHashCode();
            }

            public override bool Equals(object obj) =>
                obj is EquivClass ec && _ba.AreEquivalent(_set, ec._set);
        }
    }

    internal class PartitonTree<PRED>
    {
        private PartitonTree<PRED> parent;
        private int nr;
        internal PRED phi;
        private IBooleanAlgebra<PRED> solver;
        private PartitonTree<PRED> left;
        private PartitonTree<PRED> right;  //complement
        internal PartitonTree(IBooleanAlgebra<PRED> solver)
        {
            this.solver = solver;
            nr = -1;
            parent = null;
            this.phi = solver.True;
            this.left = null;
            this.right = null;
        }
        private PartitonTree(IBooleanAlgebra<PRED> solver, int depth, PartitonTree<PRED> parent, PRED phi, PartitonTree<PRED> left, PartitonTree<PRED> right)
        {
            this.solver = solver;
            this.parent = parent;
            this.nr = depth;
            this.phi = phi;
            this.left = left;
            this.right = right;
        }

        internal void Refine(PRED psi)
        {

            if (left == null && right == null)
            {
                #region leaf
                var phi_and_psi = solver.MkAnd(phi, psi);
                if (solver.IsSatisfiable(phi_and_psi))
                {
                    var phi_min_psi = solver.MkAnd(phi, solver.MkNot(psi));
                    if (solver.IsSatisfiable(phi_min_psi))
                    {
                        left = new PartitonTree<PRED>(solver, nr + 1, this, phi_and_psi, null, null);
                        right = new PartitonTree<PRED>(solver, nr + 1, this, phi_min_psi, null, null);
                    }
                    else // [[phi]] subset of [[psi]]
                        left = new PartitonTree<PRED>(solver, nr + 1, this, phi, null, null); //psi must true
                }
                else // [[phi]] subset of [[not(psi)]]
                    right = new PartitonTree<PRED>(solver, nr + 1, this, phi, null, null); //psi must be false
                #endregion
            }
            else if (left == null)
                right.Refine(psi);
            else if (right == null)
                left.Refine(psi);
            else
            {
                #region nonleaf
                var phi_and_psi = solver.MkAnd(phi, psi);
                if (solver.IsSatisfiable(phi_and_psi))
                {
                    var phi_min_psi = solver.MkAnd(phi, solver.MkNot(psi));
                    if (solver.IsSatisfiable(phi_min_psi))
                    {
                        left.Refine(psi);
                        right.Refine(psi);
                    }
                    else // [[phi]] subset of [[psi]]
                    {
                        left.ExtendLeft(); //psi is true
                        right.ExtendLeft();
                    }
                }
                else // [[phi]] subset of [[not(psi)]]
                {
                    left.ExtendRight();
                    right.ExtendRight(); //psi is false
                }
                #endregion
            }
        }

        private void ExtendRight()
        {
            if (left == null && right == null)
                right = new PartitonTree<PRED>(solver, nr + 1, this, phi, null, null);
            else if (left == null)
                right.ExtendRight();
            else if (right == null)
                left.ExtendRight();
            else
            {
                left.ExtendRight();
                right.ExtendRight();
            }
        }

        private void ExtendLeft()
        {
            if (left == null && right == null)
                left = new PartitonTree<PRED>(solver, nr + 1, this, phi, null, null);
            else if (left == null)
                right.ExtendLeft();
            else if (right == null)
                left.ExtendLeft();
            else
            {
                left.ExtendLeft();
                right.ExtendLeft();
            }
        }

        internal IEnumerable<int> GetPath()
        {
            for (var curr = this; curr.parent != null; curr = curr.parent)
                if (curr.parent.left == curr) //curr is the left child of its parent
                    yield return curr.nr;
        }

        internal IEnumerable<PartitonTree<PRED>> GetLeaves()
        {
            if (left == null && right == null)
                yield return this;
            else if (right == null)
                foreach (var leaf in left.GetLeaves())
                    yield return leaf;
            else if (left == null)
                foreach (var leaf in right.GetLeaves())
                    yield return leaf;
            else
            {
                foreach (var leaf in left.GetLeaves())
                    yield return leaf;
                foreach (var leaf in right.GetLeaves())
                    yield return leaf;
            }
        }
    }
}
