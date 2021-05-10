// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Runtime.CompilerServices;
using System.IO;

namespace System.Text.RegularExpressions
{
    public partial class Regex
    {
        /// <summary>
        /// This method is called and the SRM related classes should be loaded only if _useSRM is true
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal Match? RunSRM(bool quick, string input, int beg, int startat, int length)
        {
            int endat = Math.Min(beg + length, input.Length) - 1;

            if (startat > endat)
                return System.Text.RegularExpressions.Match.Empty;

            var match = _srm._matcher.FindMatch(quick, input, startat, endat);
            if (quick)
            {
                if (match is null)
                    return null;
                else
                    return System.Text.RegularExpressions.Match.Empty;
            }
            else if (match.Success)
            {
                Match res = new Match(this, 1, input, beg, length, startat);
                res._matches[0][0] = match.Index;
                res._matches[0][1] = match.Length;
                res._matchcount[0] = 1;
                res.Tidy(match.Index + match.Length);
                return res;
            }
            else
                return System.Text.RegularExpressions.Match.Empty;
        }

        /// <summary>
        /// Unwind the regex and save the resulting state graph in DGML
        /// </summary>
        /// <param name="bound">roughly the maximum number of states, 0 means no bound</param>
        /// <param name="hideStateInfo">if true then hide state info</param>
        /// <param name="addDotStar">if true then pretend that there is a .* at the beginning</param>
        /// <param name="inReverse">if true then unwind the regex backwards (addDotStar is then ignored)</param>
        /// <param name="onlyDFAinfo">if true then compute and save only genral DFA info</param>
        /// <param name="writer">dgml output is written here</param>
        /// <param name="maxLabelLength">maximum length of labels in nodes anything over that length is indicated with .. </param>
        internal void SaveDGML(TextWriter writer, int bound, bool hideStateInfo, bool addDotStar, bool inReverse, bool onlyDFAinfo, int maxLabelLength)
        {
            if (!_useSRM)
                throw new NotSupportedException();

            _srm.SaveDGML(writer, bound, hideStateInfo, addDotStar, inReverse, onlyDFAinfo, maxLabelLength);
        }

        internal string Serialize()
        {
            if (!_useSRM)
                throw new NotSupportedException();

            StringBuilder sb = new StringBuilder();
            _srm.Serialize(sb);
            return sb.ToString();
        }

        internal static Regex Deserialize(string s)
        {
            var srm = SRM.Regex.Deserialize(s);
            Regex r = new(srm);
            return r;
        }

        private Regex(SRM.Regex srm)
        {
            _useSRM = true;
            _srm = srm;
        }

#if DEBUG
        /// <summary>
        /// Generates two files IgnoreCaseRelation.cs and UnicodeCategoryRanges.cs for the namespace System.Text.RegularExpressions.SRM.Unicode
        /// in the given directory path. Only avaliable in DEBUG mode.
        /// </summary>
        internal static void GenerateUnicodeTables(string path)
        {
            SRM.Unicode.IgnoreCaseRelationGenerator.Generate("System.Text.RegularExpressions.SRM.Unicode", "IgnoreCaseRelation", path);
            SRM.Unicode.UnicodeCategoryRangesGenerator.Generate("System.Text.RegularExpressions.SRM.Unicode", "UnicodeCategoryRanges", path);
        }
#endif
    }
}
