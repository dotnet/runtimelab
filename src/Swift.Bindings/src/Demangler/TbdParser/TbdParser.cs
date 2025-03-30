// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
        /// Initializes a new instance of the TBD parser with a specified logger
        /// </summary>
        public TbdParser(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<TbdParser>();

            // Register all format parsers
            _formatParsers = new List<ITbdFormatParser>
            {
                new YamlLikeTbdFormatParser(loggerFactory.CreateLogger<YamlLikeTbdFormatParser>()),
                // new JsonTbdFormatParser(loggerFactory.CreateLogger<JsonTbdFormatParser>()) // TODO: Parser of TBD in JSON format is not implemented yet
            };

            _logger.LogDebug("TBD Parser initialized with parsers: {Parsers}", string.Join(", ",
                _formatParsers.Select(p => p.GetType().Name)));
        }

        /// <summary>
        /// Parse a TBD file and return a TbdFile object
        /// </summary>
        public TbdFile ParseFile(string filePath)
        {
            _logger.LogInformation("Parsing file {FilePath}:", filePath);

            if (!File.Exists(filePath))
            {
                _logger.LogError("File not found {FilePath}:", filePath);
                throw new FileNotFoundException($"TBD file not found: {filePath}");
            }

            // Read all lines from the file
            _logger.LogDebug("Reading file content");
            string[] lines = File.ReadAllLines(filePath);
            _logger.LogDebug("Read {NumberOfLines} lines", lines.Length);

            // Find a parser that can handle this format
            _logger.LogDebug("Detecting file format...");
            ITbdFormatParser? parser = _formatParsers.FirstOrDefault(p => p.CanParse(lines));

            if (parser == null)
            {
                _logger.LogError("Could not determine TBD file format");
                throw new ParsingException("Unsupported TBD file format. Could not determine the format version.");
            }

            _logger.LogInformation("Detected format: {TbdFormat}", parser.GetType().Name);

            try
            {
                _logger.LogDebug("Beginning parse operation");
                TbdFile result = parser.Parse(lines);
                _logger.LogDebug("Successfully parsed TBD file version {TbdFileVersion} with {NumberOfExports} exports", result.Version, result.Exports.Count);
                return result;
            }
            catch (NotImplementedException ex)
            {
                _logger.LogError("Format detected but parsing not implemented {Exception}", ex);
                throw new ParsingException($"Format detected but parsing is not yet implemented: {ex.Message}", ex);
            }
            catch (Exception ex) when (!(ex is ParsingException))
            {
                _logger.LogError("Error parsing TBD file {Exception}", ex);
                throw new ParsingException($"Error parsing TBD file: {ex.Message}", ex);
            }
        }
    }
}
