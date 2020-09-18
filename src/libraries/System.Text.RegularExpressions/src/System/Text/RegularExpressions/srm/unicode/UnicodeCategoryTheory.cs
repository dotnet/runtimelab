using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SRM.Generated;

namespace Microsoft.SRM
{
    /// <summary>
    /// Maps unicode categories to correspoing character predicates.
    /// </summary>
    /// <typeparam name="PRED">predicates</typeparam>
    internal interface IUnicodeCategoryTheory<PRED>
    {
        /// <summary>
        /// Gets the unicode category condition for unicode category cat, that must be an integer between 0 and 29
        /// </summary>
        PRED CategoryCondition(int cat);

        /// <summary>
        /// Gets the white space condition
        /// </summary>
        PRED WhiteSpaceCondition { get; }

        /// <summary>
        /// Gets the word letter (\w) condition
        /// </summary>
        PRED WordLetterCondition { get; }

        string[] UnicodeCategoryStandardAbbreviations { get; }
    }

    internal class UnicodeCategoryTheory<PRED> : IUnicodeCategoryTheory<PRED>
    {
        private ICharAlgebra<PRED> solver;
        private PRED[] catConditions = new PRED[30];
        private PRED whiteSpaceCondition;
        private PRED wordLetterCondition;

        public string[] UnicodeCategoryStandardAbbreviations
        {
            get
            {
                return unicodeCategoryStandardAbbreviations;
            }
        }

        #region unicode category abbreviations
        public static string[] unicodeCategoryStandardAbbreviations = new string[30]{
            "Lu", //0: UppercaseLetter
            "Ll", //1: LowercaseLetter
            "Lt", //2: TitlecaseLetter
            "Lm", //3: ModifierLetter
            "Lo", //4: OtherLetter
            "Mn", //5: NonSpacingMark
            "Mc", //6: SpacingCombiningMark
            "Me", //7: EnclosingMark
            "Nd", //8: DecimalDigitNumber
            "Nl", //9: LetterNumber
            "No", //10: OtherNumber
            "Zs", //11: SpaceSeparator
            "Zl", //12: LineSeparator
            "Zp", //13: ParagraphSeparator
            "Cc", //14: Control
            "Cf", //15: Format
            "Cs", //16: Surrogate
            "Co", //17: PrivateUse
            "Pc", //18: ConnectorPunctuation
            "Pd", //19: DashPunctuation
            "Ps", //20: OpenPunctuation
            "Pe", //21: ClosePunctuation
            "Pi", //22: InitialQuotePunctuation
            "Pf", //23: FinalQuotePunctuation
            "Po", //24: OtherPunctuation
            "Sm", //25: MathSymbol
            "Sc", //26: CurrencySymbol
            "Sk", //27: ModifierSymbol
            "So", //28: OtherSymbol
            "Cn", //29: OtherNotAssigned
        };
        #endregion

        public static string UnicodeCategoryPredicateName(int cat)
        {
            string catName = ((System.Globalization.UnicodeCategory)cat).ToString();
            return "Is" + catName;
        }

        public UnicodeCategoryTheory(ICharAlgebra<PRED> solver)
        {
            this.solver = solver;
            InitializeUnicodeCategoryDefinitions();
        }

        private PRED MkRangesConstraint(IEnumerable<int[]> ranges)
        {
            PRED res = solver.False;
            foreach (var range in ranges)
                res = solver.MkOr(res, solver.MkRangeConstraint((char)range[0], (char)range[1]));
            return res;
        }

        private void InitializeUnicodeCategoryDefinitions()
        {
            if (solver.Encoding == BitWidth.BV7)
            {
                //use ranges directly
                for (int i = 0; i < 30; i++)
                    if (UnicodeCategoryRanges.ASCII[i] == null)
                        catConditions[i] = solver.False;
                    else
                        catConditions[i] = solver.MkCharPredicate(
                              UnicodeCategoryPredicateName(i), MkRangesConstraint(UnicodeCategoryRanges.ASCII[i]));

                whiteSpaceCondition = solver.MkCharPredicate(
                              "IsWhitespace", MkRangesConstraint(UnicodeCategoryRanges.ASCIIWhitespace));
                wordLetterCondition = solver.MkCharPredicate(
                              "IsWordletter", MkRangesConstraint(UnicodeCategoryRanges.ASCIIWordCharacter));
            }
            else if (solver.Encoding == BitWidth.BV8)
            {
                //use BDDs
                for (int i = 0; i < 30; i++)
                    if (UnicodeCategoryRanges.CP437Bdd[i] == null)
                        catConditions[i] = solver.False;
                    else
                        catConditions[i] = solver.MkCharPredicate(
                              UnicodeCategoryPredicateName(i),
                              solver.ConvertFromCharSet(solver.CharSetProvider.DeserializeCompact(UnicodeCategoryRanges.CP437Bdd[i])));
                whiteSpaceCondition = solver.MkCharPredicate("IsWhitespace",
                             solver.ConvertFromCharSet(solver.CharSetProvider.DeserializeCompact(UnicodeCategoryRanges.CP437WhitespaceBdd)));
                wordLetterCondition = solver.MkCharPredicate("IsWordletter",
                             solver.ConvertFromCharSet(solver.CharSetProvider.DeserializeCompact(UnicodeCategoryRanges.CP437WordCharacterBdd)));
            }
            else
            {
                //use BDDs
                for (int i = 0; i < 30; i++)
                    catConditions[i] = solver.MkCharPredicate(
                         UnicodeCategoryPredicateName(i),
                         solver.ConvertFromCharSet(solver.CharSetProvider.DeserializeCompact(UnicodeCategoryRanges.UnicodeBdd[i])));
                whiteSpaceCondition = solver.MkCharPredicate("IsWhitespace",
                             solver.ConvertFromCharSet(solver.CharSetProvider.DeserializeCompact(UnicodeCategoryRanges.UnicodeWhitespaceBdd)));
                wordLetterCondition = solver.MkCharPredicate("IsWordletter",
                             solver.ConvertFromCharSet(solver.CharSetProvider.DeserializeCompact(UnicodeCategoryRanges.UnicodeWordCharacterBdd)));
            }
        }

