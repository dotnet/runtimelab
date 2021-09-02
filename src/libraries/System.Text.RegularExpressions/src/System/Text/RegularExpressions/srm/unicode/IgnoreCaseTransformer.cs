// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.RegularExpressions.SRM.Unicode
{
    internal sealed class IgnoreCaseTransformer
    {
        private const char Turkish_I_withDot = '\u0130';
        private const char Turkish_i_withoutDot = '\u0131';
        private const char Kelvin_sign = '\u212A';

        private readonly CharSetSolver _solver;

        private BDD _IgnoreCaseRel_default;
        private BDD _IgnoreCaseRel_inv;
        private BDD _IgnoreCaseRel_tr;
        private BDD _IgnoreCaseRel_default_dom;
        private BDD _IgnoreCaseRel_inv_dom;
        private BDD _IgnoreCaseRel_tr_dom;

        private readonly BDD _i_default;
        private readonly BDD _i_inv;
        private readonly BDD _i_tr;
        private readonly BDD _I_tr;

        //maps each char c to the Case-Insensitive set of c that is culture-idependent (for non-null entries)
        private readonly BDD[] _char_table_CI = new BDD[0x10000];

        public IgnoreCaseTransformer(CharSetSolver solver)
        {
            _solver = solver;
            _i_inv = solver.MkOr(_solver.MkCharConstraint('i'), solver.MkCharConstraint('I'));
            _i_default = solver.MkOr(_i_inv, solver.MkCharConstraint(Turkish_I_withDot));
            _i_tr = solver.MkOr(solver.MkCharConstraint('i'), solver.MkCharConstraint(Turkish_I_withDot));
            _I_tr = solver.MkOr(solver.MkCharConstraint('I'), solver.MkCharConstraint(Turkish_i_withoutDot));
        }

        private void SetUpDefault()
        {
            if (_IgnoreCaseRel_default == null)
            {
                //deserialize the table for the default culture
                _IgnoreCaseRel_default = BDD.Deserialize(IgnoreCaseRelation.s_IgnoreCaseBDD_repr, _solver);
                //represents the set of all casesensitive characters in the default culture
                _IgnoreCaseRel_default_dom = _solver.ShiftRight(_IgnoreCaseRel_default, 16);
            }
        }

        /// <summary>
        /// Gets the correct transformation relation based on the current culture;
        /// culture="" means InvariantCulture while culture=null means to use the current culture.
        /// </summary>
        private BDD GetIgnoreCaseRel(out BDD domain, string culture = null)
        {
            culture ??= Globalization.CultureInfo.CurrentCulture.Name;
            //pick the correct transformer BDD based on current culture
            if (culture == "en-US")
            {
                SetUpDefault();
                domain = _IgnoreCaseRel_default_dom;
                return _IgnoreCaseRel_default;
            }
            else if (culture == string.Empty)
            {
                if (_IgnoreCaseRel_inv == null)
                {
                    SetUpDefault();
                    //compute the inv table based off of default
                    //in the default (en-US) culture: Turkish_I_withDot = i = I
                    //in the invariant culture: i = I, while Turkish_I_withDot is caseinsensitive
                    BDD tr_I_withdot_BDD = _solver.MkCharConstraint(Turkish_I_withDot);
                    BDD i_BDD = _solver.MkCharConstraint('i');
                    BDD I_BDD = _solver.MkCharConstraint('I');
                    //since Turkish_I_withDot is caseinsensitive in invariant culture, remove it from the default (en-US culture) table
                    BDD inv_table = _solver.MkAnd(_IgnoreCaseRel_default, _solver.MkNot(tr_I_withdot_BDD));
                    //Next remove Turkish_I_withDot from the RHS of the relation also
                    //effectively this removes Turkish_I_withDot from the equivalence sets of 'i' and 'I'
                    _IgnoreCaseRel_inv = _solver.MkAnd(inv_table, _solver.MkNot(_solver.ShiftLeft(tr_I_withdot_BDD, 16)));
                    //remove Turkish_I_withDot from the domain of casesensitive characters in the default case
                    _IgnoreCaseRel_inv_dom = _solver.MkAnd(_IgnoreCaseRel_default_dom, _solver.MkNot(tr_I_withdot_BDD));
                }
                domain = _IgnoreCaseRel_inv_dom;
                return _IgnoreCaseRel_inv;
            }
            else if (IsTurkishAlphabet(culture))
            {
                if (_IgnoreCaseRel_tr == null)
                {
                    SetUpDefault();
                    //compute the tr table based off of default
                    //in the default (en-US) culture: Turkish_I_withDot = i = I
                    //in the tr culture: i = Turkish_I_withDot, I = Turkish_i_withoutDot
                    BDD tr_I_withdot_BDD = _solver.MkCharConstraint(Turkish_I_withDot);
                    BDD tr_i_withoutdot_BDD = _solver.MkCharConstraint(Turkish_i_withoutDot);
                    BDD i_BDD = _solver.MkCharConstraint('i');
                    BDD I_BDD = _solver.MkCharConstraint('I');
                    //first remove all i's from the default table from the LHS and from the RHS
                    //note that Turkish_i_withoutDot is not in the default table because it is caseinsensitive in the en-US culture
                    BDD iDefault = _solver.MkOr(i_BDD, _solver.MkOr(I_BDD, tr_I_withdot_BDD));
                    BDD tr_table = _solver.MkAnd(_IgnoreCaseRel_default, _solver.MkNot(iDefault));
                    tr_table = _solver.MkAnd(tr_table, _solver.MkNot(_solver.ShiftLeft(iDefault, 16)));
                    // i_tr = {i,Turkish_I_withDot}
                    BDD i_tr = _solver.MkOr(i_BDD, tr_I_withdot_BDD);
                    // I_tr = {I,Turkish_i_withoutDot}
                    BDD I_tr = _solver.MkOr(I_BDD, tr_i_withoutdot_BDD);
                    // the Cartesian product i_tr X i_tr
                    BDD i_trXi_tr = _solver.MkAnd(_solver.ShiftLeft(i_tr, 16), i_tr);
                    // the Cartesian product I_tr X I_tr
                    BDD I_trXI_tr = _solver.MkAnd(_solver.ShiftLeft(I_tr, 16), I_tr);
                    // update the table with the new entries
                    _IgnoreCaseRel_tr = _solver.MkOr(tr_table, _solver.MkOr(i_trXi_tr, I_trXI_tr));
                    //finally add Turkish_i_withoutDot also into the domain of casesensitive characters
                    _IgnoreCaseRel_tr_dom = _solver.MkOr(_IgnoreCaseRel_default_dom, tr_i_withoutdot_BDD);
                }
                domain = _IgnoreCaseRel_tr_dom;
                return _IgnoreCaseRel_tr;
            }
            else
            {
                //all other cultures are equivalent to the en-US culture wrt casesensitivity
                SetUpDefault();
                domain = _IgnoreCaseRel_default_dom;
                return _IgnoreCaseRel_default;
            }
        }

        /// <summary>
        /// Get the set of CI-equivalent characters to c.
        /// This operation depends on culture for i, I, '\u0130', and '\u0131';
        /// culture="" means InvariantCulture while culture=null means to use the current culture.
        /// </summary>
        public BDD Apply(char c, string culture = null)
        {
            if (_char_table_CI[c] == null)
            {
                culture ??= Globalization.CultureInfo.CurrentCulture.Name;
                switch (c)
                {
                    case 'i':
                        if (culture == "en-US")
                            return _i_default;
                        else if (culture == string.Empty)
                            return _i_inv;
                        else if (IsTurkishAlphabet(culture))
                            return _i_tr;
                        else
                            //for all other cultures case-sensitivity is the same as for en-US
                            return _i_default;
                    case 'I':
                        if (culture == "en-US")
                            return _i_default;
                        else if (culture == string.Empty)
                            return _i_inv;
                        else if (IsTurkishAlphabet(culture))
                            return _I_tr;
                        else
                            return _i_default;
                    case Turkish_I_withDot:
                        if (culture == "en-US")
                            return _i_default;
                        else if (culture == string.Empty)
                            return _solver.MkCharConstraint(Turkish_I_withDot);
                        else if (IsTurkishAlphabet(culture))
                            return _i_tr;
                        else
                            return _i_default;
                    case Turkish_i_withoutDot:
                        if (culture == "en-US" || culture == string.Empty)
                            return _solver.MkCharConstraint(Turkish_i_withoutDot);
                        else if (IsTurkishAlphabet(culture))
                            return _I_tr;
                        else
                            //for all other cultures case-sensitivity is the same as for en-US
                            return _solver.MkCharConstraint(Turkish_i_withoutDot);
                    default:
                        if (c == 'k' || c == 'K' || c == Kelvin_sign)
                        {
                            BDD k = _solver.MkOr(_solver.MkOr(_solver.MkCharConstraint('k'), _solver.MkCharConstraint('K')), _solver.MkCharConstraint(Kelvin_sign));
                            _char_table_CI[c] = k;
                            return k;
                        }
                        else if (c <= '\x7F')
                        {
                            //for ASCII range other than letters i,I,k,K
                            //the case-conversion is independent of culture and does not include case-insensitive-equivalent nonascci
                            BDD set = _solver.MkOr(_solver.MkCharConstraint(char.ToLower(c)), _solver.MkCharConstraint(char.ToUpper(c)));
                            _char_table_CI[c] = set;
                            return set;
                        }
                        else
                        {
                            //bring in the full transfomation relation, but here it does not actually depend on culture
                            //so it is safe to store the result for c
                            BDD set = Apply(_solver.MkCharConstraint(c));
                            _char_table_CI[c] = set;
                            return set;
                        }
                }
            }
            else
                return _char_table_CI[c];
        }

        /// <summary>
        /// For all letters in the bdd add their lower and upper case equivalents.
        /// This operation depends on culture for i, I, '\u0130', and '\u0131';
        /// culture="" means InvariantCulture while culture=null means to use the current culture.
        /// </summary>
        public BDD Apply(BDD bdd, string culture = null)
        {
            //first get the culture specific relation
            BDD ignoreCaseRel = GetIgnoreCaseRel(out BDD domain, culture);
            if (_solver.MkAnd(domain, bdd).IsEmpty)
            {
                //no elements need to be added
                return bdd;
            }
            else
            {
                //compute the set of all characters that are equivalent to some element in bdd
                //restr is the relation restricted to the relevant characters in bdd
                //this conjunction works because bdd is unspecified for bits > 15
                BDD restr = _solver.MkAnd(bdd, ignoreCaseRel);
                //shiftright essentially produces the LHS of the relation (char X char) that restr represents
                BDD ignorecase = _solver.ShiftRight(restr, 16);
                //the final set is the union of all the characters
                BDD res = _solver.MkOr(ignorecase, bdd);
                return res;
            }
        }

        private static bool IsTurkishAlphabet(string culture) =>
            culture == "az" ||
            culture == "az-Cyrl" ||
            culture == "az-Cyrl-AZ" ||
            culture == "az-Latn" ||
            culture == "az-Latn-AZ" ||
            culture == "tr" ||
            culture == "tr-CY" ||
            culture == "tr-TR";
    }
}
