// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;

namespace System.Text.RegularExpressions.SRM
{
    // types used as keys in BDD operation caches
    using BoolOpKey = ValueTuple<BoolOp, BDD, BDD>;
    using ShiftOpKey = ValueTuple<BDD, int>;

    /// <summary>
    /// Boolean operations over BDDs.
    /// </summary>
    internal enum BoolOp
    {
        OR, AND, XOR, NOT
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
        /// Operation cache for binary Boolean operations over BDDs.
        /// Here BoolOpKey.Item1 is one of: OR, AND, XOR.
        /// </summary>
        private readonly Dictionary<BoolOpKey, BDD> _binOpCache = new Dictionary<BoolOpKey, BDD>();

        /// <summary>
        /// Operation cache for complementing BDDs.
        /// </summary>
        private readonly Dictionary<BDD, BDD> _notCache = new Dictionary<BDD, BDD>();

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
        /// Construct a solver for BDDs.
        /// </summary>
        public BDDAlgebra() => _mintermGen = new MintermGenerator<BDD>(this);

        /// <summary>
        /// Assumes op is a binary commutative operation: one of OR, AND, XOR.
        /// Treats the arguments as if they are unordered.
        /// Orders left and right by hashcode in the constructed key.
        /// </summary>
        private static BoolOpKey MkBoolOpKey(BoolOp op, BDD left, BDD right)
        {
            if (left.GetHashCode() <= right.GetHashCode())
                return new BoolOpKey(op, left, right);

            return new BoolOpKey(op, right, left);
        }

