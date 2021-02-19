// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using BDD_Int = System.Tuple<System.Text.RegularExpressions.SRM.BDD, int>;
using BoolOpKey = System.Tuple<System.Text.RegularExpressions.SRM.BoolOp, System.Text.RegularExpressions.SRM.BDD, System.Text.RegularExpressions.SRM.BDD>;

namespace System.Text.RegularExpressions.SRM
{

    /// <summary>
    /// Boolean operations over BDDs.
    /// </summary>
    internal enum BoolOp
    {
        OR, AND, XOR, NOT
    }

    /// <summary>
    /// Solver for Specialized BDDs.
    /// TBD: policy for clearing/reducing the caches when they grow too large.
    /// Ultimately, the caches are crucial for efficiency, not for correctness.
    /// </summary>
    internal abstract class BDDAlgebra : IBooleanAlgebra<BDD>
    {
        /// <summary>
        /// Operation cache for binary Boolean operations over BDDs.
        /// Here BoolOpKey.Item1 is one of: OR, AND, XOR.
        /// </summary>
        private Dictionary<BoolOpKey, BDD> _binOpCache = new Dictionary<BoolOpKey, BDD>();
        /// <summary>
        /// Operation cache for complementing BDDs.
        /// </summary>
        private Dictionary<BDD, BDD> _notCache = new Dictionary<BDD, BDD>();

        /// <summary>
        /// Internalize the creation of BDDs so that two BDDs with same ordinal and identical children are the same object.
        /// The algorithms do not rely on 100% internalization
        /// (they could but this would make it difficult (or near impossible) to clear caches.
        /// Allowing distinct but equivalent BDDs is also a tradeoff between efficiency and flexibility.
        /// </summary>
        private HashSet<BDD> _bddCache = new HashSet<BDD>();

        /// <summary>
        /// Generator for minterms.
        /// </summary>
        private MintermGenerator<BDD> _mintermGen;

        /// <summary>
        /// Construct a solver for BDDs.
        /// </summary>
        public BDDAlgebra()
        {
            _mintermGen = new MintermGenerator<BDD>(this);
        }

        /// <summary>
        /// Create a BDD with given ordinal and given one and zero child.
        /// Returns the BDD from the cache if it already exists.
        /// Must be executed in a single thread mode.
        /// </summary>
        public BDD MkBDD(int ordinal, BDD one, BDD zero)
        {
            var key = new BDD(ordinal, one, zero);
            BDD set;
            if (!_bddCache.TryGetValue(key, out set))
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

            var key = new BoolOpKey(BoolOp.OR, a, b);
            BDD res;
            if (_binOpCache.TryGetValue(key, out res))
                return res;

            return MkBoolOP_lock(key);
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

            var key = new BoolOpKey(BoolOp.AND, a, b);
            BDD res;
            if (_binOpCache.TryGetValue(key, out res))
                return res;

            return MkBoolOP_lock(key);
        }

        /// <summary>
        /// Complement a
        /// </summary>
        public BDD MkNot(BDD a)
        {
            if (a == False)
                return True;
            if (a == True)
                return False;

            BDD neg;
            if (_notCache.TryGetValue(a, out neg))
                return neg;

            return MkBoolOP_lock(new BoolOpKey(BoolOp.NOT, a, null));
        }

        /// <summary>
        /// Apply the operation in the key in a thread safe manner.
        /// All new entries in _boolOpCache, _notCache, and _bddCache are created through this call.
        /// </summary>
        /// <param name="key">containing the Boolean operation and two BDD arguments</param>
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
                    res = MkBDD(a.Ordinal, MkNot_rec(a.One), MkNot_rec(a.Zero));
                    _notCache[a] = res;
                    return res;
                }

