// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace System.Text.RegularExpressions.SRM.Unicode
{
    internal class IgnoreCaseTransformer
    {
        private const char Turkish_I_withDot = '\u0130';
        private const char Turkish_i_withoutDot = '\u0131';
        private const char Kelvin_sign = '\u212A';

        private CharSetSolver _solver;

        private BDD _IgnoreCaseRel_default;
        private BDD _IgnoreCaseRel_inv;
        private BDD _IgnoreCaseRel_tr;
        private BDD _IgnoreCaseRel_default_dom;
        private BDD _IgnoreCaseRel_inv_dom;
        private BDD _IgnoreCaseRel_tr_dom;

        private BDD _i_default;
        private BDD _i_inv;
        private BDD _i_tr;
        private BDD _I_tr;

        //maps each char c to the Case-Insensitive set of c that is culture-idependent (for non-null entries)
        private BDD[] _char_table_CI = new BDD[0x10000];

        public IgnoreCaseTransformer(CharSetSolver solver)
        {
            _solver = solver;
            _i_inv = solver.MkOr(_solver.MkCharConstraint('i'), solver.MkCharConstraint('I'));
            _i_default = solver.MkOr(_i_inv, solver.MkCharConstraint(Turkish_I_withDot));
            _i_tr = solver.MkOr(solver.MkCharConstraint('i'), solver.MkCharConstraint(Turkish_I_withDot));
            _I_tr = solver.MkOr(solver.MkCharConstraint('I'), solver.MkCharConstraint(Turkish_i_withoutDot));
        }

        /// <summary>
        /// Gets the correct transformation relation based on the current culture;
        /// culture="" means InvariantCulture while culture=null means to use the current culture.
        /// </summary>
        private BDD GetIgnoreCaseRel(out BDD domain, string culture = null)
        {
            culture = (culture != null ? culture : Globalization.CultureInfo.CurrentCulture.Name);
            //pick the correct transformer BDD based on current culture
            if (culture == "en-US")
            {
                if (_IgnoreCaseRel_default == null)
                {
                    //deserialize the table for the default culture
                    _IgnoreCaseRel_default = BDD.Deserialize(IgnoreCaseRelation.s_IgnoreCaseBDD_repr, _solver);
                    //represents characters for which the transformation is relevant
                    _IgnoreCaseRel_default_dom = _solver.ShiftRight(_IgnoreCaseRel_default, 16);
                }
                domain = _IgnoreCaseRel_default_dom;
                return _IgnoreCaseRel_default;
            }
            else if (culture == string.Empty)
            {
                if (_IgnoreCaseRel_inv == null)
                {
                    //deserialize the table for the invariant culture
                    _IgnoreCaseRel_inv = BDD.Deserialize(IgnoreCaseRelation.s_IgnoreCaseBDD_inv_repr, _solver);
                    _IgnoreCaseRel_inv_dom = _solver.ShiftRight(_IgnoreCaseRel_inv, 16);
                }
                domain = _IgnoreCaseRel_inv_dom;
                return _IgnoreCaseRel_inv;
            }
            else if (IsTurkishAlphabet(culture))
            {
                if (_IgnoreCaseRel_tr == null)
                {
                    //deserialize the table for the invariant culture
                    _IgnoreCaseRel_tr = BDD.Deserialize(IgnoreCaseRelation.s_IgnoreCaseBDD_tr_repr, _solver);
                    _IgnoreCaseRel_tr_dom = _solver.ShiftRight(_IgnoreCaseRel_tr, 16);
                }
                domain = _IgnoreCaseRel_tr_dom;
                return _IgnoreCaseRel_tr;
            }
            else
            {
                if (_IgnoreCaseRel_default == null)
                {
                    _IgnoreCaseRel_default = BDD.Deserialize(IgnoreCaseRelation.s_IgnoreCaseBDD_repr, _solver);
                    _IgnoreCaseRel_default_dom = _solver.ShiftRight(_IgnoreCaseRel_default, 16);
                }
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
                culture = (culture != null ? culture : Globalization.CultureInfo.CurrentCulture.Name);
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
                            var k = _solver.MkOr(_solver.MkOr(_solver.MkCharConstraint('k'), _solver.MkCharConstraint('K')), _solver.MkCharConstraint(Kelvin_sign));
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
                            var set = Apply(_solver.MkCharConstraint(c));
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
            BDD domain;
            BDD ignoreCaseRel = GetIgnoreCaseRel(out domain, culture);
            if (_solver.MkAnd(domain, bdd).IsEmpty)
                //no elements need to be added
                return bdd;
            else
            {
                //compute the set of all characters that are equivalent to some element in bdd
                //restr is the relation restricted to the relevant characters in bdd
                //this conjunction works because bdd is unspecified for bits > 15
                var restr = _solver.MkAnd(bdd, ignoreCaseRel);
                //shiftright essentially produces the LHS of the relation (char X char) that restr represents
                var ignorecase = _solver.ShiftRight(restr, 16);
                //the final set is the union of all the characters
                var res = _solver.MkOr(ignorecase, bdd);
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
