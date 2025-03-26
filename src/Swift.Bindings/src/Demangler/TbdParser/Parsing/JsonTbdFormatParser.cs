// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using TbdParsing.Models;
using Microsoft.Extensions.Logging;

namespace TbdParsing.Parsing
{
    /// <summary>
    /// Parser for JSON-based TBD format (version 5+)
    /// Note: This is a placeholder for future implementation
    /// </summary>
    public class JsonTbdFormatParser : TbdFormatParserBase
    {
        /// <summary>
        /// Creates a new JSON TBD format parser
        /// </summary>
        public JsonTbdFormatParser(ILogger<JsonTbdFormatParser> logger) : base(logger)
        {
        }

        public override bool CanParse(string[] lines)
        {
            _logger.LogError("JSON format TBD parsing is not yet implemented");
            // This is a placeholder for future implementation
            // For now, we'll throw an exception since JSON parsing is not implemented yet
            throw new NotImplementedException("JSON format parsing for TBD version 5+ is not yet implemented.");
        }

        public override TbdFile Parse(string[] lines)
        {
            _logger.LogError("JSON format TBD parsing is not yet implemented");
            // This is a placeholder for future implementation
            // For now, we'll throw an exception since JSON parsing is not implemented yet
            throw new NotImplementedException("JSON format parsing for TBD version 5+ is not yet implemented.");
        }
    }
}
