// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.Symbolic
{
    /// <summary>Classifies characters into partition block ids.</summary>
    internal sealed class PartitionClassifier
    {
        private readonly int[] _precomputed;
        private readonly BDD _mtbdd;

        /// <summary>This value can be tuned for efficiency to control how many input character partition ids are precomputed, default value is ASCII</summary>
        private const int PrecomputeCount = 128;

        private PartitionClassifier(int[] precomputed, BDD mtbdd)
        {
            _precomputed = precomputed;
            _mtbdd = mtbdd;
        }

        /// <summary>Create a classifier that maps a character into a partion block id.</summary>
        /// <param name="solver">Character algebra</param>
        /// <param name="partition">Partition of the whole set of all characters into pairwise disjoint nonempty parts</param>
        internal static PartitionClassifier Create(CharSetSolver solver, BDD[] partition)
        {
            if (partition.Length == 1)
            {
                // Partition is trivial: everything maps to one id 0, replace True with MTBDD terminal 0
                return new PartitionClassifier(Array.Empty<int>(), solver.ReplaceTrue(BDD.True, 0));
            }

            BDD mtbdd = BDD.False;
            for (int i = 0; i < partition.Length; i++)
            {
                // Turn the i'th BDD into MTBDD with True replaced by i
                BDD part_i = solver.ReplaceTrue(partition[i], i);

                // Make union of part_i with the mtbdd created so far
                // IMPORTANT: the folowing works correctly because 2 elements can never never map to different ids in the updated mtbdd
                // due to the partition -- every element belongs to EXACTLY one part of the partition
                mtbdd = solver.Or(mtbdd, part_i);
            }

            // Precompute all the entries below PrecomputeCount.
            // Observe that all entries will return nonnegative terminal ids -- again, because of the partition.
            var precomp = new int[PrecomputeCount];
            for (int i = 0; i < precomp.Length; i++)
            {
                precomp[i] = mtbdd.Find(i);
            }

            BDD filter =
                PrecomputeCount == 128 ? solver._nonascii :
                PrecomputeCount > char.MaxValue ? BDD.False :
                solver.CreateCharSetFromRange((char)PrecomputeCount, '\uFFFF');

            // Apply the filter to the mtbdd because the precomputed characters will never be applied
            // -- the 'Find' method of mtbdd is never called for any value below precomputeCount ---.
            // This reduces the size of the needed mtbdd, although it would be correct to keep mtbdd "as is"
            // i.e. this step is optional, it will not affect correctness if the following line is commented out.
            // However, it will increase the number of nodes in (size of) the mtbdd and also its serialization size
            // essentially --- this removes irrelevant characters.
            // IMPORTANT: this works because of the semantics of MTBDD And-operation
            mtbdd = solver.And(mtbdd, filter);

            // Further small otimization: check if the mtbdd maps all elements to the same terminal.
            // This happens e.g. when all the relevant input is really over ASCII and all non-ASCII maps to another fixed therminal
            // then use that terminal instead of the whole mtbdd.
            return new PartitionClassifier(precomp, mtbdd.IsEssentiallyBoolean(out BDD? the_terminal) ? the_terminal : mtbdd);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Find(int c)
        {
            int[] precomputed = _precomputed;
            return (uint)c < (uint)precomputed.Length ? precomputed[c] : _mtbdd.Find(c);
        }

        #region serialization
        /// <summary>Appends a string in [0-9A-Za-z/+.-,]* to sb.</summary>
        public void Serialize(StringBuilder sb)
        {
            // This encoding does not use ','
            Base64Utility.Encode(_precomputed, sb);
            // Separate the precomputed serialization from the BDD serialization using ','
            sb.Append(',');
            // This encoding does not use ','
            _mtbdd.Serialize(sb);
        }

        /// <summary>Deserializes the classifier from the string s created by Serialize.</summary>
        public static PartitionClassifier Deserialize(ReadOnlySpan<char> input, BDDAlgebra? algebra = null)
        {
            int firstEnd = input.IndexOf(',');
            if (firstEnd == -1 || input.Slice(firstEnd + 1).IndexOf(',') != -1)
            {
                throw new ArgumentOutOfRangeException(nameof(input));
            }

            int[] precomp = Base64Utility.DecodeInt32Array(input[..firstEnd]);
            BDD bst = BDD.Deserialize(input.Slice(firstEnd + 1), algebra);
            return new PartitionClassifier(precomp, bst);
        }

        /// <summary>Returns the serialized format of the classifier for Debugging.</summary>
        public override string ToString()
        {
            var sb = new StringBuilder();
            Serialize(sb);
            return sb.ToString();
        }
        #endregion
    }
}
