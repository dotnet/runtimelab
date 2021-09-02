// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Bit vector algebra of up to 64 bits
    /// </summary>
    internal sealed class BV64Algebra : BVAlgebraBase, ICharAlgebra<ulong>
    {
        private readonly MintermGenerator<ulong> _mtg;
        private readonly ulong _False;
        private readonly ulong _True;

        public ulong ComputeDomainSize(ulong set)
        {
            ulong size = 0;
            for (int i = 0; i < _bits; i++)
                if (IsSatisfiable(set & ((ulong)1 << i)))
                    size += _cardinalities[i];
            return size;
        }

        public BV64Algebra(CharSetSolver solver, BDD[] minterms) :
            base(Classifier.Create(solver, minterms), Array.ConvertAll(minterms, solver.ComputeDomainSize), minterms)
        {
            Debug.Assert(minterms.Length <= 64);
            _mtg = new MintermGenerator<ulong>(this);
            _False = 0;
            _True = _bits == 64 ? ulong.MaxValue : ulong.MaxValue >> (64 - _bits);
        }

        /// <summary>
        /// Constructor used by BVAlgebraBase.Deserialize. Here the minters and the CharSetSolver are unknown and set to null.
        /// </summary>
        public BV64Algebra(Classifier classifier, ulong[] cardinalities) : base(classifier, cardinalities, null)
        {
            Debug.Assert(cardinalities.Length <= 64);
            _mtg = new MintermGenerator<ulong>(this);
            _False = 0;
            _True = _bits == 64 ? ulong.MaxValue : ulong.MaxValue >> (64 - _bits);
        }

        public bool IsExtensional => true;
        public bool HashCodesRespectEquivalence => true;

        public CharSetSolver CharSetProvider => throw new NotSupportedException();

        ulong IBooleanAlgebra<ulong>.False => _False;

        ulong IBooleanAlgebra<ulong>.True => _True;

        public bool AreEquivalent(ulong predicate1, ulong predicate2) => predicate1 == predicate2;

        public IEnumerable<Tuple<bool[], ulong>> GenerateMinterms(params ulong[] constraints) => _mtg.GenerateMinterms(constraints);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSatisfiable(ulong predicate) => predicate != _False;

        public ulong MkAnd(params ulong[] predicates)
        {
            ulong and = _True;
            for (int i = 0; i < predicates.Length; i++)
            {
                and &= predicates[i];
                if (and == _False)
                    return _False;
            }
            return and;
        }

        public ulong MkAnd(IEnumerable<ulong> predicates) => throw new NotImplementedException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong MkAnd(ulong predicate1, ulong predicate2) => predicate1 & predicate2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong MkNot(ulong predicate) => _True & ~predicate; //NOTE: must filter off unused bits

        public ulong MkOr(IEnumerable<ulong> predicates)
        {
            ulong res = _False;
            foreach (ulong p in predicates)
            {
                res |= p;
                if (res == _True)
                    return _True;
            }
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong MkOr(ulong predicate1, ulong predicate2) => predicate1 | predicate2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong MkSymmetricDifference(ulong p1, ulong p2) => p1 ^ p2;

        public ulong MkRangeConstraint(char lower, char upper, bool caseInsensitive = false, string culture = null) => throw new NotSupportedException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong MkCharConstraint(char c, bool caseInsensitive = false, string culture = null)
        {
            if (caseInsensitive)
                throw new NotImplementedException(nameof(MkCharConstraint));

            return ((ulong)1) << _classifier.Find(c);
        }

        /// <summary>
        /// Assumes that set is a union of some minterms (or empty).
        /// If null then 0 is returned.
        /// </summary>
        public ulong ConvertFromCharSet(BDDAlgebra alg, BDD set)
        {
            if (set == null)
                return _False;

            ulong res = _False;
            for (int i = 0; i < _bits; i++)
                if (alg.IsSatisfiable(alg.MkAnd(_partition[i], set)))
                    res |= (ulong)1 << i;
            return res;
        }

        public BDD ConvertToCharSet(ICharAlgebra<BDD> solver, ulong pred)
        {
            if (_partition == null)
                throw new NotImplementedException(nameof(ConvertToCharSet));

            BDD res = BDD.False;
            if (pred != _False)
                for (int i = 0; i < _bits; i++)
                    //construct the union of the corresponding minterms in the partition
                    if ((pred & ((ulong)1 << i)) != _False)
                        res = solver.MkOr(res, _partition[i]);
            return res;
        }

        public ulong[] GetPartition()
        {
            ulong[] atoms = new ulong[_bits];
            for (int i = 0; i < _bits; i++)
                atoms[i] = (ulong)1 << i;
            return atoms;
        }

        public IEnumerable<char> GenerateAllCharacters(ulong set) => throw new NotImplementedException(nameof(GenerateAllCharacters));

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

        public ulong MkCharPredicate(string name, ulong pred) => throw new NotImplementedException(nameof(MkCharPredicate));

        /// <summary>
        /// Pretty print the bitvector bv as the character set it represents.
        /// </summary>
        public string PrettyPrint(ulong bv)
        {
            //accesses the shared BDD solver
            ICharAlgebra<BDD> bddalgebra = Regex.s_unicode._solver;

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
