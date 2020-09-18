using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace Microsoft.SRM
{
    internal abstract class BVAlgebraBase
    {
        internal DecisionTree dtree;
        internal IntervalSet[] partition;
        internal int nrOfBits;

        internal BVAlgebraBase(DecisionTree dtree, IntervalSet[] partition, int nrOfBits)
        {
            this.dtree = dtree;
            this.partition = partition;
            this.nrOfBits = nrOfBits;
        }

        protected string SerializePartition()
        {
            string s = "";
            for (int i = 0; i < partition.Length; i++)
            {
                if (i > 0)
                    s += ";";
                s += partition[i].Serialize();
            }
            return s;
        }

        protected static IntervalSet[] DeserializePartition(string s)
        {
            var blocks = s.Split(';');
            var intervalSets = Array.ConvertAll(blocks, IntervalSet.Parse);
            return intervalSets;
        }
    }
    /// <summary>
    /// Bit vector algebra
    /// </summary>
    [Serializable]
    public internal BVAlgebra : BVAlgebraBase, ICharAlgebra<BV>, ISerializable
    {
        [NonSerialized]
        MintermGenerator<BV> mtg;
        [NonSerialized]
        BV zero;
        [NonSerialized]
        BV ones;
        [NonSerialized]
        ulong[] all0;
        [NonSerialized]
        ulong[] all1;
        [NonSerialized]
        internal BV[] atoms;

        public ulong ComputeDomainSize(BV set)
        {
            int size = 0;
            for (int i = 0; i < atoms.Length; i++)
            {
                if (IsSatisfiable(set & atoms[i]))
                    size += partition[i].Count;
            }
            return (ulong)size;
        }

        public static BVAlgebra Create(CharSetSolver solver, BDD[] minterms)
        {
            var dtree = DecisionTree.Create(solver, minterms);
            var partitionBase = Array.ConvertAll(minterms, m => solver.ToRanges(m));
            var partition = Array.ConvertAll(partitionBase, p => new IntervalSet(p));
            return new BVAlgebra(dtree, partition);
        }

        private BVAlgebra(DecisionTree dtree, IntervalSet[] partition) : base(dtree, partition, partition.Length)
        {
            var K = (nrOfBits - 1) / 64;
            int last = nrOfBits % 64;
            ulong lastMask = (last == 0 ? ulong.MaxValue : (((ulong)1 << last) - 1));
            all0 = new ulong[K];
            all1 = new ulong[K];
            for (int i = 0; i < K; i++)
            {
                all0[0] = 0;
                if (i < K - 1)
                {
                    all1[i] = ulong.MaxValue;
                }
                else
                {
                    all1[i] = lastMask;
                }
            }
            this.zero = new BV(0, all0);
            this.ones = new BV((K == 0 ? lastMask : ulong.MaxValue), all1);
            this.mtg = new MintermGenerator<BV>(this);
            this.atoms = new BV[nrOfBits];
            for (int i = 0; i < nrOfBits; i++)
            {
                atoms[i] = MkBV(i);
            }
        }

        public BV False
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

        public BV True
        {
            get
            {
                return ones;
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

        public bool AreEquivalent(BV predicate1, BV predicate2)
        {
            return predicate1.Equals(predicate2);
        }

        public IEnumerable<Tuple<bool[], BV>> GenerateMinterms(params BV[] constraints)
        {
            return this.mtg.GenerateMinterms(constraints);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsSatisfiable(BV predicate)
        {
            return !predicate.Equals(zero);
        }

        public BV MkAnd(params BV[] predicates)
        {
            var and = ones;
            for (int i = 0; i < predicates.Length; i++)
            {
                and = and & predicates[i];
                if (and.Equals(zero))
                    return zero;
            }
            return and;
        }

        public BV MkAnd(IEnumerable<BV> predicates)
        {
            throw new NotImplementedException();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BV MkAnd(BV predicate1, BV predicate2)
        {
            return predicate1 & predicate2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BV MkDiff(BV predicate1, BV predicate2)
        {
            return predicate1 & ~predicate2; 
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public BV MkNot(BV predicate)
        {
            return ones & ~predicate;
        }

        public BV MkOr(IEnumerable<BV> predicates)
        {
            var res = zero;
            foreach (var p in predicates)
            {
                res = res | p;
                if (res.Equals(ones))
                    return ones;
            }
            return res;
        }

        public BV MkOr(BV predicate1, BV predicate2)
        {
            return predicate1 | predicate2;
        }

        public BV MkBV(params int[] truebits)
        {
            ulong first = 0;
            var more = new ulong[this.all0.Length];
            for (int i = 0; i < truebits.Length; i++)
            {
                int b = truebits[i];
                if (b >= nrOfBits || b < 0)
                    throw new AutomataException(AutomataExceptionKind.BitOutOfRange);
                int k = b / 64;
                int j = b % 64;
                if (k == 0)
                    first = first | ((ulong)1 << j);
                else
                    more[k-1] = more[k-1] | ((ulong)1 << j);
            }
            var bv = new BV(first, more);
            return bv;
        }

        public BV MkRangeConstraint(char lower, char upper, bool caseInsensitive = false)
        {
            throw new NotSupportedException();
        }

        public BV MkCharConstraint(char c, bool caseInsensitive = false)
        {
            if (caseInsensitive == true)
                throw new AutomataException(AutomataExceptionKind.NotSupported);

            int i = this.dtree.GetId(c);
            return this.atoms[i];
        }

        /// <summary>
        /// Assumes that set is a union of some minterms (or empty).
        /// If null then null is returned.
        /// </summary>
        public BV ConvertFromCharSet(BDD set)
        {
            if (set == null)
                return null;
            var alg = set.algebra;
            BV res = this.zero;
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

        public BDD ConvertToCharSet(BDDAlgebra solver, BV pred)
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

        public BV[] GetPartition()
        {
            return atoms;
        }

        public IEnumerable<char> GenerateAllCharacters(BV set)
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
        public BVAlgebra(SerializationInfo info, StreamingContext context)
            : this((DecisionTree)info.GetValue("d", typeof(DecisionTree)),
                  DeserializePartition(info.GetString("p")))
        {
        }

        /// <summary>
        /// calls bv.Serialize()
        /// </summary>
        public string SerializePredicate(BV bv)
        {
            return bv.Serialize();
        }

        /// <summary>
        /// calls BV.Deserialize(s)
        /// </summary>
        public BV DeserializePredicate(string s)
        {
            return BV.Deserialize(s);
        }
        #endregion

        public BV MkCharPredicate(string name, BV pred)
        {
            throw new NotImplementedException();
        }
    }
}