        /// <summary>
        /// Create a BDD with given ordinal and given one and zero child.
        /// Returns the BDD from the cache if it already exists.
        /// Must be executed in a single thread mode.
        /// </summary>
        public BDD MkBDD(int ordinal, BDD one, BDD zero)
        {
            var key = new BDD(ordinal, one, zero);
            if (!_bddCache.TryGetValue(key, out BDD set))
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
        public BDD MkOr(BDD a, BDD b)
        {
            //one of a or b is a leaf
            if (a == False)
                return b;

            if (b == False)
                return a;

            if (a == True || b == True)
                return True;

            if (a == b)
                return a;

            BoolOpKey key = MkBoolOpKey(BoolOp.OR, a, b);
            return _binOpCache.TryGetValue(key, out BDD res) ?
                res :
                MkBoolOP_lock(key);
        }

        /// <summary>
        /// Make the intersection of a and b
        /// </summary>
        public BDD MkAnd(BDD a, BDD b)
        {
            if (a == True)
                return b;

            if (b == True)
                return a;

            if (a == False || b == False)
                return False;

            if (a == b)
                return a;

            BoolOpKey key = MkBoolOpKey(BoolOp.AND, a, b);
            return _binOpCache.TryGetValue(key, out BDD res) ?
                res :
                MkBoolOP_lock(key);
        }

        /// <summary>
        /// Complement a
        /// </summary>
        public BDD MkNot(BDD a) =>
            a == False ? True :
            a == True ? False :
            _notCache.TryGetValue(a, out BDD neg) ? neg :
            MkBoolOP_lock(new BoolOpKey(BoolOp.NOT, a, null));

        /// <summary>
        /// Apply the operation in the key in a thread safe manner.
        /// All new entries in _boolOpCache and _notCache are created through this call.
        /// </summary>
        /// <param name="key">contains the Boolean operation and two BDD arguments</param>
        private BDD MkBoolOP_lock(BoolOpKey key)
        {
            //updates to _boolOpCache and _notCache  may only happen through this method call
            lock (this)
            {
                BoolOp op = key.Item1;
                BDD a = key.Item2;
                BDD? b = key.Item3;
                BDD res;

                if (op == BoolOp.NOT)
                {
                    if (a.IsLeaf)
                    {
                        //multi-terminal case, we know here that a is neither True nor False
                        int ord = CombineTerminals(op, a.Ordinal, 0);
                        res = MkBDD(ord, null, null);
                        _notCache[a] = res;
                        return res;
                    }
                    else
                    {
                        res = MkBDD(a.Ordinal, MkNot_rec(a.One), MkNot_rec(a.Zero));
                        _notCache[a] = res;
                        return res;
                    }
                }

                if (a.IsLeaf && b.IsLeaf)
                {
                    //multi-terminal case, we know here that a is neither True nor False
                    int ord = CombineTerminals(op, a.Ordinal, b.Ordinal);
                    res = MkBDD(ord, null, null);
                }
                else if (a.IsLeaf || b.Ordinal > a.Ordinal)
                {
                    BDD t = MkBinBoolOP_rec(op, a, b.One);
                    BDD f = MkBinBoolOP_rec(op, a, b.Zero);
                    res = t == f ? t : MkBDD(b.Ordinal, t, f);
                }
                else if (b.IsLeaf || a.Ordinal > b.Ordinal)
                {
                    BDD t = MkBinBoolOP_rec(op, a.One, b);
                    BDD f = MkBinBoolOP_rec(op, a.Zero, b);
                    res = t == f ? t : MkBDD(a.Ordinal, t, f);
                }
                else
                {
                    BDD t = MkBinBoolOP_rec(op, a.One, b.One);
                    BDD f = MkBinBoolOP_rec(op, a.Zero, b.Zero);
                    res = t == f ? t : MkBDD(a.Ordinal, t, f);
                }

                _binOpCache[key] = res;
                return res;
            }
        }

        /// <summary>
        /// Applies the binary Boolean operation op and constructs the BDD recursively from a and b.
        /// Is executed in a single thread mode.
        /// </summary>
        /// <param name="op">given binary Boolean operation</param>
        /// <param name="a">first BDD</param>
        /// <param name="b">second BDD</param>
        /// <returns></returns>
        private BDD MkBinBoolOP_rec(BoolOp op, BDD a, BDD b)
        {
            #region the cases when one of a or b is True or False or when a == b
            switch (op)
            {
                case BoolOp.OR:
                    if (a == False)
                        return b;
                    if (b == False)
                        return a;
                    if (a == True || b == True)
                        return True;
                    if (a == b)
                        return a;
                    break;

                case BoolOp.AND:
                    if (a == True)
                        return b;
                    if (b == True)
                        return a;
                    if (a == False || b == False)
                        return False;
                    if (a == b)
                        return a;
                    break;
                case BoolOp.XOR:
                    if (a == False)
                        return b;
                    if (b == False)
                        return a;
                    if (a == b)
                        return False;
                    if (a == True)
                        return MkNot_rec(b);
                    if (b == True)
                        return MkNot_rec(a);
                    break;
                default:
                    Debug.Assert(false, "Unhandled binary BoolOp case");
                    break;
            }
            #endregion

            BoolOpKey key = MkBoolOpKey(op, a, b);
            if (_binOpCache.TryGetValue(key, out BDD res))
                return res;

            if (a.IsLeaf && b.IsLeaf)
            {
                //multi-terminal case, we know here that a is neither True nor False
                int ord = CombineTerminals(op, a.Ordinal, b.Ordinal);
                res = MkBDD(ord, null, null);
            }
            else
            {
                if (a.IsLeaf || b.Ordinal > a.Ordinal)
                {
                    BDD t = MkBinBoolOP_rec(op, a, b.One);
                    BDD f = MkBinBoolOP_rec(op, a, b.Zero);
                    res = t == f ? t : MkBDD(b.Ordinal, t, f);
                }
                else if (b.IsLeaf || a.Ordinal > b.Ordinal)
                {
                    BDD t = MkBinBoolOP_rec(op, a.One, b);
                    BDD f = MkBinBoolOP_rec(op, a.Zero, b);
                    res = t == f ? t : MkBDD(a.Ordinal, t, f);
                }
                else
                {
                    BDD t = MkBinBoolOP_rec(op, a.One, b.One);
                    BDD f = MkBinBoolOP_rec(op, a.Zero, b.Zero);
                    res = t == f ? t : MkBDD(a.Ordinal, t, f);
                }
            }

            _binOpCache[key] = res;
            return res;
        }

        /// <summary>
        /// Negate a.
        /// Is executed in a single thread mode.
        /// </summary>
        private BDD MkNot_rec(BDD a)
        {
            if (a == False)
                return True;
            if (a == True)
                return False;

            if (_notCache.TryGetValue(a, out BDD neg))
                return neg;

            neg = a.IsLeaf ?
                MkBDD(CombineTerminals(BoolOp.NOT, a.Ordinal, 0), null, null) : // multi-terminal case
                MkBDD(a.Ordinal, MkNot_rec(a.One), MkNot_rec(a.Zero));
            _notCache[a] = neg;
            return neg;
        }

        /// <summary>
        /// Intersect all sets in the enumeration
        /// </summary>
        public BDD MkAnd(IEnumerable<BDD> sets)
        {
            BDD res = True;
            foreach (BDD bdd in sets)
            {
                res = MkAnd(res, bdd);
            }
            return res;
        }

        /// <summary>
        /// Intersect all sets in the array
        /// </summary>
        public BDD MkAnd(params BDD[] sets)
        {
            BDD res = True;
            foreach (BDD bdd in sets)
            {
                res = MkAnd(res, bdd);
            }
            return res;
        }

        /// <summary>
        /// Take the union of all sets in the enumeration
        /// </summary>
        public BDD MkOr(IEnumerable<BDD> sets)
        {
            BDD res = False;
            foreach (BDD bdd in sets)
            {
                res = MkOr(res, bdd);
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
        public bool AreEquivalent(BDD a, BDD b) => MkXOr(a, b) == False;

        #endregion

        /// <summary>
        /// Make the XOR of a and b
        /// </summary>
        internal BDD MkXOr(BDD a, BDD b)
        {
            if (a == False)
                return b;
            if (b == False)
                return a;
            if (a == True)
                return MkNot(b);
            if (b == True)
                return MkNot(a);
            if (a == b)
                return False;

            BoolOpKey key = MkBoolOpKey(BoolOp.XOR, a, b);
            return _binOpCache.TryGetValue(key, out BDD res) ?
                res :
                MkBoolOP_lock(key);
        }

        #region bit-shift operations

        /// <summary>
        /// Shift all elements k (=1 by default) bits to the right.
        /// For example if set denotes {*0000,*1110,*1111} then
        /// ShiftRight(set) denotes {*000,*111} where * denotes any prefix of 0's or 1's.
        /// </summary>
        public BDD ShiftRight(BDD set, int k = 1) =>
            k < 0 ? throw new AutomataException(AutomataExceptionKind.InvalidArgument) :
            set.IsLeaf || k == 0 ? set :
            Shift_lock(set, 0 - k);

        /// <summary>
        /// Shift all elements k bits to the left.
        /// For example if k=1 and set denotes {*0000,*1111} then
        /// ShiftLeft(set) denotes {*00000,*00001,*11110,*11111} where * denotes any prefix of 0's or 1's.
        /// </summary>
        public BDD ShiftLeft(BDD set, int k = 1) =>
            k < 0 ? throw new AutomataException(AutomataExceptionKind.InvalidArgument) :
            set.IsLeaf || k == 0 ? set :
            Shift_lock(set, k);

        /// <summary>
        /// Allow shift_lock only single thread at a time because _bddCache is updated.
        /// </summary>
        private BDD Shift_lock(BDD set, int k)
        {
            lock (this)
            {
                return Shift_rec(new Dictionary<ShiftOpKey, BDD>(), set, k);
            }
        }

        /// <summary>
        /// Uses shiftCache to avoid recomputations in shared BDDs (DAGs).
        /// Is executed in a single thread mode.
        /// </summary>
        private BDD Shift_rec(Dictionary<ShiftOpKey, BDD> shiftCache, BDD set, int k)
        {
            if (set.IsLeaf || k == 0)
                return set;

            int ordinal = set.Ordinal + k;

            if (ordinal < 0)
                return True;  //this arises if k is negative

            var key = new ShiftOpKey(set, k);

            if (shiftCache.TryGetValue(key, out BDD res))
                return res;

            BDD zero = Shift_rec(shiftCache, set.Zero, k);
            BDD one = Shift_rec(shiftCache, set.One, k);

            res = (zero == one) ?
                zero :
                MkBDD((ushort)ordinal, one, zero);
            shiftCache[key] = res;
            return res;
        }

        #endregion

        /// <summary>
        /// Generate all non-overlapping Boolean combinations of a set of BDDs.
        /// </summary>
        /// <param name="sets">the BDDs to create the minterms for</param>
        /// <returns>
        /// tuples of booleans indicating which of the input sets are true in the minterm and the BDD for the minterm
        /// </returns>
        public IEnumerable<Tuple<bool[], BDD>> GenerateMinterms(params BDD[] sets) => _mintermGen.GenerateMinterms(sets);


        /// <summary>
        /// Make a set containing all integers whose bits up to maxBit equal n.
        /// </summary>
        /// <param name="n">the given integer</param>
        /// <param name="maxBit">bits above maxBit are unspecified</param>
        /// <returns></returns>
        public BDD MkSetFrom(uint n, int maxBit) => MkSetFromRange(n, n, maxBit);

        /// <summary>
        /// Make the set containing all values greater than or equal to m and less than or equal to n when considering bits between 0 and maxBit.
        /// Is executed in a single thread mode.
        /// </summary>
        /// <param name="m">lower bound</param>
        /// <param name="n">upper bound</param>
        /// <param name="maxBit">bits above maxBit are unspecified</param>
        public BDD MkSetFromRange(uint m, uint n, int maxBit)
        {
            lock (this)
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

                return CreateFromInterval_rec(mask, maxBit, m, n);
            }
        }

        /// <summary>
        /// Is executed in single-threaded mode, makes updates to _bddCache.
        /// </summary>
        private BDD CreateFromInterval_rec(uint mask, int bit, uint m, uint n)
        {
            if (mask == 1) //base case: LSB
            {
                return
                    n == 0 ? MkBDD((ushort)bit, False, True) : //implies that m==0
                    m == 1 ? MkBDD((ushort)bit, True, False) : //implies that n==1
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
                return MkBDD((ushort)bit, False, fcase);
            }
            else if (mb == mask) // implies that 0-branch is empty
            {
                BDD tcase = CreateFromInterval_rec(mask >> 1, bit - 1, m & ~mask, n & ~mask);
                return MkBDD((ushort)bit, tcase, False);
            }
            else //split the interval in two
            {
                BDD fcase = CreateFromInterval_rec(mask >> 1, bit - 1, m, mask - 1);
                BDD tcase = CreateFromInterval_rec(mask >> 1, bit - 1, 0, n & ~mask);
                return MkBDD((ushort)bit, tcase, fcase);
            }
        }

        /// <summary>
        /// Convert the set into an equivalent array of uint ranges.
        /// Bits above maxBit are ignored.
        /// The ranges are nonoverlapping and ordered.
        /// If limit > 0 and there are more ranges than limit then return null.
        /// </summary>
        public static Tuple<uint, uint>[] ToRanges(BDD set, int maxBit, int limit = 0)
        {
            Tuple<uint, uint>[] ranges = RangeConverter.ToRanges(set, maxBit);

            if (limit == 0 || ranges.Length <= limit)
                return ranges;

            return null;
        }

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

        #region Serialializing and deserializing BDDs from dags encoded by ulongs arrays
        /// <summary>
        /// Serializes the BDD using BDD.Serialize(StringBuilder)
        /// </summary>
        public void SerializePredicate(BDD bdd, StringBuilder builder) => bdd.Serialize(builder);

        /// <summary>
        /// Calls BDD.Deserialize(s, this)
        /// </summary>
        public BDD DeserializePredicate(string s) => BDD.Deserialize(s, this);
        #endregion

        /// <summary>
        /// Throws NotSupportedException.
        /// Can be overwridden in a derived algebra.
        /// The returned integer must be nonegative
        /// and will act as the combined terminal in a multi-terminal BDD.
        /// </summary>
        public virtual int CombineTerminals(BoolOp op, int terminal1, int terminal2) => throw new NotSupportedException($"{nameof(CombineTerminals)}:{op}");

        /// <summary>
        /// Replace the True node in the BDD by a non-Boolean terminal.
        /// Locks the algebra for single threaded use.
        /// Observe that the Ordinal of False is -1 and the Ordinal of True is -2.
        /// </summary>
        public BDD ReplaceTrue(BDD bdd, int terminal)
        {
            Debug.Assert(terminal >= 0);

            lock (this)
            {
                BDD leaf = MkBDD(terminal, null, null);
                return ReplaceTrue_(bdd, leaf, new Dictionary<BDD, BDD>());
            }
        }

        private BDD ReplaceTrue_(BDD bdd, BDD leaf, Dictionary<BDD, BDD> cache)
        {
            if (bdd == True)
                return leaf;

            if (bdd.IsLeaf)
                return bdd;

            if (cache.TryGetValue(bdd, out BDD res))
                return res;

            BDD one = ReplaceTrue_(bdd.One, leaf, cache);
            BDD zero = ReplaceTrue_(bdd.Zero, leaf, cache);
            res = MkBDD(bdd.Ordinal, one, zero);
            cache[bdd] = res;
            return res;
        }

        public void Serialize(StringBuilder sb) => throw new NotSupportedException(nameof(BDDAlgebra.Serialize));
    }
}
