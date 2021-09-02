﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.SRM
{
    internal abstract class BVAlgebraBase
    {
        internal readonly Classifier _classifier;
        protected readonly ulong[] _cardinalities;
        protected readonly int _bits;
        protected readonly BDD[]? _partition;

        internal BVAlgebraBase(Classifier classifier, ulong[] cardinalities, BDD[]? partition)
        {
            _classifier = classifier;
            _cardinalities = cardinalities;
            _bits = cardinalities.Length;
            _partition = partition;
        }

        #region serialization
        /// <summary>
        /// Appends a string in [0-9A-Za-z/+.-,;]* to sb
        /// </summary>
        public void Serialize(StringBuilder sb)
        {
            //does not use ';'
            Base64.Encode(_cardinalities, sb);
            sb.Append(';'); //separator
            //does not use ';'
            _classifier.Serialize(sb);
        }

        /// <summary>
        /// Reconstructs either a BV64Algebra or a BVAlgebra from the input.
        /// </summary>
        public static BVAlgebraBase Deserialize(string input)
        {
            int firstEnd = input.IndexOf(';');
            if (firstEnd == -1 || input.IndexOf(';', firstEnd + 1) != -1)
            {
                throw new ArgumentException($"{nameof(BVAlgebraBase.Deserialize)} input error");
            }

            ulong[] cardinalities = Base64.DecodeUInt64Array(input.AsSpan(0, firstEnd));

            //here one could potentially pass in the global CharSetSolver as the second parameter.
            //but it is not needed, practically speaking, because the functionality
            //needed during matching will not use operations that need the BDD algebra.
            Classifier cl = Classifier.Deserialize(input.AsSpan(firstEnd + 1));
            return cardinalities.Length <= 64 ?
                new BV64Algebra(cl, cardinalities) :
                new BVAlgebra(cl, cardinalities);
        }
        #endregion
    }

    /// <summary>
    /// Bit vector algebra
    /// </summary>
    internal sealed class BVAlgebra : BVAlgebraBase, ICharAlgebra<BV>
    {
        private readonly MintermGenerator<BV> _mtg;
        private readonly BV _zero;
        private readonly BV _ones;
        internal BV[] _atoms;

        public ulong ComputeDomainSize(BV set)
        {
            ulong size = 0;
            for (int i = 0; i < _bits; i++)
            {
                if (set[i])
                {
                    size += _cardinalities[i];
                }
            }

            return size;
        }

        public BVAlgebra(CharSetSolver solver, BDD[] minterms) :
            base(Classifier.Create(solver, minterms), Array.ConvertAll(minterms, solver.ComputeDomainSize), minterms)
        {
            _mtg = new MintermGenerator<BV>(this);
            _zero = BV.MkFalse(_bits);
            _ones = BV.MkTrue(_bits);

            var atoms = new BV[_bits];
            for (int i = 0; i < atoms.Length; i++)
            {
                atoms[i] = BV.MkBit1(_bits, i);
            }
            _atoms = atoms;
        }

        /// <summary>
        /// Constructor used by BVAlgebraBase.Deserialize. Here the minters and the CharSetSolver are unknown and set to null.
        /// </summary>
        public BVAlgebra(Classifier classifier, ulong[] cardinalities) : base(classifier, cardinalities, null)
        {
            _mtg = new MintermGenerator<BV>(this);
            _zero = BV.MkFalse(_bits);
            _ones = BV.MkTrue(_bits);

            var atoms = new BV[_bits];
            for (int i = 0; i < atoms.Length; i++)
            {
                atoms[i] = BV.MkBit1(_bits, i);
            }
            _atoms = atoms;
        }

        public BV False => _zero;
        public bool IsExtensional => true;
        public bool HashCodesRespectEquivalence => true;
        public BV True => _ones;
        public CharSetSolver CharSetProvider => throw new NotSupportedException();
        public bool AreEquivalent(BV predicate1, BV predicate2) => predicate1.Equals(predicate2);
        public IEnumerable<Tuple<bool[], BV>> GenerateMinterms(params BV[] constraints) => _mtg.GenerateMinterms(constraints);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSatisfiable(BV predicate) => !predicate.Equals(_zero);

        public BV MkAnd(params BV[] predicates)
        {
            BV and = _ones;
            for (int i = 0; i < predicates.Length; i++)
            {
                and &= predicates[i];
            }

            return and;
        }

        public BV MkAnd(IEnumerable<BV> predicates) => throw new NotImplementedException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BV MkAnd(BV predicate1, BV predicate2) => predicate1 & predicate2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BV MkNot(BV predicate) => ~predicate;

        public BV MkOr(IEnumerable<BV> predicates)
        {
            BV res = _zero;
            foreach (BV p in predicates)
            {
                res |= p;
            }

            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BV MkOr(BV predicate1, BV predicate2) => predicate1 | predicate2;

        public BV MkRangeConstraint(char lower, char upper, bool caseInsensitive = false, string culture = null) => throw new NotSupportedException(nameof(MkRangeConstraint));

        public BV MkCharConstraint(char c, bool caseInsensitive = false, string culture = null)
        {
            Debug.Assert(!caseInsensitive);
            int i = _classifier.Find(c);
            return _atoms[i];
        }

        /// <summary>
        /// Assumes that set is a union of some minterms (or empty).
        /// If null then null is returned.
        /// </summary>
        public BV ConvertFromCharSet(BDDAlgebra alg, BDD set)
        {
            if (set == null)
                return null;

            Debug.Assert(_partition is not null);

            BV res = _zero;
            for (int i = 0; i < _bits; i++)
            {
                BDD bdd_i = _partition[i];
                BDD conj = alg.MkAnd(bdd_i, set);
                if (alg.IsSatisfiable(conj))
                {
                    res |= _atoms[i];
                }
            }

            return res;
        }

        public BDD ConvertToCharSet(ICharAlgebra<BDD> solver, BV pred)
        {
            Debug.Assert(_partition is not null);

            BDD res = solver.False;
            if (!pred.Equals(_zero))
            {
                for (int i = 0; i < _bits; i++)
                {
                    // construct the union of the corresponding atoms
                    if (pred[i])
                    {
                        res = solver.MkOr(res, _partition[i]);
                    }
                }
            }

            return res;
        }

        public BV[] GetPartition() => _atoms;
        public IEnumerable<char> GenerateAllCharacters(BV set) => throw new NotImplementedException(nameof(GenerateAllCharacters));
        public BV MkCharPredicate(string name, BV pred) => throw new NotImplementedException(nameof(GenerateAllCharacters));

        /// <summary>
        /// calls bv.Serialize()
        /// </summary>
        public void SerializePredicate(BV bv, StringBuilder builder) => bv.Serialize(builder);

        /// <summary>
        /// calls BV.Deserialize(s)
        /// </summary>
        public BV DeserializePredicate(string s) => BV.Deserialize(s);

        /// <summary>
        /// Pretty print the bitvector bv as the character set it represents.
        /// </summary>
        public string PrettyPrint(BV bv)
        {
            //accesses the shared BDD solver
            ICharAlgebra<BDD> bddalgebra = Regex.s_unicode._solver;

            if (_partition == null || bddalgebra == null)
                return $"[{bv.SerializeToString()}]";

            BDD bdd = ConvertToCharSet(bddalgebra, bv);
            string str = bddalgebra.PrettyPrint(bdd);
            return str;
        }
    }
}
