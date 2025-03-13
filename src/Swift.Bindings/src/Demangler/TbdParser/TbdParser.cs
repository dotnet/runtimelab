// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TbdParsing.Logging;
using TbdParsing.Models;
using TbdParsing.Parsing;

namespace TbdParsing
{
    /// <summary>
    /// Parser for Text-Based Dynamic Library (TBD) files
    /// </summary>
    public class TbdParser
    {
        private readonly List<ITbdFormatParser> _formatParsers;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the TBD parser with a default logger
        /// </summary>
        public TbdParser() : this(new ConsoleLogger { MinimumLevel = LogLevel.Info })
        {
        }

        /// <summary>
        /// Initializes a new instance of the TBD parser with a specified logger
        /// </summary>
        public TbdParser(ILogger logger)
        {
            _logger = logger ?? NullLogger.Instance;

            // Register all format parsers
            _formatParsers = new List<ITbdFormatParser>
            {
                new YamlLikeTbdFormatParser(_logger),
                // new JsonTbdFormatParser(_logger) // TODO: Parser of TBD in JSON format is not implemented yet
            };

            _logger.Debug("TBD Parser initialized with parsers: " + string.Join(", ",
                _formatParsers.Select(p => p.GetType().Name)));
        }

        /// <summary>
        /// Parse a TBD file and return a TbdFile object
        /// </summary>
        public TbdFile ParseFile(string filePath)
        {
            _logger.Info($"Parsing file: {filePath}");

            if (!File.Exists(filePath))
            {
                _logger.Error($"File not found: {filePath}");
                throw new FileNotFoundException($"TBD file not found: {filePath}");
            }

            // Read all lines from the file
            _logger.Debug("Reading file content");
            string[] lines = File.ReadAllLines(filePath);
            _logger.Debug($"Read {lines.Length} lines");

            // Find a parser that can handle this format
            _logger.Debug("Detecting file format...");
            ITbdFormatParser? parser = _formatParsers.FirstOrDefault(p => p.CanParse(lines));

            if (parser == null)
            {
                _logger.Error("Could not determine TBD file format");
                throw new ParsingException("Unsupported TBD file format. Could not determine the format version.");
            }

            _logger.Info($"Detected format: {parser.GetType().Name}");

            try
            {
                _logger.Debug("Beginning parse operation");
                TbdFile result = parser.Parse(lines);
                _logger.Info($"Successfully parsed TBD file version {result.Version} with {result.Exports.Count} exports");
                return result;
            }
            catch (NotImplementedException ex)
            {
                _logger.Error("Format detected but parsing not implemented", ex);
                throw new ParsingException($"Format detected but parsing is not yet implemented: {ex.Message}", ex);
            }
            catch (Exception ex) when (!(ex is ParsingException))
            {
                _logger.Error("Error parsing TBD file", ex);
                throw new ParsingException($"Error parsing TBD file: {ex.Message}", ex);
            }
        }
    }
}
