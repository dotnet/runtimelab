using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TbdParser.Models;
using TbdParser.Parsing;

namespace TbdParser
{
    /// <summary>
    /// Parser for Text-Based Dynamic Library (TBD) files
    /// </summary>
    public class TbdParser
    {
        private readonly List<ITbdFormatParser> _formatParsers;
        private readonly int _verbosity;

        /// <summary>
        /// Initializes a new instance of the TBD parser
        /// </summary>
        /// <param name="verbosity">The verbosity level for logging.</param>
        public TbdParser(int verbosity = 1)
        {
            _verbosity = verbosity;

            // Register all format parsers
            _formatParsers = new List<ITbdFormatParser>
            {
                new YamlLikeTbdFormatParser(verbosity),
                // JSON parser is included but will throw NotImplementedException if used
                // It's here for format detection purposes
                new JsonTbdFormatParser(verbosity)
            };

            if (_verbosity > 1)
                Console.WriteLine("TBD Parser initialized with parsers: " + string.Join(", ", _formatParsers.Select(p => p.GetType().Name)));

        }

        /// <summary>
        /// Parse a TBD file and return a TbdFile object
        /// </summary>
        public TbdFile ParseFile(string filePath)
        {
            if (_verbosity > 1)
                Console.WriteLine($"Parsing file: {filePath}");

            if (!File.Exists(filePath))
            {
                Console.Error.WriteLine($"File not found: {filePath}");
                throw new FileNotFoundException($"TBD file not found: {filePath}");
            }

            // Read all lines from the file
            if (_verbosity > 2)
                Console.WriteLine("Reading file content");

            string[] lines = File.ReadAllLines(filePath);

            if (_verbosity > 2)
                Console.WriteLine($"Read {lines.Length} lines");

            // Find a parser that can handle this format
            if (_verbosity > 2)
                Console.WriteLine("Detecting file format...");

            ITbdFormatParser? parser = _formatParsers.FirstOrDefault(p => p.CanParse(lines));

            if (parser == null)
            {
                Console.Error.WriteLine("Could not determine TBD file format");
                throw new ParsingException("Unsupported TBD file format. Could not determine the format version.");
            }

            if (_verbosity > 1)
                Console.WriteLine($"Detected format: {parser.GetType().Name}");

            try
            {
                if (_verbosity > 2)
                    Console.WriteLine("Beginning parse operation");

                TbdFile result = parser.Parse(lines);
                if (_verbosity > 1)
                    Console.WriteLine($"Successfully parsed TBD file version {result.Version} with {result.Exports.Count} exports");

                return result;
            }
            catch (NotImplementedException ex)
            {
                Console.Error.WriteLine("Format detected but parsing not implemented", ex);
                throw new ParsingException($"Format detected but parsing is not yet implemented: {ex.Message}", ex);
            }
            catch (Exception ex) when (!(ex is ParsingException))
            {
                Console.Error.WriteLine("Error parsing TBD file", ex);
                throw new ParsingException($"Error parsing TBD file: {ex.Message}", ex);
            }
        }
    }
}
