// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using System.Runtime.CompilerServices;

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
            // TBD: endat is buggy in _srm.matcher.FindMatch
            if (beg + length < input.Length)
                throw new NotImplementedException($"{nameof(RunSRM)} with {nameof(length)} restriction");

            int endat = Math.Min(beg + length, input.Length) - 1;

            if (startat > endat)
                return System.Text.RegularExpressions.Match.Empty;

            var match = _srm.matcher.FindMatch(quick, input, startat, endat);
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
    }
}