                if (b.Ordinal > a.Ordinal)
                {
                    BDD t = MkBinBoolOP_rec(op, a, b.One);
                    BDD f = MkBinBoolOP_rec(op, a, b.Zero);
                    res = (t == f ? t : MkBDD(b.Ordinal, t, f));
                }
                else if (a.Ordinal > b.Ordinal)
                {
                    BDD t = MkBinBoolOP_rec(op, a.One, b);
                    BDD f = MkBinBoolOP_rec(op, a.Zero, b);
                    res = (t == f ? t : MkBDD(a.Ordinal, t, f));
                }
                else
                {
                    BDD t = MkBinBoolOP_rec(op, a.One, b.One);
                    BDD f = MkBinBoolOP_rec(op, a.Zero, b.Zero);
                    res = (t == f ? t : MkBDD(a.Ordinal, t, f));
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
            #region the cases when one of a or b is a leaf or when a == b
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
                default: //BDDOp.XOR
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
            }
            #endregion

            var key = new BoolOpKey(op, a, b);
            BDD res;
            if (_binOpCache.TryGetValue(key, out res))
                return res;

            if (b.Ordinal > a.Ordinal)
            {
                BDD t = MkBinBoolOP_rec(op, a, b.One);
                BDD f = MkBinBoolOP_rec(op, a, b.Zero);
                res = (t == f ? t : MkBDD(b.Ordinal, t, f));
            }
            else if (a.Ordinal > b.Ordinal)
            {
                BDD t = MkBinBoolOP_rec(op, a.One, b);
                BDD f = MkBinBoolOP_rec(op, a.Zero, b);
                res = (t == f ? t : MkBDD(a.Ordinal, t, f));
            }
            else
            {
                BDD t = MkBinBoolOP_rec(op, a.One, b.One);
                BDD f = MkBinBoolOP_rec(op, a.Zero, b.Zero);
                res = (t == f ? t : MkBDD(a.Ordinal, t, f));
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

            BDD neg;
            if (_notCache.TryGetValue(a, out neg))
                return neg;

            neg = MkBDD(a.Ordinal, MkNot_rec(a.One), MkNot_rec(a.Zero));
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
                res = MkAnd(res, bdd);
            return res;
        }

        /// <summary>
        /// Intersect all sets in the array
        /// </summary>
        public BDD MkAnd(params BDD[] sets)
        {
            BDD res = True;
            foreach (BDD bdd in sets)
                res = MkAnd(res, bdd);
            return res;
        }

        /// <summary>
        /// Take the union of all sets in the enumeration
        /// </summary>
        public BDD MkOr(IEnumerable<BDD> sets)
        {
            BDD res = False;
            foreach (BDD bdd in sets)
                res = MkOr(res, bdd);
            return res;
        }

        /// <summary>
        /// Gets the full set.
        /// </summary>
        public BDD True
        {
            get { return BDD.True; }
        }

        /// <summary>
        /// Gets the empty set.
        /// </summary>
        public BDD False
        {
            get { return BDD.False; }
        }

        /// <summary>
        /// Returns true if the set is nonempty.
        /// </summary>
        public bool IsSatisfiable(BDD set)
        {
            return set != False;
        }

        /// <summary>
        /// Returns true if a and b represent equivalent BDDs.
        /// </summary>
        public bool AreEquivalent(BDD a, BDD b)
        {
            return MkXOr(a, b) == False;
        }

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

            var key = new BoolOpKey(BoolOp.XOR, a, b);

            BDD res;
            if (_binOpCache.TryGetValue(key, out res))
                return res;

