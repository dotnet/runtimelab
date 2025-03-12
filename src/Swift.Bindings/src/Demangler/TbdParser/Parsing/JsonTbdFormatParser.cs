using System;
using System.Collections.Generic;
using TbdParser.Models;

namespace TbdParser.Parsing
{
    /// <summary>
    /// Parser for JSON-based TBD format (version 5+)
    /// Note: This is a placeholder for future implementation
    /// </summary>
    public class JsonTbdFormatParser : ITbdFormatParser
    {
        private readonly int _verbosity;
        /// <summary>
        /// Creates a new JSON TBD format parser
        /// </summary>
        public JsonTbdFormatParser(int verbosity)
        {
            _verbosity = verbosity;
        }

        public bool CanParse(string[] lines)
        {
            if (_verbosity > 1)
                Console.WriteLine("JSON format TBD parsing is not yet implemented");

            // This is a placeholder for future implementation
            // For now, we'll throw an exception since JSON parsing is not implemented yet
            throw new NotImplementedException("JSON format parsing for TBD version 5+ is not yet implemented.");
        }

        public TbdFile Parse(string[] lines)
        {
            if (_verbosity > 1)
                Console.WriteLine("JSON format TBD parsing is not yet implemented");

            // This is a placeholder for future implementation
            // For now, we'll throw an exception since JSON parsing is not implemented yet
            throw new NotImplementedException("JSON format parsing for TBD version 5+ is not yet implemented.");
        }
    }
}
