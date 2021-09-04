// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.RegularExpressions.SRM.Unicode
{
    internal sealed class IgnoreCaseTransformer
    {
        private const char Turkish_I_WithDot = '\u0130';
        private const char Turkish_i_WithoutDot = '\u0131';
        private const char KelvinSign = '\u212A';

        private readonly CharSetSolver _solver;
        private readonly BDD _i_Invariant;
        private readonly BDD _i_Default;
        private readonly BDD _i_Turkish;
        private readonly BDD _I_Turkish;

        private BDD _ignoreCaseRel_Default;
        private BDD _ignoreCaseRel_Default_Domain;

        private BDD _ignoreCaseRel_Invariant;
        private BDD _ignoreCaseRel_Invariant_Domain;

        private BDD _ignoreCaseRel_Turkish;
        private BDD _ignoreCaseRel_Turkish_Domain;

        /// <summary>Maps each char c to the case-insensitive set of c that is culture-independent (for non-null entries).</summary>
        private readonly BDD[] _char_table_CI = new BDD[0x10000];

        public IgnoreCaseTransformer(CharSetSolver solver)
        {
            _solver = solver;
            _i_Invariant = solver.Or(_solver.CharConstraint('i'), solver.CharConstraint('I'));
            _i_Default = solver.Or(_i_Invariant, solver.CharConstraint(Turkish_I_WithDot));
            _i_Turkish = solver.Or(solver.CharConstraint('i'), solver.CharConstraint(Turkish_I_WithDot));
            _I_Turkish = solver.Or(solver.CharConstraint('I'), solver.CharConstraint(Turkish_i_WithoutDot));
        }

        private void SetUpDefault()
        {
            if (_ignoreCaseRel_Default == null)
            {
                // Deserialize the table for the default culture.
                _ignoreCaseRel_Default = BDD.Deserialize(IgnoreCaseRelation.IgnoreCaseEnUsSerializedBDD, _solver);

                // Represents the set of all case-sensitive characters in the default culture.
                _ignoreCaseRel_Default_Domain = _solver.ShiftRight(_ignoreCaseRel_Default, 16);
            }
        }

        /// <summary>
        /// Gets the correct transformation relation based on the current culture;
        /// culture=="" means InvariantCulture while culture==null means to use the current culture.
        /// </summary>
        private BDD GetIgnoreCaseRel(out BDD domain, string culture = null)
        {
            culture ??= Globalization.CultureInfo.CurrentCulture.Name;

            if (culture == string.Empty)
            {
                if (_ignoreCaseRel_Invariant == null)
                {
                    SetUpDefault();

                    // Compute the invariant table based off of default.
                    // In the default (en-US) culture: Turkish_I_withDot = i = I
                    // In the invariant culture: i = I, while Turkish_I_withDot is case-insensitive
                    BDD tr_I_withdot_BDD = _solver.CharConstraint(Turkish_I_WithDot);
                    BDD i_BDD = _solver.CharConstraint('i');
                    BDD I_BDD = _solver.CharConstraint('I');

                    // Since Turkish_I_withDot is case-insensitive in invariant culture, remove it from the default (en-US culture) table.
                    BDD inv_table = _solver.And(_ignoreCaseRel_Default, _solver.Not(tr_I_withdot_BDD));

                    // Next, remove Turkish_I_withDot from the RHS of the relation.
                    // This also effectively removes Turkish_I_withDot from the equivalence sets of 'i' and 'I'.
                    _ignoreCaseRel_Invariant = _solver.And(inv_table, _solver.Not(_solver.ShiftLeft(tr_I_withdot_BDD, 16)));

                    // Remove Turkish_I_withDot from the domain of casesensitive characters in the default case
                    _ignoreCaseRel_Invariant_Domain = _solver.And(_ignoreCaseRel_Default_Domain, _solver.Not(tr_I_withdot_BDD));
                }

                domain = _ignoreCaseRel_Invariant_Domain;
                return _ignoreCaseRel_Invariant;
            }

            if (IsTurkishAlphabet(culture))
            {
                SetUpDefault();

                if (_ignoreCaseRel_Turkish == null)
                {
                    // Compute the tr table based off of default.
                    // In the default (en-US) culture: Turkish_I_withDot = i = I
                    // In the tr culture: i = Turkish_I_withDot, I = Turkish_i_withoutDot
                    BDD tr_I_withdot_BDD = _solver.CharConstraint(Turkish_I_WithDot);
                    BDD tr_i_withoutdot_BDD = _solver.CharConstraint(Turkish_i_WithoutDot);
                    BDD i_BDD = _solver.CharConstraint('i');
                    BDD I_BDD = _solver.CharConstraint('I');

                    // First remove all i's from the default table from the LHS and from the RHS.
                    // Note that Turkish_i_withoutDot is not in the default table because it is case-insensitive in the en-US culture.
                    BDD iDefault = _solver.Or(i_BDD, _solver.Or(I_BDD, tr_I_withdot_BDD));
                    BDD tr_table = _solver.And(_ignoreCaseRel_Default, _solver.Not(iDefault));
                    tr_table = _solver.And(tr_table, _solver.Not(_solver.ShiftLeft(iDefault, 16)));

                    BDD i_tr = _solver.Or(i_BDD, tr_I_withdot_BDD);
                    BDD I_tr = _solver.Or(I_BDD, tr_i_withoutdot_BDD);

                    // The Cartesian product i_tr X i_tr.
                    BDD i_trXi_tr = _solver.And(_solver.ShiftLeft(i_tr, 16), i_tr);

                    // The Cartesian product I_tr X I_tr.
                    BDD I_trXI_tr = _solver.And(_solver.ShiftLeft(I_tr, 16), I_tr);

                    // Update the table with the new entries, and add Turkish_i_withoutDot also into the domain of case-sensitive characters.
                    _ignoreCaseRel_Turkish = _solver.Or(tr_table, _solver.Or(i_trXi_tr, I_trXI_tr));
                    _ignoreCaseRel_Turkish_Domain = _solver.Or(_ignoreCaseRel_Default_Domain, tr_i_withoutdot_BDD);
                }

                domain = _ignoreCaseRel_Turkish_Domain;
                return _ignoreCaseRel_Turkish;
            }

            // All other cultures are equivalent to the default culture wrt case-sensitivity.
            SetUpDefault();
            domain = _ignoreCaseRel_Default_Domain;
            return _ignoreCaseRel_Default;
        }

        /// <summary>
        /// Get the set of CI-equivalent characters to c.
        /// This operation depends on culture for i, I, '\u0130', and '\u0131';
        /// culture="" means InvariantCulture while culture=null means to use the current culture.
        /// </summary>
        public BDD Apply(char c, string culture = null)
        {
            if (_char_table_CI[c] is BDD bdd)
            {
                return bdd;
            }

            culture ??= Globalization.CultureInfo.CurrentCulture.Name;
            switch (c)
            {
                case 'i':
                    return
                        culture == string.Empty ? _i_Invariant :
                        IsTurkishAlphabet(culture) ? _i_Turkish :
                        _i_Default; // for all other cultures, case-sensitivity is the same as for en-US

                case 'I':
                    return
                        culture == string.Empty ? _i_Invariant :
                        IsTurkishAlphabet(culture) ? _I_Turkish : // different from 'i' above
                        _i_Default;

                case Turkish_I_WithDot:
                    return
                        culture == string.Empty ? _solver.CharConstraint(Turkish_I_WithDot) :
                        IsTurkishAlphabet(culture) ? _i_Turkish :
                        _i_Default;

                case Turkish_i_WithoutDot:
                    return
                        IsTurkishAlphabet(culture) ? _I_Turkish :
                        _solver.CharConstraint(Turkish_i_WithoutDot);

                case 'k':
                case 'K':
                case KelvinSign:
                    return _char_table_CI[c] = _solver.Or(_solver.Or(_solver.CharConstraint('k'), _solver.CharConstraint('K')), _solver.CharConstraint(KelvinSign));

                case <= '\x7F':
                    // For ASCII range other than letters i, I, k, and K, the case-conversion is independent of culture and does
                    // not include case-insensitive-equivalent non-ASCII.
                    return _char_table_CI[c] = _solver.Or(_solver.CharConstraint(char.ToLower(c)), _solver.CharConstraint(char.ToUpper(c)));

                default:
                    // Bring in the full transfomation relation, but here it does not actually depend on culture
                    // so it is safe to store the result for c.
                    return _char_table_CI[c] = Apply(_solver.CharConstraint(c));
            }
        }

        /// <summary>
        /// For all letters in the bdd add their lower and upper case equivalents.
        /// This operation depends on culture for i, I, '\u0130', and '\u0131';
        /// culture="" means InvariantCulture while culture=null means to use the current culture.
        /// </summary>
        public BDD Apply(BDD bdd, string culture = null)
        {
            // First get the culture specific relation
            BDD ignoreCaseRel = GetIgnoreCaseRel(out BDD domain, culture);
            if (_solver.And(domain, bdd).IsEmpty)
            {
                //no elements need to be added
                return bdd;
            }

            // Compute the set of all characters that are equivalent to some element in bdd.
            // restr is the relation restricted to the relevant characters in bdd.
            // This conjunction works because bdd is unspecified for bits > 15.
            BDD restr = _solver.And(bdd, ignoreCaseRel);

            // Shiftright essentially produces the LHS of the relation (char X char) that restr represents.
            BDD ignorecase = _solver.ShiftRight(restr, 16);

            // The final set is the union of all the characters.
            return _solver.Or(ignorecase, bdd);
        }

        private static bool IsTurkishAlphabet(string culture) =>
            culture is "az" or "az-Cyrl" or "az-Cyrl-AZ" or "az-Latn" or "az-Latn-AZ" or "tr" or "tr-CY" or "tr-TR";
    }
}