            return MkBoolOP_lock(key);
        }

        #region bit-shift operations

        /// <summary>
        /// Shift all elements k (=1 by default) bits to the right.
        /// For example if set denotes {*0000,*1110,*1111} then
        /// ShiftRight(set) denotes {*000,*111} where * denotes any prefix of 0's or 1's.
        /// </summary>
        public BDD ShiftRight(BDD set, int k = 1)
        {
            if (k < 0)
                throw new AutomataException(AutomataExceptionKind.InvalidArgument);
            if (set.IsLeaf || k == 0)
                return set;
            return Shift_lock(set, 0 - k);
        }

        /// <summary>
        /// Shift all elements k bits to the left.
        /// For example if k=1 and set denotes {*0000,*1111} then
        /// ShiftLeft(set) denotes {*00000,*00001,*11110,*11111} where * denotes any prefix of 0's or 1's.
        /// </summary>
        public BDD ShiftLeft(BDD set, int k = 1)
        {
            if (k < 0)
                throw new AutomataException(AutomataExceptionKind.InvalidArgument);
            if (set.IsLeaf || k == 0)
                return set;
            return Shift_lock(set, k);
        }

        /// <summary>
        /// Allow shift_lock only single thread at a time because _bddCache is updated.
        /// </summary>
        private BDD Shift_lock(BDD set, int k)
        {
            lock (this)
            {
                return Shift_rec(new Dictionary<BDD_Int, BDD>(), set, k);
            }
        }

        /// <summary>
        /// Uses shiftCache to avoid recomputations in shared BDDs (DAGs).
        /// Is executed in a single thread mode.
        /// </summary>
        private BDD Shift_rec(Dictionary<BDD_Int, BDD> shiftCache, BDD set, int k)
        {
            if (set.IsLeaf || k == 0)
                return set;

            int ordinal = set.Ordinal + k;

            if (ordinal < 0)
                return True;  //this arises if k is negative

            var key = new BDD_Int(set, k);

            BDD res;
            if (shiftCache.TryGetValue(key, out res))
                return res;
            else
            {
                //make sure another thread hasn't meanwhile alreday done this
                if (shiftCache.TryGetValue(key, out res))
                    return res;

                BDD zero = Shift_rec(shiftCache, set.Zero, k);
                BDD one = Shift_rec(shiftCache, set.One, k);

                if (zero == one)
                    res = zero;
                else
                    res = MkBDD(ordinal, one, zero);

                shiftCache[key] = res;
                return res;
            }
        }

        #endregion

        #region Minterm generation

        public IEnumerable<Tuple<bool[], BDD>> GenerateMinterms(params BDD[] sets)
        {
            return _mintermGen.GenerateMinterms(sets);
        }

        #endregion

        /// <summary>
        /// Make a set containing all integers whose bits up to maxBit equal n.
        /// </summary>
        /// <param name="n">the given integer</param>
        /// <param name="maxBit">bits above maxBit are unspecified</param>
        /// <returns></returns>
        public BDD MkSetFrom(uint n, int maxBit)
        {
            var cs = MkSetFromRange(n, n, maxBit);
            return cs;
        }

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
                    m = m & filter;
                    n = n & filter;
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
                if (n == 0)  //implies that m==0
                    return MkBDD(bit, False, True);
                else if (m == 1) //implies that n==1
                    return MkBDD(bit, True, False);
                else //m=0 and n=1, thus full range from 0 to ((mask << 1)-1)
                    return True;
            }
            else if (m == 0 && n == ((mask << 1) - 1)) //full interval
            {
                return True;
            }
            else //mask > 1, i.e., mask = 2^b for some b > 0, and not full interval
            {
                //e.g. m = x41 = 100 0001, n = x59 = 101 1001, mask = x40 = 100 0000, ord = 6 = log2(b)
                uint mb = m & mask; // e.g. mb = b
                uint nb = n & mask; // e.g. nb = b

                if (nb == 0) // implies that 1-branch is empty
                {
                    var fcase = CreateFromInterval_rec(mask >> 1, bit - 1, m, n);
                    return MkBDD(bit, False, fcase);
                }
                else if (mb == mask) // implies that 0-branch is empty
                {
                    var tcase = CreateFromInterval_rec(mask >> 1, bit - 1, m & ~mask, n & ~mask);
                    return MkBDD(bit, tcase, False);
                }
                else //split the interval in two
                {
                    var fcase = CreateFromInterval_rec(mask >> 1, bit - 1, m, mask - 1);
                    var tcase = CreateFromInterval_rec(mask >> 1, bit - 1, 0, n & ~mask);
                    return MkBDD(bit, tcase, fcase);
                }
            }
        }

        //private BDD CreateFromInterval1(ulong mask, int bit, ulong m, ulong n)
        //{
        //    BDD set;
        //    var pair = new Tuple<ulong, ulong>(m, n);
        //    var key = new Tuple<int, Tuple<ulong, ulong>>(bit, pair);

        //    if (intervalCache.TryGetValue(key, out set))
        //        return set;

        //    else
        //    {

        //        if (mask == 1) //base case: LSB
        //        {
        //            if (n == 0)  //implies that m==0
        //                set = MkBvSet(bit, False, True);
        //            else if (m == 1) //implies that n==1
        //                set = MkBvSet(bit, True, False);
        //            else //m=0 and n=1, thus full range from 0 to ((mask << 1)-1)
        //                set = True;
        //        }
        //        else if (m == 0 && n == ((mask << 1) - 1)) //full interval
        //        {
        //            set = True;
        //        }
        //        else //mask > 1, i.e., mask = 2^b for some b > 0, and not full interval
        //        {
        //            //e.g. m = x41 = 100 0001, n = x59 = 101 1001, mask = x40 = 100 0000, ord = 6 = log2(b)
        //            ulong mb = m & mask; // e.g. mb = b
        //            ulong nb = n & mask; // e.g. nb = b

        //            if (nb == 0) // implies that 1-branch is empty
        //            {
        //                var fcase = CreateFromInterval1(mask >> 1, bit - 1, m, n);
        //                set = MkBvSet(bit, False, fcase);
        //            }
        //            else if (mb == mask) // implies that 0-branch is empty
        //            {
        //                var tcase = CreateFromInterval1(mask >> 1, bit - 1, m & ~mask, n & ~mask);
        //                set = MkBvSet(bit, tcase, False);
        //            }
        //            else //split the interval in two
        //            {
        //                var fcase = CreateFromInterval1(mask >> 1, bit - 1, m, mask - 1);
        //                var tcase = CreateFromInterval1(mask >> 1, bit - 1, 0, n & ~mask);
        //                set = MkBvSet(bit, tcase, fcase);
        //            }
        //        }
        //        intervalCache[key] = set;
        //        return set;
        //    }
        //}

        /// <summary>
        /// Convert the set into an equivalent array of uint ranges.
        /// Bits above maxBit are ignored.
        /// The ranges are nonoverlapping and ordered.
        /// If limit > 0 and there are more ranges than limit then return null.
        /// </summary>
        public Tuple<uint, uint>[] ToRanges(BDD set, int maxBit, int limit = 0)
        {
            var rc = new RangeConverter();
            var ranges = rc.ToRanges(set, maxBit);
            if (limit == 0 || ranges.Length <= limit)
                return ranges;
            else
                return null;
        }

        #region domain size and min computation

        /// <summary>
        /// Calculate the number of elements in the set. Returns 0 when set is full and maxBit is 63.
        /// </summary>
        /// <param name="set">the given set</param>
        /// <param name="maxBit">bits above maxBit are ignored</param>
        /// <returns>the cardinality of the set</returns>
        public ulong ComputeDomainSize(BDD set, int maxBit)
        {
            if (maxBit < set.Ordinal)
                throw new AutomataException(AutomataExceptionKind.InvalidArguments);

            if (set == False)
                return 0UL;
            else if (set == True)
            {
                //e.g if maxBit is 15 then the return value is 1 << 16, i.e., 2^16
                return ((1UL << maxBit) << 1);
            }
            else
            {
                var res = CalculateCardinality1(new Dictionary<BDD, ulong>(), set);
                if (maxBit > set.Ordinal)
                {
                    res = (1UL << (maxBit - set.Ordinal)) * res;
                }
                return res;
            }
        }

        /// <summary>
        /// Caches previously calculated values in sizeCache so that computations are not repeated inside a BDD for the same sub-BDD.
        /// Thus the number of internal calls is propotional to the number of nodes of the BDD, that could otherwise be exponential in the worst case.
        /// The size cache cused to be a static field but the current way makes it thread-safe without use of locks.
        /// </summary>
        /// <param name="sizeCache">previously computed sizes</param>
        /// <param name="set">given set to compute size of</param>
        /// <returns></returns>
        private ulong CalculateCardinality1(Dictionary<BDD, ulong> sizeCache, BDD set)
        {
            ulong size;
            if (sizeCache.TryGetValue(set, out size))
                return size;

            ulong sizeL;
            ulong sizeR;
            if (set.Zero.IsEmpty)
            {
                sizeL = 0;
                if (set.One.IsFull)
                {
                    sizeR = ((uint)1 << set.Ordinal);
                }
                else
                {
                    sizeR = ((uint)1 << (((set.Ordinal - 1) - set.One.Ordinal))) * CalculateCardinality1(sizeCache, set.One);
                }
            }
            else if (set.Zero.IsFull)
            {
                sizeL = (1UL << set.Ordinal);
                if (set.One.IsEmpty)
                {
                    sizeR = 0UL;
                }
                else
                {
                    sizeR = (1UL << (((set.Ordinal - 1) - set.One.Ordinal))) * CalculateCardinality1(sizeCache, set.One);
                }
            }
            else
            {
                sizeL = (1UL << (((set.Ordinal - 1) - set.Zero.Ordinal))) * CalculateCardinality1(sizeCache, set.Zero);
                if (set.One == False)
                {
                    sizeR = 0UL;
                }
                else if (set.One == True)
                {
                    sizeR = (1UL << set.Ordinal);
                }
                else
                {
                    sizeR = (1UL << (((set.Ordinal - 1) - set.One.Ordinal))) * CalculateCardinality1(sizeCache, set.One);
                }
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
        public ulong GetMin(BDD set)
        {
            return set.GetMin();
        }

        #endregion

        //private BDD ProjectBit_(BDD bdd, int bit, Dictionary<BDD, BDD> cache)
        //{
        //    BDD res;
        //    if (!cache.TryGetValue(bdd, out res))
        //    {
        //        if (bdd.IsLeaf || bdd.Ordinal < bit)
        //            res = bdd;
        //        else if (bdd.Ordinal == bit)
        //            res = MkOr(bdd.One, bdd.Zero);
        //        else
        //        {
        //            var bdd1 = ProjectBit_(bdd.One, bit, cache);
        //            var bdd0 = ProjectBit_(bdd.Zero, bit, cache);
        //            res = MkBvSet(bdd.Ordinal, bdd1, bdd0);
        //        }
        //        cache[bdd] = res;
        //    }
        //    return res;
        //}

        /// <summary>
        /// Any two BDDs that are equivalent are isomorphic and have the same hashcode.
        /// </summary>
        public bool HashCodesRespectEquivalence
        {
            get { return true; }
        }

        /// <summary>
        /// Two equuivalent BDDs need not be identical
        /// </summary>
        public bool IsExtensional
        {
            get { return false; }
        }

        #region Serialializing and deserializing BDDs from dags encoded by ulongs arrays
        /// <summary>
        /// Serialize a BDD in a flat ulong array.
        /// The BDD may have at most 2^16 bits and 2^24 nodes.
        /// BDD.False is represented by ulong[]{0}.
        /// BDD.True is represented by ulong[]{0,0}.
        /// Element at index 0 is the false node,
        /// element at index 1 is the true node,
        /// and entry at index i>1 is node i and has the structure:
        /// (ordinal &lt;&lt; 48) | (trueNode &lt;&lt; 24) | falseNode.
        /// The root of the BDD (when different from True and False) is node 2.
        /// </summary>
        public ulong[] Serialize(BDD bdd)
        {
            if (bdd.IsEmpty)
                return new ulong[] { 0 };
            if (bdd.IsFull)
                return new ulong[] { 0, 0 };
            if (bdd.IsLeaf)
                throw new AutomataException(AutomataExceptionKind.MTBDDsNotSupportedForThisOperation);

            int nrOfNodes = bdd.CountNodes();

            if (nrOfNodes > (1 << 24))
                throw new AutomataException(AutomataExceptionKind.BDDSerializationNodeLimitViolation);

            if (bdd.Ordinal >= (1 << 16))
                throw new AutomataException(AutomataExceptionKind.BDDSerializationBitLimitViolation);

            ulong[] res = new ulong[nrOfNodes];

            //here we know that bdd is neither empty nor full
            var done = new Dictionary<BDD, int>();
            done[False] = 0;
            done[True] = 1;

            Stack<BDD> stack = new Stack<BDD>();
            stack.Push(bdd);
            done[bdd] = 2;

            int doneCount = 3;

            while (stack.Count > 0)
            {
                BDD b = stack.Pop();
                if (!done.ContainsKey(b.One))
                {
                    done[b.One] = (doneCount++);
                    stack.Push(b.One);
                }
                if (!done.ContainsKey(b.Zero))
                {
                    done[b.Zero] = (doneCount++);
                    stack.Push(b.Zero);
                }
                int bId = done[b];
                int fId = done[b.Zero];
                int tId = done[b.One];

                res[bId] = (((ulong)b.Ordinal) << 48) | (((ulong)tId) << 24) | ((uint)fId);
            }
            return res;
        }

        /// <summary>
        /// Recreates a BDD from a ulong array that has been created using Serialize.
        /// Is executed in a single thread mode.
        /// </summary>
        public BDD Deserialize(ulong[] arcs)
        {
            lock (this)
            {
                if (arcs.Length == 1)
                    return False;
                if (arcs.Length == 2)
                    return True;

                //organized by order
                var levelsMap = new Dictionary<int, List<int>>();
                List<int> levels = new List<int>();

                BDD[] bddMap = new BDD[arcs.Length];
                bddMap[0] = False;
                bddMap[1] = True;

                for (int i = 2; i < arcs.Length; i++)
                {
                    ulong ordinal = (arcs[i] >> 48);
                    int x = (int)ordinal;
                    List<int> x_list;
                    if (!levelsMap.TryGetValue(x, out x_list))
                    {
                        x_list = new List<int>();
                        levelsMap[x] = x_list;
                        levels.Add(x);
                    }
                    x_list.Add(i);
                }

                //create the BDD nodes according to the level order
                //strating with the lowest ordinal
                //this is to ensure proper internalization
                levels.Sort();

                foreach (int x in levels)
                {
                    foreach (int i in levelsMap[x])
                    {
                        ulong oneU = (arcs[i] >> 24) & 0xFFFFFF;
                        int one = (int)oneU;
                        ulong zeroU = arcs[i] & 0xFFFFFF;
                        int zero = (int)zeroU;
                        if (one >= bddMap.Length || zero >= bddMap.Length)
                            throw new AutomataException(AutomataExceptionKind.BDDDeserializationError);
                        var oneBranch = bddMap[one];
                        var zeroBranch = bddMap[zero];
                        var bdd = MkBDD(x, oneBranch, zeroBranch);
                        bddMap[i] = bdd;
                        if (bdd.Ordinal <= bdd.One.Ordinal || bdd.Ordinal <= bdd.Zero.Ordinal)
                            throw new AutomataException(AutomataExceptionKind.BDDDeserializationError);
                    }
                }

                return bddMap[2];
            }
        }

        public abstract string SerializePredicate(BDD s);
        public abstract BDD DeserializePredicate(string s);
        #endregion
    }
}
