// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System.Text.RegularExpressions.SRM.Unicode
{
    internal sealed class UnicodeCategoryTheory<PRED>
    {
        internal readonly ICharAlgebra<PRED> _solver;
        private readonly PRED[] _catConditions = new PRED[30];
        private PRED _whiteSpaceCondition;
        private PRED _wordLetterCondition;

        public UnicodeCategoryTheory(ICharAlgebra<PRED> solver) => _solver = solver;

        public static string UnicodeCategoryPredicateName(int cat) => $"Is{(Globalization.UnicodeCategory)cat}";

        #region IUnicodeCategoryTheory<Expr> Members

        public PRED CategoryCondition(int i)
        {
            if (Equals(_catConditions[i], default(PRED))) //uninitialized
            {
                BDD cat_i = BDD.Deserialize(UnicodeCategoryRanges.s_UnicodeCategoryBdd_repr[i], _solver.CharSetProvider);
                _catConditions[i] = _solver.ConvertFromCharSet(_solver.CharSetProvider, cat_i);
                ValidateSerialization(_catConditions[i]);
            }

            return _catConditions[i];
        }

        public PRED WhiteSpaceCondition
        {
            get
            {
                if (Equals(_whiteSpaceCondition, default(PRED)))
                {
                    BDD sBDD = BDD.Deserialize(UnicodeCategoryRanges.s_UnicodeWhitespaceBdd_repr, _solver.CharSetProvider);
                    _whiteSpaceCondition = _solver.ConvertFromCharSet(_solver.CharSetProvider, sBDD);
                    ValidateSerialization(_whiteSpaceCondition);
                }

                return _whiteSpaceCondition;
            }
        }

        public PRED WordLetterCondition
        {
            get
            {
                if (Equals(_wordLetterCondition, default(PRED)))
                {
                    BDD wBDD = BDD.Deserialize(UnicodeCategoryRanges.s_UnicodeWordCharacterBdd_repr, _solver.CharSetProvider);
                    _wordLetterCondition = _solver.ConvertFromCharSet(_solver.CharSetProvider, wBDD);
                    ValidateSerialization(_wordLetterCondition);
                }

                return _wordLetterCondition;
            }
        }

        /// <summary>
        /// Validate correctness of serialization/deserialization for the given predicate
        /// </summary>
        [Conditional("DEBUG")]
        private void ValidateSerialization(PRED pred)
        {
            var sb = new StringBuilder();
            _solver.SerializePredicate(pred, sb);
            PRED psi = _solver.DeserializePredicate(sb.ToString());
            if (!pred.Equals(psi))
            {
                throw new AutomataException(AutomataExceptionKind.BDDDeserializationError);
            }
        }

        #endregion
    }
}
