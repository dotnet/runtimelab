// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text.RegularExpressions.SRM;

namespace System.Text.RegularExpressions
{
    public partial class Regex
    {
        // TODO: Avoid rooting Serialize/Deserialize in ILLink purely for use by tests
        // TODO: Only compile SaveDGML into Debug build
        // TODO: Figure out what to do about Serialize/Deserialize in general (e.g. delete them, expose them, use them in source generated code, etc.)

        internal string Serialize()
        {
            if (factory is not SymbolicRegexRunnerFactory srmFactory)
            {
                throw new NotSupportedException();
            }

            return srmFactory._runner.Serialize();
        }

        internal static Regex Deserialize(string serializedNonBacktrackingRegex) =>
            SymbolicRegexRunner.DeserializeRegex(serializedNonBacktrackingRegex);

        /// <summary>Unwind the regex and save the resulting state graph in DGML</summary>
        /// <param name="bound">roughly the maximum number of states, 0 means no bound</param>
        /// <param name="hideStateInfo">if true then hide state info</param>
        /// <param name="addDotStar">if true then pretend that there is a .* at the beginning</param>
        /// <param name="inReverse">if true then unwind the regex backwards (addDotStar is then ignored)</param>
        /// <param name="onlyDFAinfo">if true then compute and save only general DFA info</param>
        /// <param name="writer">dgml output is written here</param>
        /// <param name="maxLabelLength">maximum length of labels in nodes anything over that length is indicated with .. </param>
        internal void SaveDGML(TextWriter writer, int bound, bool hideStateInfo, bool addDotStar, bool inReverse, bool onlyDFAinfo, int maxLabelLength)
        {
            if (factory is not SymbolicRegexRunnerFactory srmFactory)
            {
                throw new NotSupportedException();
            }

            srmFactory._runner.SaveDGML(writer, bound, hideStateInfo, addDotStar, inReverse, onlyDFAinfo, maxLabelLength);
        }

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