        #region IUnicodeCategoryTheory<Expr> Members

        public PRED CategoryCondition(int i)
        {
            if (object.Equals(catConditions[i], default(PRED))) //uninitialized
            {
                if (solver.Encoding == BitWidth.BV7)
                {
                    if (UnicodeCategoryRanges.ASCII[i] == null)
                        catConditions[i] = solver.False;
                    else
                        catConditions[i] = solver.MkCharPredicate(
                              UnicodeCategoryPredicateName(i), MkRangesConstraint(UnicodeCategoryRanges.ASCII[i]));
                }
                else if (solver.Encoding == BitWidth.BV8)
                {
                    //use BDDs
                    if (UnicodeCategoryRanges.CP437Bdd[i] == null)
                        catConditions[i] = solver.False;
                    else
                        catConditions[i] = solver.MkCharPredicate(
                              UnicodeCategoryPredicateName(i),
                              solver.ConvertFromCharSet(solver.CharSetProvider.DeserializeCompact(UnicodeCategoryRanges.CP437Bdd[i])));
                }
                else
                {
                    catConditions[i] = solver.MkCharPredicate(
                         UnicodeCategoryPredicateName(i),
                         solver.ConvertFromCharSet(solver.CharSetProvider.DeserializeCompact(UnicodeCategoryRanges.UnicodeBdd[i])));
                }
            }
            return catConditions[i];
        }

        public PRED WhiteSpaceCondition
        {
            get {
                if (object.Equals(whiteSpaceCondition, default(PRED)))
                {
                    if (solver.Encoding == BitWidth.BV7)
                    {
                        whiteSpaceCondition = solver.MkCharPredicate(
                                      "IsWhitespace", MkRangesConstraint(UnicodeCategoryRanges.ASCIIWhitespace));
                    }
                    else if (solver.Encoding == BitWidth.BV8)
                    {
                        //use BDDs
                        whiteSpaceCondition = solver.MkCharPredicate("IsWhitespace",
                                     solver.ConvertFromCharSet(solver.CharSetProvider.DeserializeCompact(UnicodeCategoryRanges.CP437WhitespaceBdd)));
                    }
                    else
                    {
                        //use BDDs
                        whiteSpaceCondition = solver.MkCharPredicate("IsWhitespace",
                                     solver.ConvertFromCharSet(solver.CharSetProvider.DeserializeCompact(UnicodeCategoryRanges.UnicodeWhitespaceBdd)));
                    }
                }
                return whiteSpaceCondition;
            }
        }

        public PRED WordLetterCondition
        {
            get {
                if (object.Equals(wordLetterCondition, default(PRED)))
                {
                    if (solver.Encoding == BitWidth.BV7)
                    {
                        wordLetterCondition = solver.MkCharPredicate(
                                      "IsWordletter", MkRangesConstraint(UnicodeCategoryRanges.ASCIIWordCharacter));
                    }
                    else if (solver.Encoding == BitWidth.BV8)
                    {
                        //use BDDs
                        wordLetterCondition = solver.MkCharPredicate("IsWordletter",
                                     solver.ConvertFromCharSet(solver.CharSetProvider.DeserializeCompact(UnicodeCategoryRanges.CP437WordCharacterBdd)));
                    }
                    else
                    {
                        //use BDDs
                        wordLetterCondition = solver.MkCharPredicate("IsWordletter",
                                     solver.ConvertFromCharSet(solver.CharSetProvider.DeserializeCompact(UnicodeCategoryRanges.UnicodeWordCharacterBdd)));
                    }
                }
                return wordLetterCondition;
            }
        }

        #endregion
    }
}
