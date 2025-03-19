// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using TbdParsing.Models;

namespace TbdParsing.Parsing
{
    /// <summary>
    /// Interface for TBD file format parsers
    /// </summary>
    public interface ITbdFormatParser
    {
        /// <summary>
        /// Determines if this parser can handle the given file format based on its content
        /// </summary>
        /// <param name="lines">The lines of the file</param>
        /// <returns>True if this parser can handle the format</returns>
        bool CanParse(string[] lines);

        /// <summary>
        /// Parses the TBD file content
        /// </summary>
        /// <param name="lines">The lines of the file</param>
        /// <returns>A TbdFile object</returns>
        TbdFile Parse(string[] lines);
    }
}
