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
        internal Match RunSRM(string input, int startat, int length)
        {
            Match res = System.Text.RegularExpressions.Match.Empty;
            if (startat >= length)
                return res;

            // TBD: the endat argument of _srm.Matches works incorrectly with length and anchors
            // but cutting the input is also potentially incorrect because of
            // potential new $ or \z or \Z anchor matches that were not possible before
            if (length < input.Length)
                input = input.Substring(0, length);

            var matches = _srm.Matches(input, 1, startat);
            if (matches.Count != 0)
            {
                res = new Match(this, 1, input, 0, length, 0);
                res._matches[0][0] = matches[0].Index;
                res._matches[0][1] = matches[0].Length;
                res._matchcount[0] = 1;
                res.Tidy(0);
            }
            return res;
        }
    }
}
