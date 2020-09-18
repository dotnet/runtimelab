using System;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Text;

namespace Microsoft.SRM
{
    /// <summary>
    /// Decision tree for mapping character ranges into corresponding partition block ids
    /// </summary>
    [Serializable]
    internal class DecisionTree : ISerializable
    {
        [NonSerialized]
        internal int[] precomputed;
        [NonSerialized]
        internal BST bst;

        internal BST Tree
        {
            get
            {
                return bst;
            }
        }

        public DecisionTree(int[] precomputed, BST bst)
        {
            this.precomputed = precomputed;
            this.bst = bst;
        }

        /// <summary>
        /// Crteate a decision tree that maps a character into a partion block id
        /// </summary>
        /// <param name="solver">character alberbra</param>
        /// <param name="partition">partition of the whole set of all characters into pairwise disjoint nonempty sets</param>
        /// <param name="precomputeLimit">upper limit for block ids for characters to be precomputed in an array (default is 0xFF, i.e. extended ASCII)</param>
        /// <returns></returns>
        internal static DecisionTree Create(CharSetSolver solver, BDD[] partition, ushort precomputeLimit = 0xFF)
        {
            if (partition.Length == 1)
                //there is no actual partition, everything maps to one id 0, e.g. as in .*
                return new DecisionTree(new int[(int)precomputeLimit], new BST(0, null, null));

            if (precomputeLimit == 0)
                return new DecisionTree(new int[] { }, MkBST(new PartitionCut(solver, partition), 0, 0xFFFF));

            int[] precomp = Precompute(solver, partition, precomputeLimit);
            BST bst = null;
            if (precomputeLimit < ushort.MaxValue)
                bst = MkBST(new PartitionCut(solver, partition), precomputeLimit + 1, ushort.MaxValue);

            return new DecisionTree(precomp, bst);
        }

        private static int[] Precompute(CharSetSolver solver, BDD[] partition, int precomputeLimit)
        {
            int[] precomp = new int[precomputeLimit + 1];
            Func<int, int> GetPartitionId = i =>
            {
                for (int j = 0; j < partition.Length; j++)
                {
                    var i_bdd = solver.MkCharConstraint((char)i);
                    if (solver.IsSatisfiable(solver.MkAnd(i_bdd, partition[j])))
                    {
                        return j;
                    }
                }
                return -1;
            };
            for (int c = 0; c <= precomputeLimit; c++)
            {
                int id = GetPartitionId(c);
                if (id < 0)
                    throw new AutomataException(AutomataExceptionKind.InternalError);
                precomp[c] = id;
            }
            return precomp;
        }

        private static BST MkBST(PartitionCut partition, int from, int to)
        {
            var cut = partition.Cut(from, to);
            if (cut.IsEmpty)
                return null;
            else
            {
                int block_id = cut.GetSigletonId();
                if (block_id >= 0)
                    //there is precisely one block remaining
                    return new BST(block_id, null, null);
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
                        return new BST(mid + 1, left, right);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetId(ushort c)
        {
            if (c < precomputed.Length)
            {
                return precomputed[c];
            }
            else
            {
                return bst.Find(c);
            }
        }

        /// <summary>
        /// Used in the decision tree to locate minterm ids of nonascii characters
        /// </summary>
        public internal BST
        {
            //[NonSerialized]
            int node;
            //[NonSerialized]
            BST left;
            //[NonSerialized]
            BST right;

            internal BST Left
            {
                get
                {
                    return left;
                }
            }

            internal BST Right
            {
                get
                {
                    return right;
                }
            }

            internal bool IsLeaf
            {
                get
                {
                    return left == null;
                }
            }

            internal int Node
            {
                get
                {
                    return node;
                }
            }

            internal int Find(int charCode)
            {
                if (left == null)
                    return node; //return the leaf
                else if (charCode < node)
                    return left.Find(charCode);
                else
                    return right.Find(charCode);
            }

            public BST(int node, BST left, BST right)
            {
                this.node = node;
                this.left = left;
                this.right = right;
            }

            public override string ToString()
            {
                return this.Serialize();
            }

            #region custom serialization
            void SerializeHelper(StringBuilder sb)
            {
                if (IsLeaf)
                {
                    sb.Append(string.Format("{0}#", node));
                }
                else
                {
                    sb.Append("(");
                    sb.Append(node);
                    sb.Append(",");
                    left.SerializeHelper(sb);
                    sb.Append(",");
                    right.SerializeHelper(sb);
                    sb.Append(")");
                }
            }
            public string Serialize()
            {
                var sb = new StringBuilder();
                SerializeHelper(sb);
                return sb.ToString();
            }

            public static BST Deserialize(string s)
            {
                int tmp;
                var bst = DeserializeHelper(s, 0, out tmp);
                return bst;
            }

            static BST DeserializeHelper(string s, int i, out int next_i)
            {
                switch (s[i])
                {
                    case '(':
                        {
                            int j = s.IndexOf(',', i + 1);
                            int node = int.Parse(s.Substring(i + 1, j - (i + 1)));
                            int k;
                            var left = DeserializeHelper(s, j + 1, out k);
                            int m;
                            var right = DeserializeHelper(s, k + 1, out m);
                            next_i = m + 1;
                            return new BST(node, left, right);
                        }
                    default: //leaf l(node)
                        {
                            int j = s.IndexOf('#', i);
                            int node = int.Parse(s.Substring(i, j - i));
                            next_i = j + 1;
                            return new BST(node, null, null);
                        }
                }
            }
            #endregion
        }

        /// <summary>
        /// Represents a cut of the original partition wrt some interval
        /// </summary>
        internal class PartitionCut
        {
            BDD[] blocks;
            CharSetSolver solver;
            internal PartitionCut(CharSetSolver solver, BDD[] blocks)
            {
                this.blocks = blocks;
                this.solver = solver;
            }

            internal bool IsEmpty
            {
                get
                {
                    return Array.TrueForAll(blocks, b => b.IsEmpty);
                }
            }

            internal int GetSigletonId()
            {
                int id = -1;
                for (int i = 0; i < blocks.Length; i++)
                {
                    if (!blocks[i].IsEmpty)
                    {
                        if (id >= 0)
                            //there is more than one nonempty block
                            return -1;
                        else
                            id = i;
                    }
                }
                return id;
            }

            internal PartitionCut Cut(int lower, int upper)
            {
                var set = solver.MkCharSetFromRange((char)lower, (char)upper);
                var newblocks = Array.ConvertAll(blocks, b => solver.MkAnd(b, set));
                return new PartitionCut(solver, newblocks);
            }
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
        public DecisionTree(SerializationInfo info, StreamingContext context)
        {
            precomputed = DeserializePrecomputed(info.GetString("p"));
            bst = BST.Deserialize(info.GetString("b"));
        }

        string SerializePrecomputed()
        {
            string s = "";
            for (int i=0; i < precomputed.Length; i++)
            {
                if (i > 0)
                    s += ",";
                s += precomputed[i].ToString();
            }
            return s;
        }

        static int[] DeserializePrecomputed(string s)
        {
            var vals = Array.ConvertAll(s.Split(','), x => int.Parse(x));
            return vals;
        }
        #endregion
    }
}
