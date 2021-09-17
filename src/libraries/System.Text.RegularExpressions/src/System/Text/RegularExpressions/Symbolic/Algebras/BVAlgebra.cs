﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.Symbolic
{
    /// <summary>
    /// Base class for bitvector algebras, which represent sets as bitvectors indexed by the elements. An element is in
    /// the set if the corresponding bit is set.
    ///
    /// These bitvector algebras are used to represent sets of minterms, and thus represent sets of characters
    /// indirectly. However, the bitvector algebras are aware of this indirection in that the cardinalities of sets
    /// count the characters rather than the minterms. For example, the cardinality of a bitvector "110" where the bits
    /// correspond to minterms [a-c], [0-9] and [^a-c0-9] is 13 rather than 2.
    /// </summary>
    internal abstract class BVAlgebraBase
    {
        internal readonly PartitionClassifier _classifier;
        protected readonly ulong[] _cardinalities;
        protected readonly int _bits;
        protected readonly BDD[]? _partition;

        internal BVAlgebraBase(PartitionClassifier classifier, ulong[] cardinalities, BDD[]? partition)
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
    internal sealed class BVAlgebra : BVAlgebraBase, ICharAlgebra<BV>
    {
        private readonly MintermGenerator<BV> _mintermGenerator;
        internal BV[] _atoms;

        public ulong ComputeDomainSize(BV set)
        {
            ulong size = 0;
            for (int i = 0; i < _bits; i++)
            {
                // if the bit is set then add the minterm's size
                if (set[i])
                {
                    size += _cardinalities[i];
                }
            }

            return size;
        }

        public BVAlgebra(CharSetSolver solver, BDD[] minterms) :
            base(PartitionClassifier.Create(solver, minterms), Array.ConvertAll(minterms, solver.ComputeDomainSize), minterms)
        {
            _mintermGenerator = new MintermGenerator<BV>(this);
            False = BV.CreateFalse(_bits);
            True = BV.CreateTrue(_bits);

            var atoms = new BV[_bits];
            for (int i = 0; i < atoms.Length; i++)
            {
                atoms[i] = BV.CreateSingleBit(_bits, i);
            }
            _atoms = atoms;
        }

        public BV False { get; }
        public BV True { get; }

        public bool IsExtensional => true;
        public bool HashCodesRespectEquivalence => true;
        public CharSetSolver CharSetProvider => throw new NotSupportedException();
        public bool AreEquivalent(BV predicate1, BV predicate2) => predicate1.Equals(predicate2);
        public List<BV> GenerateMinterms(params BV[] constraints) => _mintermGenerator.GenerateMinterms(constraints);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSatisfiable(BV predicate) => !predicate.Equals(False);

        public BV And(params BV[] predicates)
        {
            BV and = True;
            for (int i = 0; i < predicates.Length; i++)
            {
                and &= predicates[i];
            }

            return and;
        }

        public BV And(IEnumerable<BV> predicates) => throw new NotSupportedException();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BV And(BV predicate1, BV predicate2) => predicate1 & predicate2;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BV Not(BV predicate) => ~predicate;

        public BV Or(IEnumerable<BV> predicates)
        {
            BV res = False;
            foreach (BV p in predicates)
            {
                res |= p;
            }

            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BV Or(BV predicate1, BV predicate2) => predicate1 | predicate2;

        public BV RangeConstraint(char lower, char upper, bool caseInsensitive = false, string? culture = null) => throw new NotSupportedException();

        public BV CharConstraint(char c, bool caseInsensitive = false, string? culture = null)
        {
            Debug.Assert(!caseInsensitive);
            int i = _classifier.Find(c);
            return _atoms[i];
        }

        /// <summary>
        /// Assumes that set is a union of some minterms (or empty).
        /// If null then null is returned.
        /// </summary>
        [return: NotNullIfNotNull("set")]
        public BV? ConvertFromCharSet(BDDAlgebra alg, BDD set)
        {
            if (set == null)
                return null;

            Debug.Assert(_partition is not null);

            BV res = False;
            for (int i = 0; i < _bits; i++)
            {
                BDD bdd_i = _partition[i];
                BDD conj = alg.And(bdd_i, set);
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

            // the result will be the union of all minterms in the set
            BDD res = solver.False;
            if (!pred.Equals(False))
            {
                for (int i = 0; i < _bits; i++)
                {
                    // include the i'th minterm in the union if the i'th bit is set
                    if (pred[i])
                    {
                        res = solver.Or(res, _partition[i]);
                    }
                }
            }

            return res;
        }

        public BV[] GetPartition() => _atoms;
        public IEnumerable<char> GenerateAllCharacters(BV set) => throw new NotSupportedException();

        /// <summary>
        /// Pretty print the bitvector bv as the character set it represents.
        /// </summary>
        public string PrettyPrint(BV bv)
        {
            //accesses the shared BDD solver
            ICharAlgebra<BDD> bddalgebra = SymbolicRegexRunner.s_unicode._solver;

            if (_partition == null || bddalgebra == null)
            {
                var sb = new StringBuilder();
                sb.Append('[');
                for (int i = 0; i < bv.Length; i++)
                {
                    if (sb.Length != 0)
                    {
                        sb.Append(',');
                    }
                    if (bv[i])
                    {
                        sb.Append(i);
                    }
                }
                sb.Append(']');
                return sb.ToString();
            }

            BDD bdd = ConvertToCharSet(bddalgebra, bv);
            string str = bddalgebra.PrettyPrint(bdd);
            return str;
        }
    }
}
