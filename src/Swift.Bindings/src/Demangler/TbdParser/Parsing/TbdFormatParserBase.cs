// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using TbdParsing.Logging;
using TbdParsing.Models;

namespace TbdParsing.Parsing
{
    /// <summary>
    /// Base class for TBD format parsers that handles common functionality
    /// </summary>
    public abstract class TbdFormatParserBase : ITbdFormatParser
    {
        /// <summary>
        /// The logger instance used by the parser
        /// </summary>
        protected readonly ILogger _logger;

        /// <summary>
        /// Creates a new TBD format parser base with the specified logger
        /// </summary>
        protected TbdFormatParserBase(ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// Determines if this parser can handle the given file format based on its content
        /// </summary>
        public abstract bool CanParse(string[] lines);

        /// <summary>
        /// Parses the TBD file content
        /// </summary>
        public abstract TbdFile Parse(string[] lines);
    }
}
