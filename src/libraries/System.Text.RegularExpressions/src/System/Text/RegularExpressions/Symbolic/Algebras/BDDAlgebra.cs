// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace System.Text.RegularExpressions.Symbolic
{
    // types used as keys in BDD operation caches
    using BoolOpKey = ValueTuple<BoolOp, BDD, BDD?>;
    using ShiftOpKey = ValueTuple<BDD, int>;

    /// <summary>
    /// Boolean operations over BDDs.
    /// </summary>
    internal enum BoolOp
    {
        Or,
        And,
        Xor,
        Not
    }

    /// <summary>
    /// Boolean algebra for Binary Decision Diagrams. Boolean operations on BDDs are cached for efficiency. The
    /// IBooleanAlgebra interface implemented by this class is thread safe.
    /// TBD: policy for clearing/reducing the caches when they grow too large.
    /// Ultimately, the caches are crucial for efficiency, not for correctness.
    /// </summary>
    internal abstract class BDDAlgebra : IBooleanAlgebra<BDD>
    {
        /// <summary>
        /// Operation cache for Boolean operations over BDDs.
        /// </summary>
        private readonly Dictionary<BoolOpKey, BDD> _opCache = new Dictionary<BoolOpKey, BDD>();

        /// <summary>
        /// Internalize the creation of BDDs so that two BDDs with same ordinal and identical children are the same object.
        /// The algorithms do not rely on 100% internalization
        /// (they could but this would make it difficult (or near impossible) to clear caches.
        /// Allowing distinct but equivalent BDDs is also a tradeoff between efficiency and flexibility.
        /// </summary>
        private readonly HashSet<BDD> _bddCache = new HashSet<BDD>();

        /// <summary>
        /// Generator for minterms.
        /// </summary>
        private readonly MintermGenerator<BDD> _mintermGen;

        /// <summary>
        /// All accesses to the caches are protected with this lock.
        /// </summary>
        internal readonly ReaderWriterLockSlim Lock = new();

        /// <summary>
        /// Construct a solver for BDDs.
        /// </summary>
        public BDDAlgebra() => _mintermGen = new MintermGenerator<BDD>(this);

        /// <summary>
        /// Assumes op is a binary commutative operation: one of OR, AND, XOR.
        /// Treats the arguments as if they are unordered.
        /// Orders left and right by hashcode in the constructed key.
        /// </summary>
        private static BoolOpKey CreateBinOpKey(BoolOp op, BDD left, BDD right) =>
            left.GetHashCode() <= right.GetHashCode() ?
                new BoolOpKey(op, left, right) :
                new BoolOpKey(op, right, left);

        /// <summary>
        /// Create a BDD with given ordinal and given one and zero child.
        /// Returns the BDD from the cache if it already exists.
        /// Lock must be held in write mode when this method is called.
        /// </summary>
        public BDD GetOrCreateBDD(int ordinal, BDD? one, BDD? zero)
        {
            Debug.Assert(Lock.IsWriteLockHeld);

            var key = new BDD(ordinal, one, zero);
            if (!_bddCache.TryGetValue(key, out BDD? set))
            {
                set = key;
                _bddCache.Add(set);
            }

            return set;
        }

        #region IBooleanAlgebra members

        /// <summary>
        /// Make the union of a and b
        /// </summary>
        public BDD Or(BDD a, BDD b) =>
            a == False ? b :
            b == False ? a :
            a == True || b == True ? True :
            a == b ? a :
            GetOrCreateBoolOp(CreateBinOpKey(BoolOp.Or, a, b));

        /// <summary>
        /// Make the intersection of a and b
        /// </summary>
        public BDD And(BDD a, BDD b) =>
            a == True ? b :
            b == True ? a :
            a == False || b == False ? False :
            a == b ? a :
            GetOrCreateBoolOp(CreateBinOpKey(BoolOp.And, a, b));

        /// <summary>
        /// Complement a
        /// </summary>
        public BDD Not(BDD a) =>
            a == False ? True :
            a == True ? False :
            GetOrCreateBoolOp(new BoolOpKey(BoolOp.Not, a, null));

        private BDD GetOrCreateBoolOp(BoolOpKey key)
        {
            Lock.EnterReadLock();
            try
            {
                if (_opCache.TryGetValue(key, out BDD? result))
                {
                    return result;
                }
            }
            finally
            {
                Lock.ExitReadLock();
            }
            // No result was found in cache
            Lock.EnterUpgradeableReadLock();
            try
            {
                // Check again for entry in cache, since another thread may have created it when read lock was released
                if (!_opCache.TryGetValue(key, out BDD? result))
                {
                    Lock.EnterWriteLock();
                    try
                    {
                        result = CreateBoolOP_lock(key);
                    }
                    finally
                    {
                        Lock.ExitWriteLock();
                    }
                }
                return result;
            }
            finally
            {
                Lock.ExitUpgradeableReadLock();
            }
        }

        /// <summary>
        /// Apply the operation in the key in a thread safe manner.
        /// All new entries in _boolOpCache and _notCache are created through this call.
        /// </summary>
        /// <param name="key">contains the Boolean operation and two BDD arguments</param>
        private BDD CreateBoolOP_lock(BoolOpKey key)
        {
            Debug.Assert(Lock.IsWriteLockHeld);

            BoolOp op = key.Item1;
            BDD a = key.Item2;
            BDD? b = key.Item3;
            BDD res;

            if (op == BoolOp.Not)
            {
                if (a.IsLeaf)
                {
                    //multi-terminal case, we know here that a is neither True nor False
                    int ord = CombineTerminals(op, a.Ordinal, 0);
                    res = GetOrCreateBDD(ord, null, null);
                }
                else
                {
                    res = GetOrCreateBDD(a.Ordinal, CreateNot_rec(a.One), CreateNot_rec(a.Zero));
                }
            }
            else
            {
                Debug.Assert(b is not null);

                if (a.IsLeaf && b.IsLeaf)
                {
                    //multi-terminal case, we know here that a is neither True nor False
                    int ord = CombineTerminals(op, a.Ordinal, b.Ordinal);
                    res = GetOrCreateBDD(ord, null, null);
                }
                else if (a.IsLeaf || b.Ordinal > a.Ordinal)
                {
                    Debug.Assert(!b.IsLeaf);
                    BDD t = CreateBinBoolOP_rec(op, a, b.One);
                    BDD f = CreateBinBoolOP_rec(op, a, b.Zero);
                    res = t == f ? t : GetOrCreateBDD(b.Ordinal, t, f);
                }
                else if (b.IsLeaf || a.Ordinal > b.Ordinal)
                {
                    Debug.Assert(!a.IsLeaf);
                    BDD t = CreateBinBoolOP_rec(op, a.One, b);
                    BDD f = CreateBinBoolOP_rec(op, a.Zero, b);
                    res = t == f ? t : GetOrCreateBDD(a.Ordinal, t, f);
                }
                else
                {
                    Debug.Assert(!a.IsLeaf);
                    Debug.Assert(!b.IsLeaf);
                    BDD t = CreateBinBoolOP_rec(op, a.One, b.One);
                    BDD f = CreateBinBoolOP_rec(op, a.Zero, b.Zero);
                    res = t == f ? t : GetOrCreateBDD(a.Ordinal, t, f);
                }
            }

            _opCache[key] = res;
            return res;
        }

        /// <summary>
        /// Applies the binary Boolean operation op and constructs the BDD recursively from a and b.
        /// Is executed in a single thread mode.
        /// </summary>
        /// <param name="op">given binary Boolean operation</param>
        /// <param name="a">first BDD</param>
        /// <param name="b">second BDD</param>
        /// <returns></returns>
        private BDD CreateBinBoolOP_rec(BoolOp op, BDD a, BDD b)
        {
            Debug.Assert(Lock.IsWriteLockHeld);

            #region the cases when one of a or b is True or False or when a == b
            switch (op)
            {
                case BoolOp.Or:
                    if (a == False)
                        return b;
                    if (b == False)
                        return a;
                    if (a == True || b == True)
                        return True;
                    if (a == b)
                        return a;
                    break;

                case BoolOp.And:
                    if (a == True)
                        return b;
                    if (b == True)
                        return a;
                    if (a == False || b == False)
                        return False;
                    if (a == b)
                        return a;
                    break;

                case BoolOp.Xor:
                    if (a == False)
                        return b;
                    if (b == False)
                        return a;
                    if (a == b)
                        return False;
                    if (a == True)
                        return CreateNot_rec(b);
                    if (b == True)
                        return CreateNot_rec(a);
                    break;

                default:
                    Debug.Fail("Unhandled binary BoolOp case");
                    break;
            }
            #endregion

            BoolOpKey key = CreateBinOpKey(op, a, b);
            if (_opCache.TryGetValue(key, out BDD? res))
                return res;

            if (a.IsLeaf && b.IsLeaf)
            {
                //multi-terminal case, we know here that a is neither True nor False
                int ord = CombineTerminals(op, a.Ordinal, b.Ordinal);
                res = GetOrCreateBDD(ord, null, null);
            }
            else if (a.IsLeaf || b.Ordinal > a.Ordinal)
            {
                Debug.Assert(!b.IsLeaf);
                BDD t = CreateBinBoolOP_rec(op, a, b.One);
                BDD f = CreateBinBoolOP_rec(op, a, b.Zero);
                res = t == f ? t : GetOrCreateBDD(b.Ordinal, t, f);
            }
            else if (b.IsLeaf || a.Ordinal > b.Ordinal)
            {
                Debug.Assert(!a.IsLeaf);
                BDD t = CreateBinBoolOP_rec(op, a.One, b);
                BDD f = CreateBinBoolOP_rec(op, a.Zero, b);
                res = t == f ? t : GetOrCreateBDD(a.Ordinal, t, f);
            }
            else
            {
                Debug.Assert(!a.IsLeaf);
                Debug.Assert(!b.IsLeaf);
                BDD t = CreateBinBoolOP_rec(op, a.One, b.One);
                BDD f = CreateBinBoolOP_rec(op, a.Zero, b.Zero);
                res = t == f ? t : GetOrCreateBDD(a.Ordinal, t, f);
            }

            _opCache[key] = res;
            return res;
        }

        /// <summary>
        /// Negate a.
        /// Is executed in a single thread mode.
        /// </summary>
        private BDD CreateNot_rec(BDD a)
        {
            Debug.Assert(Lock.IsWriteLockHeld);

            if (a == False)
                return True;
            if (a == True)
                return False;

            BoolOpKey key = new(BoolOp.Not, a, null);
            if (_opCache.TryGetValue(key, out BDD? neg))
                return neg;

            neg = a.IsLeaf ?
                GetOrCreateBDD(CombineTerminals(BoolOp.Not, a.Ordinal, 0), null, null) : // multi-terminal case
                GetOrCreateBDD(a.Ordinal, CreateNot_rec(a.One), CreateNot_rec(a.Zero));
            _opCache[key] = neg;
            return neg;
        }

        /// <summary>
        /// Intersect all sets in the enumeration
        /// </summary>
        public BDD And(IEnumerable<BDD> sets)
        {
            BDD res = True;
            foreach (BDD bdd in sets)
            {
                res = And(res, bdd);
            }
            return res;
        }

        /// <summary>
        /// Take the union of all sets in the enumeration
        /// </summary>
        public BDD Or(IEnumerable<BDD> sets)
        {
            BDD res = False;
            foreach (BDD bdd in sets)
            {
                res = Or(res, bdd);
            }
            return res;
        }

        /// <summary>
        /// Gets the full set.
        /// </summary>
        public BDD True => BDD.True;

        /// <summary>
        /// Gets the empty set.
        /// </summary>
        public BDD False => BDD.False;

        /// <summary>
        /// Returns true if the set is nonempty.
        /// </summary>
        public bool IsSatisfiable(BDD set) => set != False;

        /// <summary>
        /// Returns true if a and b represent equivalent BDDs.
        /// </summary>
        public bool AreEquivalent(BDD a, BDD b) => Xor(a, b) == False;

        #endregion

        /// <summary>
        /// Make the XOR of a and b
        /// </summary>
        internal BDD Xor(BDD a, BDD b) =>
            a == False ? b :
            b == False ? a :
            a == True ? Not(b) :
            b == True ? Not(a) :
            a == b ? False :
            GetOrCreateBoolOp(CreateBinOpKey(BoolOp.Xor, a, b));

        #region bit-shift operations

        /// <summary>
        /// Shift all elements k bits to the right.
        /// For example if set denotes {*0000,*1110,*1111} then
        /// ShiftRight(set) denotes {*000,*111} where * denotes any prefix of 0's or 1's.
        /// </summary>
        public BDD ShiftRight(BDD set, int k)
        {
            Debug.Assert(k >= 0);
            return set.IsLeaf ? set : Shift_lock(set, 0 - k);
        }

        /// <summary>
        /// Shift all elements k bits to the left.
        /// For example if k=1 and set denotes {*0000,*1111} then
        /// ShiftLeft(set) denotes {*00000,*00001,*11110,*11111} where * denotes any prefix of 0's or 1's.
        /// </summary>
        public BDD ShiftLeft(BDD set, int k)
        {
            Debug.Assert(k >= 0);
            return set.IsLeaf ? set : Shift_lock(set, k);
        }

        /// <summary>
        /// Allow shift_lock only single thread at a time because _bddCache is updated.
        /// </summary>
        private BDD Shift_lock(BDD set, int k)
        {
            Lock.EnterWriteLock();
            try
            {
                return Shift_rec(new Dictionary<ShiftOpKey, BDD>(), set, k);
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Uses shiftCache to avoid recomputations in shared BDDs (DAGs).
        /// Is executed in a single thread mode.
        /// </summary>
        private BDD Shift_rec(Dictionary<ShiftOpKey, BDD> shiftCache, BDD set, int k)
        {
            Debug.Assert(Lock.IsWriteLockHeld);

            if (set.IsLeaf || k == 0)
                return set;

            int ordinal = set.Ordinal + k;

            if (ordinal < 0)
                return True;  //this arises if k is negative

            var key = new ShiftOpKey(set, k);

            if (shiftCache.TryGetValue(key, out BDD? res))
                return res;

            BDD zero = Shift_rec(shiftCache, set.Zero, k);
            BDD one = Shift_rec(shiftCache, set.One, k);

            res = (zero == one) ?
                zero :
                GetOrCreateBDD((ushort)ordinal, one, zero);
            shiftCache[key] = res;
            return res;
        }

        #endregion

        /// <summary>
        /// Generate all non-overlapping Boolean combinations of a set of BDDs.
        /// </summary>
        /// <param name="sets">the BDDs to create the minterms for</param>
        /// <returns>BDDs for the minterm</returns>
        public List<BDD> GenerateMinterms(params BDD[] sets) => _mintermGen.GenerateMinterms(sets);

        /// <summary>
        /// Make a set containing all integers whose bits up to maxBit equal n.
        /// </summary>
        /// <param name="n">the given integer</param>
        /// <param name="maxBit">bits above maxBit are unspecified</param>
        /// <returns></returns>
        public BDD CreateSetFrom(uint n, int maxBit) => CreateSetFromRange(n, n, maxBit);

        /// <summary>
        /// Make the set containing all values greater than or equal to m and less than or equal to n when considering bits between 0 and maxBit.
        /// Is executed in a single thread mode.
        /// </summary>
        /// <param name="m">lower bound</param>
        /// <param name="n">upper bound</param>
        /// <param name="maxBit">bits above maxBit are unspecified</param>
        public BDD CreateSetFromRange(uint m, uint n, int maxBit)
        {

            if (n < m)
                return False;

            uint mask = (uint)1 << maxBit;

            //filter out bits greater than maxBit
            if (maxBit < 31)
            {
                uint filter = (mask << 1) - 1;
                m &= filter;
                n &= filter;
            }

            Lock.EnterWriteLock();
            try
            {
                return CreateFromInterval_rec(mask, maxBit, m, n);
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Is executed in single-threaded mode, makes updates to _bddCache.
        /// </summary>
        private BDD CreateFromInterval_rec(uint mask, int bit, uint m, uint n)
        {
            Debug.Assert(Lock.IsWriteLockHeld);

            if (mask == 1) //base case: LSB
            {
                return
                    n == 0 ? GetOrCreateBDD((ushort)bit, False, True) : //implies that m==0
                    m == 1 ? GetOrCreateBDD((ushort)bit, True, False) : //implies that n==1
                    True; //m=0 and n=1, thus full range from 0 to ((mask << 1)-1)
            }

            if (m == 0 && n == ((mask << 1) - 1)) //full interval
            {
                return True;
            }

            // mask > 1, i.e., mask = 2^b for some b > 0, and not full interval
            // e.g. m = x41 = 100 0001, n = x59 = 101 1001, mask = x40 = 100 0000, ord = 6 = log2(b)
            uint mb = m & mask; // e.g. mb = b
            uint nb = n & mask; // e.g. nb = b

            if (nb == 0) // implies that 1-branch is empty
            {
                BDD fcase = CreateFromInterval_rec(mask >> 1, bit - 1, m, n);
                return GetOrCreateBDD((ushort)bit, False, fcase);
            }
            else if (mb == mask) // implies that 0-branch is empty
            {
                BDD tcase = CreateFromInterval_rec(mask >> 1, bit - 1, m & ~mask, n & ~mask);
                return GetOrCreateBDD((ushort)bit, tcase, False);
            }
            else //split the interval in two
            {
                BDD fcase = CreateFromInterval_rec(mask >> 1, bit - 1, m, mask - 1);
                BDD tcase = CreateFromInterval_rec(mask >> 1, bit - 1, 0, n & ~mask);
                return GetOrCreateBDD((ushort)bit, tcase, fcase);
            }
        }

        /// <summary>
        /// Convert the set into an equivalent array of uint ranges.
        /// Bits above maxBit are ignored.
        /// The ranges are nonoverlapping and ordered.
        /// </summary>
        public static (uint, uint)[] ToRanges(BDD set, int maxBit) => BDDRangeConverter.ToRanges(set, maxBit);

        #region domain size and min computation

        /// <summary>
        /// Calculate the number of elements in the set. Returns 0 when set is full and maxBit is 63.
        /// </summary>
        /// <param name="set">the given set</param>
        /// <param name="maxBit">bits above maxBit are ignored</param>
        /// <returns>the cardinality of the set</returns>
        public virtual ulong ComputeDomainSize(BDD set, int maxBit)
        {
            if (maxBit < set.Ordinal)
                throw new ArgumentOutOfRangeException(nameof(maxBit));

            if (set == False)
                return 0UL;

            if (set == True)
                return 1UL << maxBit << 1; // e.g. if maxBit is 15 then the return value is 1 << 16, i.e., 2^16

            if (set.IsLeaf)
                throw new NotSupportedException(); // multi-terminal case is not supported

            ulong res = CalculateCardinality1(new Dictionary<BDD, ulong>(), set);
            if (maxBit > set.Ordinal)
            {
                res = (1UL << (maxBit - set.Ordinal)) * res;
            }

            return res;
        }

        /// <summary>
        /// Caches previously calculated values in sizeCache so that computations are not repeated inside a BDD for the same sub-BDD.
        /// Thus the number of internal calls is propotional to the number of nodes of the BDD, that could otherwise be exponential in the worst case.
        /// The size cache used to be a static field but the current way makes it thread-safe without use of locks.
        /// </summary>
        /// <param name="sizeCache">previously computed sizes</param>
        /// <param name="set">given set to compute size of</param>
        /// <returns></returns>
        private ulong CalculateCardinality1(Dictionary<BDD, ulong> sizeCache, BDD set)
        {
            if (sizeCache.TryGetValue(set, out ulong size))
                return size;

            if (set.IsLeaf)
                throw new NotSupportedException(); //multi-terminal case is not supported

            ulong sizeL;
            ulong sizeR;
            if (set.Zero.IsEmpty)
            {
                sizeL = 0;
                sizeR = set.One.IsFull ?
                    (uint)1 << set.Ordinal :
                    ((uint)1 << (set.Ordinal - 1 - set.One.Ordinal)) * CalculateCardinality1(sizeCache, set.One);
            }
            else if (set.Zero.IsFull)
            {
                sizeL = 1UL << set.Ordinal;
                sizeR = set.One.IsEmpty ?
                    0UL :
                    (1UL << (set.Ordinal - 1 - set.One.Ordinal)) * CalculateCardinality1(sizeCache, set.One);
            }
            else
            {
                sizeL = (1UL << (set.Ordinal - 1 - set.Zero.Ordinal)) * CalculateCardinality1(sizeCache, set.Zero);
                sizeR =
                    set.One == False ? 0UL :
                    set.One == True ? 1UL << set.Ordinal :
                    (1UL << (set.Ordinal - 1 - set.One.Ordinal)) * CalculateCardinality1(sizeCache, set.One);
            }

            size = sizeL + sizeR;
            sizeCache[set] = size;
            return size;
        }

        /// <summary>
        /// Get the lexicographically minimum bitvector in the set as a ulong.
        /// Assumes that the set is nonempty and that the ordinal of the BDD is at most 63.
        /// </summary>
        /// <param name="set">the given nonempty set</param>
        /// <returns>the lexicographically smallest bitvector in the set</returns>
        public ulong GetMin(BDD set) => set.GetMin();

        #endregion

        /// <summary>
        /// Any two BDDs that are equivalent are isomorphic and have the same hashcode.
        /// </summary>
        public bool HashCodesRespectEquivalence => true;

        /// <summary>
        /// Two equivalent BDDs need not be identical
        /// </summary>
        public bool IsExtensional => false;

        /// <summary>
        /// The returned integer must be nonegative
        /// and will act as the combined terminal in a multi-terminal BDD.
        /// May throw NotSupportedException.
        /// </summary>
        public abstract int CombineTerminals(BoolOp op, int terminal1, int terminal2);

        /// <summary>
        /// Replace the True node in the BDD by a non-Boolean terminal.
        /// Locks the algebra for single threaded use.
        /// Observe that the Ordinal of False is -1 and the Ordinal of True is -2.
        /// </summary>
        public BDD ReplaceTrue(BDD bdd, int terminal)
        {
            Debug.Assert(terminal >= 0);

            Lock.EnterWriteLock();
            try
            {
                BDD leaf = GetOrCreateBDD(terminal, null, null);
                return ReplaceTrue_rec(bdd, leaf, new Dictionary<BDD, BDD>());
            }
            finally
            {
                Lock.ExitWriteLock();
            }
        }

        private BDD ReplaceTrue_rec(BDD bdd, BDD leaf, Dictionary<BDD, BDD> cache)
        {
            if (bdd == True)
                return leaf;

            if (bdd.IsLeaf)
                return bdd;

            if (cache.TryGetValue(bdd, out BDD? res))
                return res;

            BDD one = ReplaceTrue_rec(bdd.One, leaf, cache);
            BDD zero = ReplaceTrue_rec(bdd.Zero, leaf, cache);
            res = GetOrCreateBDD(bdd.Ordinal, one, zero);
            cache[bdd] = res;
            return res;
        }
    }
}
