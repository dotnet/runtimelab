// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Classifies characters into true or false
    /// </summary>
    internal sealed class BooleanClassifier
    {
        //stores the remaining characters in a BDD
        private readonly BDD _nonascii;
        //explcit array for ascii
        private readonly bool[] _ascii;

        private BooleanClassifier(ulong lower, ulong upper, BDD bdd)
        {
            _ascii = new bool[128];
            for (int i = 0; i < 128; i++)
                _ascii[i] = i < 64 ? ((lower & ((ulong)1 << i)) != 0) : ((upper & ((ulong)1 << (i - 64))) != 0);
            _nonascii = bdd;
        }

        private BooleanClassifier(bool[] ascii, BDD bdd)
        {
            _ascii = ascii;
            _nonascii = bdd;
        }

        /// <summary>
        /// Create a Boolean classifier.
        /// </summary>
        /// <param name="solver">character algebra (the algebra is not stored in the classifier)</param>
        /// <param name="domain">elements that map to true</param>
        /// <returns></returns>
        internal static BooleanClassifier Create(CharSetSolver solver, BDD domain)
        {
            bool[] ascii = new bool[128];
            for (int i = 0; i < 128; i++)
                ascii[i] = domain.Contains(i);
            //remove the ASCII characters from the domain if the domain is not everything
            BDD bdd = domain.IsFull ? domain : solver.And(solver._nonascii, domain);
            return new BooleanClassifier(ascii, bdd);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(ushort c) => c < 128 ? _ascii[c] : _nonascii.Contains(c);

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
            Base64.Encode(lower, sb);
            sb.Append(',');
            Base64.Encode(upper, sb);
            sb.Append(',');
            _nonascii.Serialize(sb);
        }

        public static BooleanClassifier Deserialize(string input, BDDAlgebra solver = null)
        {
            int firstEnd = input.IndexOf(',');
            if (firstEnd >= 0)
            {
                int secondEnd = input.IndexOf(',', firstEnd + 1);
                if (secondEnd >= 0 && input.IndexOf(',', secondEnd + 1) == -1)
                {
                    ReadOnlySpan<char> s = input;
                    ulong lower = Base64.DecodeUInt64(s[..firstEnd]);
                    ulong upper = Base64.DecodeUInt64(s[(firstEnd + 1)..secondEnd]);
                    BDD bdd = BDD.Deserialize(s[(secondEnd + 1)..], solver);
                    return new BooleanClassifier(lower, upper, bdd);
                }
            }

            throw new ArgumentException($"{nameof(BooleanClassifier.Deserialize)} invalid '{nameof(input)}' parameter");
        }
        #endregion
    }
}
