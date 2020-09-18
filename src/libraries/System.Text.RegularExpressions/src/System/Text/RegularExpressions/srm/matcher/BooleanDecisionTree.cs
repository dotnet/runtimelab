using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace Microsoft.SRM
{
    /// <summary>
    /// Decision tree for mapping character ranges into corresponding partition block ids
    /// </summary>
    [Serializable]
    internal class BooleanDecisionTree : ISerializable
    {
        [NonSerialized]
        internal bool[] precomputed;
        [NonSerialized]
        internal DecisionTree.BST bst;

        internal BooleanDecisionTree(bool[] precomputed, DecisionTree.BST bst)
        {
            this.precomputed = precomputed;
            this.bst = bst;
        }

        /// <summary>
        /// Crteate a Boolean decision tree.
        /// References to solver and domain are not saved in the resulting decision tree.
        /// </summary>
        /// <param name="solver">character alberbra</param>
        /// <param name="domain">elements that map to true</param>
        /// <param name="precomputeLimit">upper limit for block ids for characters to be precomputed in an array (default is 0xFF, i.e. extended ASCII)</param>
        /// <returns></returns>
        internal static BooleanDecisionTree Create(CharSetSolver solver, BDD domain, ushort precomputeLimit = 0xFF)
        {
            BDD domain_compl = solver.MkNot(domain);
            var partition = new BDD[] { domain_compl, domain };
            if (precomputeLimit == 0)
            {
                return new BooleanDecisionTree(Array.Empty<bool>(), MkBST(new DecisionTree.PartitionCut(solver, partition), 0, 0xFFFF));
            }

            bool[] precomp = Precompute(solver, domain, precomputeLimit);
            DecisionTree.BST bst = null;
            if (precomputeLimit < ushort.MaxValue)
                bst = MkBST(new DecisionTree.PartitionCut(solver, partition), precomputeLimit + 1, ushort.MaxValue);

            return new BooleanDecisionTree(precomp, bst);
        }

        private static bool[] Precompute(CharSetSolver solver, BDD domain, int precomputeLimit)
        {
            bool[] precomp = new bool[precomputeLimit + 1];
            Func<int, bool> F = i =>
            {
                var bdd = solver.MkCharConstraint((char)i);
                if (solver.IsSatisfiable(solver.MkAnd(bdd, domain)))
                    return true;
                else
                    return false;
            };
            for (int c = 0; c <= precomputeLimit; c++)
            {
                precomp[c] = F(c);
            }
            return precomp;
        }

        private static DecisionTree.BST MkBST(DecisionTree.PartitionCut partition, int from, int to)
        {
            var cut = partition.Cut(from, to);
            if (cut.IsEmpty)
                return null;
            else
            {
                int block_id = cut.GetSigletonId();
                if (block_id >= 0)
                    //there is precisely one block remaining
                    return new DecisionTree.BST(block_id, null, null);
                else
                {
                    //it must be that 'from < to'
                    //or else there could only have been one block
                    int mid = (from + to) / 2;
                    var left = MkBST(cut, from, mid);
                    var right = MkBST(cut, mid + 1, to);
                    //it must be that either left != null or right != null
                    if (left == null)
                        return right;
                    else if (right == null)
                        return left;
                    else
                        return new DecisionTree.BST(mid + 1, left, right);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(ushort c)
        {
            return (c < precomputed.Length ? precomputed[c] : bst.Find(c) == 1);
        }

        #region serialization
        /// <summary>
        /// Serialize
        /// </summary>
        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue("p", SerializePrecomputed());
            info.AddValue("b", bst.Serialize());
        }
        /// <summary>
        /// Deserialize
        /// </summary>
        public BooleanDecisionTree(SerializationInfo info, StreamingContext context)
        {
            precomputed = DeserializePrecomputed(info.GetString("p"));
            this.bst = DecisionTree.BST.Deserialize(info.GetString("b"));
        }

        private string SerializePrecomputed()
        {
            char[] chars = Array.ConvertAll(precomputed, b => (b ? '1' : '0'));
            var s = new string(chars);
            return s;
        }

        private static bool[] DeserializePrecomputed(string s)
        {
            var vals = Array.ConvertAll(s.ToCharArray(), c => (c == '1' ? true : false));
            return vals;
        }
        #endregion
    }
}
