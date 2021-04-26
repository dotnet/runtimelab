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

        /// <summary>
        /// Custom serialization of the matcher as text in visible ASCII range.
        /// </summary>
        public void Serialize(StringBuilder sb);

        /// <summary>
        /// Unwind the regex of the matcher and save the resulting state graph in DGML
        /// </summary>
        /// <param name="bound">roughly the maximum number of states, 0 means no bound</param>
        /// <param name="hideStateInfo">if true then hide state info</param>
        /// <param name="addDotStar">if true then pretend that there is a .* at the beginning</param>
        /// <param name="inReverse">if true then unwind the regex backwards (addDotStar is then ignored)</param>
        /// <param name="onlyDFAinfo">if true then compute and save only genral DFA info</param>
        /// <param name="writer">dgml output is written here</param>
        /// <param name="maxLabelLength">maximum length of labels in nodes anything over that length is indicated with .. </param>
        public void SaveDGML(TextWriter writer, int bound, bool hideStateInfo, bool addDotStar, bool inReverse, bool onlyDFAinfo, int maxLabelLength);
    }
}
