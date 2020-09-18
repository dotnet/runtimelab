using System.Text;

namespace Microsoft.SRM
{
    internal static class RegexCharSetPrinter
    {
        internal static string ToRegexCharSet(BDD label, IUnicodeCategoryTheory<BDD> categorizer, CharSetSolver solver)
        {
            if (categorizer.CategoryCondition(8) == label)
                return @"\d";
            if (solver.MkNot(categorizer.CategoryCondition(8)) == label)
                return @"\D";
            if (categorizer.WordLetterCondition == label)
                return @"\w";
            if (solver.MkNot(categorizer.WordLetterCondition) == label)
                return @"\W";
            if (categorizer.WhiteSpaceCondition == label)
                return @"\s";
            if (solver.MkNot(categorizer.WhiteSpaceCondition) == label)
                return @"\S";
            for (int i = 0; i < categorizer.UnicodeCategoryStandardAbbreviations.Length; i++)
                if (categorizer.CategoryCondition(i) == label)
                {
                    return @"\P{" + categorizer.UnicodeCategoryStandardAbbreviations[i] + "}";
                }

            var ranges = solver.ToRanges(label);
            if (ranges.Length == 1 && ranges[0].Item1 == ranges[0].Item2)
            {
                return StringUtility.Escape((char)ranges[0].Item1);
            }

            var res = new StringBuilder("[");
            for (int i = 0; i < ranges.Length; i++ )
            {
                var range = ranges[i];
                if (range.Item1 == range.Item2)
                    res.Append(StringUtility.EscapeWithNumericSpace((char)range.Item1));
                else if (range.Item1 == range.Item2 - 1)
                {
                    res.Append(StringUtility.EscapeWithNumericSpace((char)range.Item1));
                    res.Append(StringUtility.EscapeWithNumericSpace((char)range.Item2));
                }
                else
                {
                    res.Append(StringUtility.EscapeWithNumericSpace((char)range.Item1));
                    res.Append("-");
                    res.Append(StringUtility.EscapeWithNumericSpace((char)range.Item2));
                }
            }
            res.Append("]");
            return res.ToString();
        }
    }
}