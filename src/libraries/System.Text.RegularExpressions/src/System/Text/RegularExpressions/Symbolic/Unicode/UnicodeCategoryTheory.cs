// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Globalization;

namespace System.Text.RegularExpressions.Symbolic.Unicode
{
    internal sealed class UnicodeCategoryTheory<TPredicate> where TPredicate : class
    {
        internal readonly ICharAlgebra<TPredicate> _solver;
        private readonly TPredicate[] _catConditions = new TPredicate[30];

        private TPredicate? _whiteSpaceCondition;
        private TPredicate? _wordLetterCondition;

        public UnicodeCategoryTheory(ICharAlgebra<TPredicate> solver) => _solver = solver;

        public static string UnicodeCategoryPredicateName(int cat) => $"Is{(UnicodeCategory)cat}";

        public TPredicate CategoryCondition(int i)
        {
            if (_catConditions[i] is not TPredicate condition)
            {
                BDD bdd = BDD.Deserialize(UnicodeCategoryRanges.AllCategoriesSerializedBDD[i], _solver.CharSetProvider);
                _catConditions[i] = condition = _solver.ConvertFromCharSet(_solver.CharSetProvider, bdd);
                ValidateSerialization(condition);
            }

            return condition;
        }

        public TPredicate WhiteSpaceCondition
        {
            get
            {
                if (_whiteSpaceCondition is not TPredicate condition)
                {
                    BDD bdd = BDD.Deserialize(UnicodeCategoryRanges.WhitespaceSerializedBDD, _solver.CharSetProvider);
                    _whiteSpaceCondition = condition = _solver.ConvertFromCharSet(_solver.CharSetProvider, bdd);
                    ValidateSerialization(condition);
                }

                return condition;
            }
        }

        public TPredicate WordLetterCondition
        {
            get
            {
                if (_wordLetterCondition is not TPredicate condition)
                {
                    BDD bdd = BDD.Deserialize(UnicodeCategoryRanges.WordCharactersSerializedBDD, _solver.CharSetProvider);
                    _wordLetterCondition = condition = _solver.ConvertFromCharSet(_solver.CharSetProvider, bdd);
                    ValidateSerialization(condition);
                }

                return condition;
            }
        }

        /// <summary>Validate correctness of serialization/deserialization for the given predicate.</summary>
        [Conditional("DEBUG")]
        private void ValidateSerialization(TPredicate pred)
        {
            var sb = new StringBuilder();
            _solver.SerializePredicate(pred, sb);
            TPredicate psi = _solver.DeserializePredicate(sb.ToString());
            Debug.Assert(pred.Equals(psi));
        }
    }
}
