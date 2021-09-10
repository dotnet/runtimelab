// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.Symbolic
{
    /// <summary>Classifies characters into true or false</summary>
    internal sealed class BooleanClassifier
    {
        /// <summary>Explicit array for ascii.</summary>
        private readonly bool[] _ascii;
        /// <summary>Stores the remaining characters in a BDD.</summary>
        private readonly BDD _nonAsciiBDD;

        private BooleanClassifier(ulong lower, ulong upper, BDD bdd)
        {
            var ascii = new bool[128];
            for (int i = 0; i < ascii.Length; i++)
            {
                ascii[i] = (i < 64 ? (lower & ((ulong)1 << i)) : (upper & ((ulong)1 << (i - 64)))) != 0;
            }

            _ascii = ascii;
            _nonAsciiBDD = bdd;
        }

        private BooleanClassifier(bool[] ascii, BDD bdd)
        {
            Debug.Assert(ascii.Length == 128);
            _ascii = ascii;
            _nonAsciiBDD = bdd;
        }

        /// <summary>Create a Boolean classifier.</summary>
        /// <param name="solver">character algebra (the algebra is not stored in the classifier)</param>
        /// <param name="domain">elements that map to true</param>
        internal static BooleanClassifier Create(CharSetSolver solver, BDD domain)
        {
            var ascii = new bool[128];
            for (int i = 0; i < ascii.Length; i++)
            {
                ascii[i] = domain.Contains(i);
            }

            // Remove the ASCII characters from the domain if the domain is not everything
            BDD bdd = domain.IsFull ?
                domain :
                solver.And(solver._nonascii, domain);

            return new BooleanClassifier(ascii, bdd);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(char c)
        {
            bool[] ascii = _ascii;
            return c < ascii.Length ? ascii[c] : _nonAsciiBDD.Contains(c);
        }

        #region Serialization
        public void Serialize(StringBuilder sb)
        {
            ulong lower = 0;
            for (int i = 0; i < 64; i++)
            {
                lower |= _ascii[i] ? (ulong)1 << i : 0;
            }

            ulong upper = 0;
            for (int i = 0; i < 64; i++)
            {
                upper |= _ascii[i + 64] ? (ulong)1 << i : 0;
            }

            //use comma to separate the elements, comma is not used in _bdd.Serialize
            Base64Utility.Encode(lower, sb);
            sb.Append(',');
            Base64Utility.Encode(upper, sb);
            sb.Append(',');
            _nonAsciiBDD.Serialize(sb);
        }

        public static BooleanClassifier Deserialize(string input, BDDAlgebra? solver = null)
        {
            int firstEnd = input.IndexOf(',');
            if (firstEnd >= 0)
            {
                int secondEnd = input.IndexOf(',', firstEnd + 1);
                if (secondEnd >= 0 && input.IndexOf(',', secondEnd + 1) == -1)
                {
                    ReadOnlySpan<char> s = input;
                    ulong lower = Base64Utility.DecodeUInt64(s[..firstEnd]);
                    ulong upper = Base64Utility.DecodeUInt64(s[(firstEnd + 1)..secondEnd]);
                    BDD bdd = BDD.Deserialize(s[(secondEnd + 1)..], solver);
                    return new BooleanClassifier(lower, upper, bdd);
                }
            }

            throw new ArgumentOutOfRangeException(nameof(input));
        }
        #endregion
    }
}
