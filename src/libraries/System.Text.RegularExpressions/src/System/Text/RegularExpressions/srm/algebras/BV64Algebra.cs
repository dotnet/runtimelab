using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace Microsoft.SRM
{
    /// <summary>
    /// Bit vector algebra of up to 64 bits
    /// </summary>
    [Serializable]
    internal class BV64Algebra : BVAlgebraBase, ICharAlgebra<ulong>, ISerializable
    {
        [NonSerialized]
        private MintermGenerator<ulong> mtg;
        [NonSerialized]
        private ulong zero = 0;
        [NonSerialized]
        private ulong all;
        [NonSerialized]
        internal ulong[] atoms;

        public ulong ComputeDomainSize(ulong set)
        {
            int size = 0;
            for (int i = 0; i < atoms.Length; i++)
            {
                if (IsSatisfiable(set & atoms[i]))
                    size += partition[i].Count;
            }
            return (ulong)size;
        }

        public static BV64Algebra Create(CharSetSolver solver, BDD[] minterms)
        {
            if (minterms.Length > 64)
                throw new AutomataException(AutomataExceptionKind.NrOfMintermsCanBeAtMost64);
            var dtree = DecisionTree.Create(solver, minterms);
            var partitionBase = Array.ConvertAll(minterms, m => solver.ToRanges(m));
            var partition = Array.ConvertAll(partitionBase, p => new IntervalSet(p));
            return new BV64Algebra(dtree, partition);
        }

        private BV64Algebra(DecisionTree dtree, IntervalSet[] partition) : base(dtree, partition, partition.Length)
        {
            this.all = ulong.MaxValue >> (64 - this.nrOfBits);
            this.mtg = new MintermGenerator<ulong>(this);
            this.atoms = new ulong[this.nrOfBits];
            for (int i = 0; i < this.nrOfBits; i++)
            {
                atoms[i] = ((ulong)1) << i;
            }
        }

        /// <summary>
        /// Create a variant of the algebra where each minterms is replaced with a singleton set starting from '0'
        /// Used for testing purposes.
        /// </summary>
        internal BV64Algebra ReplaceMintermsWithVisibleCharacters()
        {
            Func<int, int> f = x =>
            {
                int k;
                if (x <= 26)
                    k = ('A' + (x - 1));
                else if (x <= 52)
                    k = ('a' + (x - 27));
                else if (x <= 62)
                    k = ('0' + (x - 53));
                else
                    k = '=';
                return k;
            };
            var simplified_partition = new IntervalSet[this.partition.Length];
            int[] precomp = new int[256];
            for (int i=1; i < simplified_partition.Length; i++)
            {
                int k = f(i);
                simplified_partition[i] = new IntervalSet(new Tuple<uint, uint>((uint)k, (uint)k));
                precomp[k] = i;
            }
            var zeroIntervals = new List<Tuple<uint, uint>>();
            int lower = 0;
            int upper = 0;
            for (int i = 1; i <= 'z' + 1; i++)
            {
                if (precomp[i] == 0)
                {
                    if (upper == i - 1)
                        upper += 1;
                    else
                    {
                        zeroIntervals.Add(new Tuple<uint, uint>((uint)lower, (uint)upper));
                        lower = i;
                        upper = i;
                    }
                }
            }
            zeroIntervals.Add(new Tuple<uint, uint>((uint)lower, 0xFFFF));
            simplified_partition[0] = new IntervalSet(zeroIntervals.ToArray());

            var simplified_dtree = new DecisionTree(precomp, new DecisionTree.BST(0, null, null));
            return new BV64Algebra(simplified_dtree, simplified_partition);
        }

        public ulong False
        {
            get
            {
                return zero;
            }
        }

        public bool IsExtensional
        {
            get
            {
                return true;
            }
        }

        public ulong True
        {
            get
            {
                return all;
            }
        }

        public BitWidth Encoding
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public CharSetSolver CharSetProvider
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public bool AreEquivalent(ulong predicate1, ulong predicate2)
        {
            return predicate1 == predicate2;
        }

        public IEnumerable<Tuple<bool[], ulong>> GenerateMinterms(params ulong[] constraints)
        {
            return this.mtg.GenerateMinterms(constraints);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSatisfiable(ulong predicate)
        {
            return predicate != zero;
        }

        public ulong MkAnd(params ulong[] predicates)
        {
            var and = all;
            for (int i = 0; i < predicates.Length; i++)
            {
                and = and & predicates[i];
                if (and == zero)
                    return zero;
            }
            return and;
        }

        public ulong MkAnd(IEnumerable<ulong> predicates)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong MkAnd(ulong predicate1, ulong predicate2)
        {
            return predicate1 & predicate2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong MkDiff(ulong predicate1, ulong predicate2)
        {
            return predicate1 & ~predicate2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong MkNot(ulong predicate)
        {
            return all & ~predicate;
        }

        public ulong MkOr(IEnumerable<ulong> predicates)
        {
            var res = zero;
            foreach (var p in predicates)
            {
                res = res | p;
                if (res == all)
                    return all;
            }
            return res;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong MkOr(ulong predicate1, ulong predicate2)
        {
            return predicate1 | predicate2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong MkSymmetricDifference(ulong p1, ulong p2)
        {
            return (p1 ^ p2);
        }

        public ulong MkRangeConstraint(char lower, char upper, bool caseInsensitive = false)
        {
            throw new NotSupportedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ulong MkCharConstraint(char c, bool caseInsensitive = false)
        {
            if (caseInsensitive == true)
                throw new AutomataException(AutomataExceptionKind.NotSupported);
            return this.atoms[this.dtree.GetId(c)];
        }

        /// <summary>
        /// Assumes that set is a union of some minterms (or empty).
        /// If null then 0 is returned.
        /// </summary>
        public ulong ConvertFromCharSet(BDD set)
        {
            if (set == null)
                return zero;
            var alg = set.algebra;
            ulong res = this.zero;
            for (int i = 0; i < partition.Length; i++)
            {
                BDD bdd_i = partition[i].AsBDD(alg);
                var conj = alg.MkAnd(bdd_i, set);
                if (alg.IsSatisfiable(conj))
                {
                    res = res | atoms[i];
                }
            }
            return res;
        }

        /// <summary>
        /// Pretty print the bitvector predicate as a character class.
        /// </summary>
        /// <param name="bv">given bitvector predicate</param>
        public string PrettyPrint(ulong bv)
        {
            var lab1 = PrettyPrintHelper(bv, false);
            var lab2 = PrettyPrintHelper(~bv, true);
            if (lab1.Length <= lab2.Length)
                return lab1;
            else
                return lab2;

        }

        private string PrettyPrintHelper(ulong bv, bool complement)
        {
            List<IntervalSet> sets = new List<IntervalSet>();
            for (int i = 0; i < atoms.Length; i++)
                if (IsSatisfiable(bv & atoms[i]))
                    sets.Add(partition[i]);
            var set = IntervalSet.Merge(sets);
            var res = set.ToCharacterClass(complement);
            return res;
        }

        public BDD ConvertToCharSet(BDDAlgebra solver, ulong pred)
        {
            BDD res = solver.False;
            if (!pred.Equals(this.zero))
            {
                for (int i = 0; i < atoms.Length; i++)
                {
                    //construct the union of the corresponding atoms
                    if (!(pred & atoms[i]).Equals(this.zero))
                    {
                        BDD bdd_i = partition[i].AsBDD(solver);
                        res = solver.MkOr(res, bdd_i);
                    }
                }
            }
            return res;
        }

        public ulong[] GetPartition()
        {
            return atoms;
        }

        public IEnumerable<char> GenerateAllCharacters(ulong set)
        {
            for (int i = 0; i < atoms.Length; i++)
            {
                if (IsSatisfiable(atoms[i] & set))
                    foreach (uint elem in partition[i].Enumerate())
                        yield return (char)elem;
            }
        }

        #region serialization
        /// <summary>
        /// Serialize
        /// </summary>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("d", dtree);
            info.AddValue("p", SerializePartition());
        }

        /// <summary>
        /// Deserialize
        /// </summary>
        public BV64Algebra(SerializationInfo info, StreamingContext context)
            : this((DecisionTree)info.GetValue("d", typeof(DecisionTree)),
                  DeserializePartition(info.GetString("p")))
        {
        }

        /// <summary>
        /// Serialize s as a hexadecimal numeral using lowercase letters
        /// </summary>
        /// <param name="s">given predicate</param>
        public string SerializePredicate(ulong s)
        {
            return s.ToString("x");
        }

        /// <summary>
        /// Deserialize s from a string created by SerializePredicate
        /// </summary>
        /// <param name="s">given hexadecimal numeral representation</param>
        public ulong DeserializePredicate(string s)
        {
            return ulong.Parse(s, System.Globalization.NumberStyles.HexNumber);
        }
        #endregion

        public ulong MkCharPredicate(string name, ulong pred)
        {
            throw new NotImplementedException();
        }

    }
}
