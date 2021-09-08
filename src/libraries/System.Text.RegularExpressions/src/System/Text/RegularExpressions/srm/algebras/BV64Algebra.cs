// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Bit vector algebra for up to 64 bits that uses an ulong directly as the term representation, unlike the more
    /// general BVAlgebra that uses an array of them. This simplifies the operations making the algebra more efficient.
    /// </summary>
    internal sealed class BV64Algebra : BVAlgebraBase, ICharAlgebra<ulong>
    {
        private readonly MintermGenerator<ulong> _mintermGenerator;
        private readonly ulong _false;
        private readonly ulong _true;

        /// <summary>
        /// Return the number of characters belonging to the minterms in the given set.
        /// </summary>
        public ulong ComputeDomainSize(ulong set)
        {
            ulong size = 0;
            for (int i = 0; i < _bits; i++)
            {
                // if the bit is set then include the corresponding minterm's cardinality
                if (IsSatisfiable(set & ((ulong)1 << i)))
                {
                    size += _cardinalities[i];
                }
            }

            return size;
        }

        public BV64Algebra(CharSetSolver solver, BDD[] minterms) :
            base(Classifier.Create(solver, minterms), Array.ConvertAll(minterms, solver.ComputeDomainSize), minterms)
        {
            Debug.Assert(minterms.Length <= 64);
            _mintermGenerator = new MintermGenerator<ulong>(this);
            _false = 0;
            _true = _bits == 64 ? ulong.MaxValue : ulong.MaxValue >> (64 - _bits);
        }

        /// <summary>
        /// Constructor used by BVAlgebraBase.Deserialize. Here the minters and the CharSetSolver are unknown and set to null.
        /// </summary>
        public BV64Algebra(Classifier classifier, ulong[] cardinalities) : base(classifier, cardinalities, null)
        {
            Debug.Assert(cardinalities.Length <= 64);
            _mintermGenerator = new MintermGenerator<ulong>(this);
            _false = 0;
            _true = _bits == 64 ? ulong.MaxValue : ulong.MaxValue >> (64 - _bits);
        }

        public bool IsExtensional => true;
        public bool HashCodesRespectEquivalence => true;

        public CharSetSolver CharSetProvider => throw new NotSupportedException();

        ulong IBooleanAlgebra<ulong>.False => _false;

        ulong IBooleanAlgebra<ulong>.True => _true;

        public bool AreEquivalent(ulong predicate1, ulong predicate2) => predicate1 == predicate2;

        public List<ulong> GenerateMinterms(params ulong[] constraints) => _mintermGenerator.GenerateMinterms(constraints);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSatisfiable(ulong predicate) => predicate != _false;

        public ulong And(params ulong[] predicates)
        {
            ulong and = _true;
            for (int i = 0; i < predicates.Length; i++)
            {
                and &= predicates[i];

                // short circuit the evaluation on false, since 0&x=0
                if (and == _false)
                {
                    return _false;
                }
            }

            return and;
        }

        public ulong And(IEnumerable<ulong> predicates) => throw new NotSupportedException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong And(ulong predicate1, ulong predicate2) => predicate1 & predicate2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong Not(ulong predicate) => _true & ~predicate; //NOTE: must filter off unused bits

        public ulong Or(IEnumerable<ulong> predicates)
        {
            ulong res = _false;
            foreach (ulong p in predicates)
            {
                res |= p;

                // short circuit the evaluation on true, since 1|x=1
                if (res == _true)
                {
                    return _true;
                }
            }

            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong Or(ulong predicate1, ulong predicate2) => predicate1 | predicate2;

        public ulong RangeConstraint(char lower, char upper, bool caseInsensitive = false, string? culture = null) => throw new NotSupportedException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong CharConstraint(char c, bool caseInsensitive = false, string? culture = null)
        {
            Debug.Assert(!caseInsensitive);
            return ((ulong)1) << _classifier.Find(c);
        }

        /// <summary>
        /// Assumes that set is a union of some minterms (or empty).
        /// If null then 0 is returned.
        /// </summary>
        public ulong ConvertFromCharSet(BDDAlgebra alg, BDD? set)
        {
            ulong res = _false;

            if (set is not null)
            {
                for (int i = 0; i < _bits; i++)
                {
                    Debug.Assert(_partition is not null);

                    // set the i'th bit if the i'th minterm is in the set
                    if (alg.IsSatisfiable(alg.And(_partition[i], set)))
                    {
                        res |= (ulong)1 << i;
                    }
                }
            }

            return res;
        }

        public BDD ConvertToCharSet(ICharAlgebra<BDD> solver, ulong pred)
        {
            Debug.Assert(_partition is not null);

            // the result will be the union of all minterms in the set
            BDD res = BDD.False;
            if (pred != _false)
            {
                for (int i = 0; i < _bits; i++)
                {
                    // include the i'th minterm in the union if the i'th bit is set
                    if ((pred & ((ulong)1 << i)) != _false)
                    {
                        res = solver.Or(res, _partition[i]);
                    }
                }
            }

            return res;
        }

        /// <summary>
        /// Return an array of bitvectors representing each of the minterms.
        /// </summary>
        public ulong[] GetPartition()
        {
            ulong[] atoms = new ulong[_bits];
            for (int i = 0; i < _bits; i++)
            {
                atoms[i] = (ulong)1 << i;
            }

            return atoms;
        }

        public IEnumerable<char> GenerateAllCharacters(ulong set) => throw new NotSupportedException();

        #region serialization
        /// <summary>
        /// Serialize pred using Base64.Encode
        /// </summary>
        public void SerializePredicate(ulong pred, StringBuilder sb) => Base64.Encode(pred, sb);

        /// <summary>
        /// Deserialize s from a string created by SerializePredicate
        /// </summary>
        public ulong DeserializePredicate(string s) => Base64.DecodeUInt64(s);
        #endregion

        /// <summary>
        /// Pretty print the bitvector bv as the character set it represents.
        /// </summary>
        public string PrettyPrint(ulong bv)
        {
            //accesses the shared BDD solver
            ICharAlgebra<BDD> bddalgebra = SymbolicRegexRunner.s_unicode._solver;

            if (_partition == null || bddalgebra == null)
            {
                var sb = new StringBuilder();
                sb.Append('[');
                SerializePredicate(bv, sb);
                sb.Append(']');
                return sb.ToString();
            }

            BDD bdd = ConvertToCharSet(bddalgebra, bv);
            string str = bddalgebra.PrettyPrint(bdd);
            return str;
        }
    }
}
