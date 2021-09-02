// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Provides a generic implementation for minterm generation over a given Boolean Algebra.
    /// </summary>
    /// <typeparam name="TPredicate">type of predicates</typeparam>
    internal sealed class MintermGenerator<TPredicate>
    {
        private readonly IBooleanAlgebra<TPredicate> _ba;

        /// <summary>
        /// Constructs a minterm generator for a given Boolean Algebra.
        /// </summary>
        /// <param name="ba">given Boolean Algebra</param>
        public MintermGenerator(IBooleanAlgebra<TPredicate> ba)
        {
            // cannot rely on equivalent predicates having the same hashcode
            // so all predicates would end up in the same bucket that causes a linear search
            // with Equals to check equivalence --- this case must never arise here
            Debug.Assert(ba.HashCodesRespectEquivalence);

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
        public IEnumerable<Tuple<bool[], TPredicate>> GenerateMinterms(params TPredicate[] preds)
        {
            if (preds.Length == 0)
            {
                yield return new Tuple<bool[], TPredicate>(Array.Empty<bool>(), _ba.True);
            }
            else
            {
                int count = preds.Length;

                List<TPredicate> nonequivalentSets = new List<TPredicate>();

                //work only with nonequivalent sets as distinct elements
                var indexLookup = new Dictionary<int, int>();
                var newIndexMap = new Dictionary<EquivClass, int>();
                var equivs = new List<List<int>>();

                for (int i = 0; i < count; i++)
                {
                    EquivClass equiv = CreateEquivalenceClass(preds[i]);
                    if (!newIndexMap.TryGetValue(equiv, out int newIndex))
                    {
                        newIndex = newIndexMap.Count;
                        newIndexMap[equiv] = newIndex;
                        nonequivalentSets.Add(preds[i]);
                        equivs.Add(new List<int>());
                    }
                    indexLookup[i] = newIndex;
                    equivs[newIndex].Add(i);
                }

                var tree = new PartitonTree<TPredicate>(_ba);
                foreach (TPredicate psi in nonequivalentSets)
                {
                    tree.Refine(psi);
                }

                foreach (PartitonTree<TPredicate> leaf in tree.GetLeaves())
                {
                    bool[] characteristic = new bool[preds.Length];
                    foreach (int k in leaf.GetPath())
                    {
                        foreach (int n in equivs[k])
                        {
                            characteristic[n] = true;
                        }
                    }

                    yield return new Tuple<bool[], TPredicate>(characteristic, leaf._phi);
                }
            }
        }

        private EquivClass CreateEquivalenceClass(TPredicate set) => new EquivClass(_ba, set);

        /// <summary>
        /// Wraps a predicate as an equivalence class object whose Equals method is Equivalence checking
        /// </summary>
        private sealed class EquivClass
        {
            private readonly TPredicate _set;
            private readonly IBooleanAlgebra<TPredicate> _ba;

            internal EquivClass(IBooleanAlgebra<TPredicate> ba, TPredicate set)
            {
                _set = set;
                _ba = ba;
            }

            public override int GetHashCode() => _set.GetHashCode();

            public override bool Equals(object obj) => obj is EquivClass ec && _ba.AreEquivalent(_set, ec._set);
        }
    }

    internal sealed class PartitonTree<TPredicate>
    {
        private readonly PartitonTree<TPredicate> _parent;
        private readonly int _nr;
        internal readonly TPredicate _phi;
        private readonly IBooleanAlgebra<TPredicate> _solver;
        private PartitonTree<TPredicate> _left;
        private PartitonTree<TPredicate> _right;  // complement

        internal PartitonTree(IBooleanAlgebra<TPredicate> solver)
        {
            _solver = solver;
            _nr = -1;
            _parent = null;
            _phi = solver.True;
            _left = null;
            _right = null;
        }

        private PartitonTree(IBooleanAlgebra<TPredicate> solver, int depth, PartitonTree<TPredicate> parent, TPredicate phi, PartitonTree<TPredicate> left, PartitonTree<TPredicate> right)
        {
            _solver = solver;
            _parent = parent;
            _nr = depth;
            _phi = phi;
            _left = left;
            _right = right;
        }

        internal void Refine(TPredicate psi)
        {
            if (_left == null && _right == null)
            {
                #region leaf
                TPredicate phi_and_psi = _solver.MkAnd(_phi, psi);
                if (_solver.IsSatisfiable(phi_and_psi))
                {
                    TPredicate phi_min_psi = _solver.MkAnd(_phi, _solver.MkNot(psi));
                    if (_solver.IsSatisfiable(phi_min_psi))
                    {
                        _left = new PartitonTree<TPredicate>(_solver, _nr + 1, this, phi_and_psi, null, null);
                        _right = new PartitonTree<TPredicate>(_solver, _nr + 1, this, phi_min_psi, null, null);
                    }
                    else // [[phi]] subset of [[psi]]
                    {
                        _left = new PartitonTree<TPredicate>(_solver, _nr + 1, this, _phi, null, null); //psi must true
                    }
                }
                else // [[phi]] subset of [[not(psi)]]
                {
                    _right = new PartitonTree<TPredicate>(_solver, _nr + 1, this, _phi, null, null); //psi must be false
                }
                #endregion
            }
            else if (_left == null)
            {
                _right.Refine(psi);
            }
            else if (_right == null)
            {
                _left.Refine(psi);
            }
            else
            {
                #region nonleaf
                TPredicate phi_and_psi = _solver.MkAnd(_phi, psi);
                if (_solver.IsSatisfiable(phi_and_psi))
                {
                    TPredicate phi_min_psi = _solver.MkAnd(_phi, _solver.MkNot(psi));
                    if (_solver.IsSatisfiable(phi_min_psi))
                    {
                        _left.Refine(psi);
                        _right.Refine(psi);
                    }
                    else // [[phi]] subset of [[psi]]
                    {
                        _left.ExtendLeft(); //psi is true
                        _right.ExtendLeft();
                    }
                }
                else // [[phi]] subset of [[not(psi)]]
                {
                    _left.ExtendRight();
                    _right.ExtendRight(); //psi is false
                }
                #endregion
            }
        }

        private void ExtendRight()
        {
            if (_left == null && _right == null)
                _right = new PartitonTree<TPredicate>(_solver, _nr + 1, this, _phi, null, null);
            else if (_left == null)
                _right.ExtendRight();
            else if (_right == null)
                _left.ExtendRight();
            else
            {
                _left.ExtendRight();
                _right.ExtendRight();
            }
        }

        private void ExtendLeft()
        {
            if (_left == null && _right == null)
            {
                _left = new PartitonTree<TPredicate>(_solver, _nr + 1, this, _phi, null, null);
            }
            else if (_left == null)
            {
                _right.ExtendLeft();
            }
            else if (_right == null)
            {
                _left.ExtendLeft();
            }
            else
            {
                _left.ExtendLeft();
                _right.ExtendLeft();
            }
        }

        internal IEnumerable<int> GetPath()
        {
            for (PartitonTree<TPredicate> curr = this; curr._parent != null; curr = curr._parent)
            {
                if (curr._parent._left == curr) //curr is the left child of its parent
                {
                    yield return curr._nr;
                }
            }
        }

        internal IEnumerable<PartitonTree<TPredicate>> GetLeaves()
        {
            if (_left == null && _right == null)
            {
                yield return this;
            }
            else if (_right == null)
            {
                foreach (PartitonTree<TPredicate> leaf in _left.GetLeaves())
                    yield return leaf;
            }
            else if (_left == null)
            {
                foreach (PartitonTree<TPredicate> leaf in _right.GetLeaves())
                    yield return leaf;
            }
            else
            {
                foreach (PartitonTree<TPredicate> leaf in _left.GetLeaves())
                    yield return leaf;

                foreach (PartitonTree<TPredicate> leaf in _right.GetLeaves())
                    yield return leaf;
            }
        }
    }
}
