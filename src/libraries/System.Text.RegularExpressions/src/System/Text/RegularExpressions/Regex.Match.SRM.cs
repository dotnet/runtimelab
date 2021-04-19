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
        /// <param name="hideDerivatives">if true then hide derivatives in state labels</param>
        /// <param name="addDotStar">if true then pretend that there is a .* at the beginning</param>
        /// <param name="writer">dgml output is written here</param>
        /// <param name="maxLabelLength">maximum length of labels in nodes anything over that length is indicated with ... </param>
        internal void SaveDGML(TextWriter writer, int bound, bool hideDerivatives,  bool addDotStar, int maxLabelLength)
        {
            if (!_useSRM)
                throw new NotSupportedException();

            _srm.SaveDGML(writer, bound, hideDerivatives, addDotStar, maxLabelLength);
        }
    }
}
