﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace System.Text.RegularExpressions.SRM
{
    internal abstract class BVAlgebraBase
    {
        internal Classifier _classifier;
        protected ulong[] _cardinalities;
        protected int _bits;
        protected BDD[]? _partition;

        internal BVAlgebraBase(Classifier classifier, ulong[] cardinalities, BDD[]? partition)
        {
            _classifier = classifier;
            _cardinalities = cardinalities;
            _bits = cardinalities.Length;
            _partition = partition;
        }
    }

    /// <summary>
    /// Bit vector algebra
    /// </summary>
    internal class BVAlgebra : BVAlgebraBase, ICharAlgebra<BV>
    {
        private MintermGenerator<BV> mtg;
        private BV zero;
        private BV ones;
        internal BV[] atoms;

        public ulong ComputeDomainSize(BV set)
        {
            ulong size = 0;
            for (int i = 0; i < _bits; i++)
                if (set[i])
                    size += _cardinalities[i];
            return size;
        }

        public BVAlgebra(CharSetSolver solver, BDD[] minterms) :
            base(Classifier.Create(solver, minterms), Array.ConvertAll(minterms, solver.ComputeDomainSize), minterms)
        {
            mtg = new MintermGenerator<BV>(this);
            zero = BV.MkFalse(_bits);
            ones = BV.MkTrue(_bits);
            atoms = new BV[_bits];
            for (int i = 0; i < _bits; i++)
               atoms[i] = BV.MkBit1(_bits, i);
        }

        public BV False => zero;
        public bool IsExtensional => true;
        public bool HashCodesRespectEquivalence => true;
        public BV True => ones;
        public CharSetSolver CharSetProvider => throw new NotSupportedException();
        public bool AreEquivalent(BV predicate1, BV predicate2) => predicate1.Equals(predicate2);
        public IEnumerable<Tuple<bool[], BV>> GenerateMinterms(params BV[] constraints) => mtg.GenerateMinterms(constraints);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSatisfiable(BV predicate) => !predicate.Equals(zero);

        public BV MkAnd(params BV[] predicates)
        {
            var and = ones;
            for (int i = 0; i < predicates.Length; i++)
                and = and & predicates[i];
            return and;
        }

        public BV MkAnd(IEnumerable<BV> predicates) => throw new NotImplementedException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BV MkAnd(BV predicate1, BV predicate2) => predicate1 & predicate2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BV MkNot(BV predicate) => ~predicate;

        public BV MkOr(IEnumerable<BV> predicates)
        {
            var res = zero;
            foreach (var p in predicates)
                res = res | p;
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BV MkOr(BV predicate1, BV predicate2) => predicate1 | predicate2;

        public BV MkRangeConstraint(char lower, char upper, bool caseInsensitive = false) => throw new NotSupportedException(nameof(MkRangeConstraint));

        public BV MkCharConstraint(char c, bool caseInsensitive = false)
        {
#if DEBUG
            if (caseInsensitive == true)
                throw new AutomataException(AutomataExceptionKind.NotSupported);
#endif
            int i = _classifier.Find(c);
            return this.atoms[i];
        }

        /// <summary>
        /// Assumes that set is a union of some minterms (or empty).
        /// If null then null is returned.
        /// </summary>
        public BV ConvertFromCharSet(BDDAlgebra alg, BDD set)
        {
            if (set == null)
                return null;
#if DEBUG
            if (_partition == null)
                throw new NotImplementedException(nameof(ConvertFromCharSet));
#endif
            BV res = this.zero;
            for (int i = 0; i < _bits; i++)
            {
                BDD bdd_i = _partition[i];
                var conj = alg.MkAnd(bdd_i, set);
                if (alg.IsSatisfiable(conj))
                    res = res | atoms[i];
            }
            return res;
        }

        public BDD ConvertToCharSet(BDDAlgebra solver, BV pred)
        {
#if DEBUG
            if (_partition == null)
                throw new NotImplementedException(nameof(ConvertToCharSet));
#endif
            BDD res = solver.False;
            if (!pred.Equals(this.zero))
            {
                for (int i = 0; i < _bits; i++)
                    //construct the union of the corresponding atoms
                    if (pred[i])
                        res = solver.MkOr(res, _partition[i]);
            }
            return res;
        }

        public BV[] GetPartition() => atoms;
        public IEnumerable<char> GenerateAllCharacters(BV set) => throw new NotImplementedException(nameof(GenerateAllCharacters));
        public BV MkCharPredicate(string name, BV pred) => throw new NotImplementedException(nameof(GenerateAllCharacters));


        #region serialization
        /// <summary>
        /// calls bv.Serialize()
        /// </summary>
        public string SerializePredicate(BV bv) => bv.SerializeToString();

        /// <summary>
        /// calls BV.Deserialize(s)
        /// </summary>
        public BV DeserializePredicate(string s) => BV.Deserialize(s);

        public void Serialize(StringBuilder sb)
        {


        }
        #endregion

    }
}
