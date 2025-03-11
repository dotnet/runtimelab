using System;
using System.Collections.Generic;
using TbdParser.Logging;
using TbdParser.Models;

namespace TbdParser.Parsing
{
    /// <summary>
    /// Parser for JSON-based TBD format (version 5+)
    /// Note: This is a placeholder for future implementation
    /// </summary>
    public class JsonTbdFormatParser : ITbdFormatParser
    {
        /// <summary>
        /// Gets the logger used by this parser
        /// </summary>
        public ILogger Logger { get; }

        /// <summary>
        /// Creates a new JSON TBD format parser
        /// </summary>
        public JsonTbdFormatParser(ILogger logger)
        {
            Logger = logger ?? NullLogger.Instance;
        }
        
        public bool CanParse(string[] lines)
        {
            Logger.Warning("JSON format TBD parsing is not yet implemented");
            // This is a placeholder for future implementation
            // For now, we'll throw an exception since JSON parsing is not implemented yet
            throw new NotImplementedException("JSON format parsing for TBD version 5+ is not yet implemented.");
        }

        public TbdFile Parse(string[] lines)
        {
            Logger.Warning("JSON format TBD parsing is not yet implemented");
            // This is a placeholder for future implementation
            // For now, we'll throw an exception since JSON parsing is not implemented yet
            throw new NotImplementedException("JSON format parsing for TBD version 5+ is not yet implemented.");
        }
    }
}
