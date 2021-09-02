// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

namespace System.Text.RegularExpressions
{
    public partial class Regex
    {
        /// <summary>Used iff <see cref="_srm"/> is non-null.</summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal Match? RunSRM(bool quick, string input, int beg, int startat, int length, int prevlen)
        {
            int endAt = beg + length;

            // If the previous match was empty, advance by one before matching
            // or terminate the matching if there is no remaining input to search in
            if (prevlen == 0)
            {
                if (startat == endAt)
                    return RegularExpressions.Match.Empty;

                startat += 1;
            }

            SRM.Match match = _srm._matcher.FindMatch(quick, input, startat, endAt);
            if (quick)
            {
                if (match is null)
                {
                    return null;
                }
            }
            else if (match.Success)
            {
                var m = new Match(this, 1, input, beg, length, startat);
                m._matches[0][0] = match.Index;
                m._matches[0][1] = match.Length;
                m._matchcount[0] = 1;
                m.Tidy(match.Index + match.Length);
                return m;
            }

            return RegularExpressions.Match.Empty;
        }

        // TODO: Avoid rooting SaveDGML/Serialize/Deserialize in ILLink purely for use by tests

        /// <summary>
        /// Unwind the regex and save the resulting state graph in DGML
        /// </summary>
        /// <param name="bound">roughly the maximum number of states, 0 means no bound</param>
        /// <param name="hideStateInfo">if true then hide state info</param>
        /// <param name="addDotStar">if true then pretend that there is a .* at the beginning</param>
        /// <param name="inReverse">if true then unwind the regex backwards (addDotStar is then ignored)</param>
        /// <param name="onlyDFAinfo">if true then compute and save only general DFA info</param>
        /// <param name="writer">dgml output is written here</param>
        /// <param name="maxLabelLength">maximum length of labels in nodes anything over that length is indicated with .. </param>
        internal void SaveDGML(TextWriter writer, int bound, bool hideStateInfo, bool addDotStar, bool inReverse, bool onlyDFAinfo, int maxLabelLength)
        {
            if (_srm is null)
            {
                throw new NotSupportedException();
            }

            _srm.SaveDGML(writer, bound, hideStateInfo, addDotStar, inReverse, onlyDFAinfo, maxLabelLength);
        }

        internal string Serialize()
        {
            if (_srm is null)
            {
                throw new NotSupportedException();
            }

            return _srm.Serialize();
        }

        internal static Regex Deserialize(string s) => new(SRM.Regex.Deserialize(s));

        private Regex(SRM.Regex srm) => _srm = srm;

#if DEBUG
        /// <summary>
        /// Generates two files IgnoreCaseRelation.cs and UnicodeCategoryRanges.cs for the namespace System.Text.RegularExpressions.SRM.Unicode
        /// in the given directory path. Only avaliable in DEBUG mode.
        /// </summary>
        [ExcludeFromCodeCoverage(Justification = "Debug only")]
        internal static void GenerateUnicodeTables(string path)
        {
            SRM.Unicode.IgnoreCaseRelationGenerator.Generate("System.Text.RegularExpressions.SRM.Unicode", "IgnoreCaseRelation", path);
            SRM.Unicode.UnicodeCategoryRangesGenerator.Generate("System.Text.RegularExpressions.SRM.Unicode", "UnicodeCategoryRanges", path);
        }
#endif
    }
}
