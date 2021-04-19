// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Runtime.Serialization;
using System.IO;

namespace System.Text.RegularExpressions.SRM
{
    /// <summary>
    /// Provides IsMatch and Matches methods.
    /// </summary>
    internal interface IMatcher
    {
        /// <summary>
        /// Returns the next match (startindex, length) in the input string.
        /// </summary>
        /// <param name="isMatch">if true then return null iff a match exists</param>
        /// <param name="input">given iput string</param>
        /// <param name="startat">start position in the input, default is 0</param>
        /// <param name="endat">end position in the input, -1 means that the value is unspecified and taken to be input.Length-1</param>
        public Match FindMatch(bool isMatch, string input, int startat = 0, int endat = -1);

        public void Serialize(StringBuilder sb);

        public void SaveDGML(TextWriter writer, int bound, bool hideDerivatives, bool addDotStar, int maxLabelLength);
    }
}
