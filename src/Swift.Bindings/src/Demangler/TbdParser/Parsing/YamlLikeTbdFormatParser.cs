// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using TbdParsing.Models;

namespace TbdParsing.Parsing
{
    /// <summary>
    /// Parser for YAML-like TBD format (versions 1-4)
    /// </summary>
    public class YamlLikeTbdFormatParser : TbdFormatParserBase
    {
        /// <summary>
        /// Creates a new YAML-like TBD format parser
        /// </summary>
        public YamlLikeTbdFormatParser(ILogger logger) : base(logger)
        {
        }
        public override bool CanParse(string[] lines)
        {
            if (lines == null || lines.Length == 0)
            {
                _logger.LogDebug("Cannot parse empty file");
                return false;
            }

            // YAML-like format typically starts with "--- !tapi-tbd"
            string firstLine = lines[0].Trim();
            if (firstLine != "--- !tapi-tbd")
            {
                _logger.LogDebug("First line does not contain \"--- !tapi-tbd\"");
                return false;
            }

            return true;
        }

        public override TbdFile Parse(string[] lines)
        {
            _logger.LogDebug("Starting YAML-like TBD format parsing");
            var tbdFile = new TbdFile();
            int lineIndex = 0;

            // Skip the YAML document marker if present
            if (lineIndex < lines.Length && lines[lineIndex].Trim() == "--- !tapi-tbd")
            {
                _logger.LogDebug("Skipping YAML document marker");
                lineIndex++;
            }

            // Parse top-level key-value pairs
            while (lineIndex < lines.Length)
            {
                string line = lines[lineIndex].Trim();
                lineIndex++;

                // Skip blank lines and comments
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    continue;
                }

                if (line == "...")
                {
                    _logger.LogDebug("TBD end marker found (...)");
                    break;
                }

                // Parse key-value pairs
                KeyValuePair<string, string> kvp;
                try
                {
                    kvp = ParseKeyValuePair(line);
                }
                catch (FormatException ex)
                {
                    // TODO: We might not support all top-level keys yet
                    _logger.LogWarning($"Line {lineIndex}: {ex.Message}");
                    continue;
                }
                _logger.LogDebug($"Found top-level key-value pair: {kvp.Key} = {kvp.Value}");

                switch (kvp.Key)
                {
                    case "tbd-version":
                        try
                        {
                            tbdFile.Version = int.Parse(kvp.Value);
                            _logger.LogDebug($"Parsed tbd-version = {tbdFile.Version}");
                        }
                        catch (FormatException)
                        {
                            _logger.LogWarning($"Failed to parse tbd-version: {kvp.Value}");
                        }
                        break;

                    case "install-name":
                        tbdFile.InstallName = kvp.Value.Trim('\'', '"');
                        _logger.LogDebug($"Parsed install-name = {tbdFile.InstallName}");
                        break;

                    case "swift-abi-version":
                        try
                        {
                            tbdFile.SwiftAbiVersion = int.Parse(kvp.Value);
                            _logger.LogDebug($"Parsed swift-abi-version = {tbdFile.SwiftAbiVersion}");
                        }
                        catch (FormatException)
                        {
                            _logger.LogWarning($"Failed to parse swift-abi-version: {kvp.Value}");
                        }
                        break;

                    case "targets":
                        tbdFile.Targets = ParseMultiLineArray(lines, ref lineIndex, kvp.Value);
                        _logger.LogDebug($"Parsed {tbdFile.Targets.Count} targets: [{string.Join(", ", tbdFile.Targets)}]");
                        break;

                    case "exports":
                        _logger.LogDebug($"Starting exports section parsing at line {lineIndex}");
                        tbdFile.Exports = ParseExports(lines, ref lineIndex);
                        _logger.LogDebug($"Parsed {tbdFile.Exports.Count} export entries");
                        break;

                    default:
                        _logger.LogWarning($"Unknown top-level key: {kvp.Key}");
                        break;
                }
            }

            _logger.LogDebug("Completed YAML-like TBD format parsing");
            return tbdFile;
        }

        /// <summary>
        /// Parse an array of strings in the format [ item1, item2, item3 ]
        /// </summary>
        private List<string> ParseArray(string value)
        {
            var items = new List<string>();

            // If the array is empty
            if (string.IsNullOrWhiteSpace(value) || value == "[]")
            {
                return items;
            }

            // Handle array format like [ item1, item2, item3 ]
            if (value.StartsWith('[') && value.EndsWith(']'))
            {
                string content = value[1..^1].Trim();

                // Split by comma, but handle commas within quoted strings
                var splitItems = SplitArrayItems(content);
                foreach (var item in splitItems)
                {
                    string trimmedItem = item.Trim().Trim('\'', '"');
                    if (!string.IsNullOrWhiteSpace(trimmedItem))
                    {
                        items.Add(trimmedItem);
                    }
                }
            }
            else
            {
                _logger.LogWarning($"Invalid array format: {value}");
            }

            return items;
        }

        /// <summary>
        /// Parse an array of strings spanning over multiple lines
        /// </summary>
        private List<string> ParseMultiLineArray(string[] lines, ref int lineIndex, string initialValue)
        {
            var items = new List<string>();

            // If the array is empty
            if (string.IsNullOrWhiteSpace(initialValue) || initialValue == "[]")
            {
                return items;
            }

            // If the array is already complete on the first line
            if (initialValue.StartsWith('[') && initialValue.EndsWith(']'))
            {
                _logger.LogDebug($"Single line array encountered, falling back to ParseArray");
                return ParseArray(initialValue);
            }

            // If the array starts but doesn't end on the first line
            if (initialValue.StartsWith('[') && !initialValue.EndsWith(']'))
            {
                StringBuilder arrayBuilder = new StringBuilder(initialValue);

                // Keep reading lines until we find the closing bracket
                while (lineIndex < lines.Length)
                {
                    string nextLine = lines[lineIndex].Trim();
                    lineIndex++;

                    // If this is a new section or entry, we probably encountered a malformed array
                    if (nextLine.StartsWith('-') || nextLine.Contains(':'))
                    {
                        _logger.LogWarning($"Array does not have a closing bracket before new section at line {lineIndex} with content: {nextLine}");
                        break;
                    }

                    arrayBuilder.Append(' ').Append(nextLine);

                    // Check if this line contains the closing bracket
                    if (nextLine.Contains(']'))
                    {
                        break;
                    }
                }

                // Parse the complete array string
                return ParseArray(arrayBuilder.ToString());
            }

            // If we got here, the input wasn't an array
            _logger.LogWarning($"Expected array format but found: {initialValue}");
            return items;
        }

        /// <summary>
        /// Parse the exports section which has a nested structure
        /// </summary>
        private List<ExportEntry> ParseExports(string[] lines, ref int lineIndex)
        {
            var exports = new List<ExportEntry>();
            ExportEntry? currentExport = null;
            int baseIndentation = -1;

            _logger.LogDebug("Parsing exports section");
            while (lineIndex < lines.Length)
            {
                string rawLine = lines[lineIndex];
                // Get indentation level before trimming
                int indentation = GetIndentation(rawLine);
                string line = rawLine.Trim();
                lineIndex++;

                // If we haven't determined base indentation yet, set it now
                if (baseIndentation == -1)
                {
                    baseIndentation = indentation;
                    _logger.LogDebug($"Base indentation set to {baseIndentation}");
                }

                // If we're back at a lower indentation than the exports level,
                // we've exited the exports section
                if (indentation < baseIndentation)
                {
                    _logger.LogDebug($"Exiting exports section at line {lineIndex}, indentation {indentation} < base {baseIndentation}");
                    break;
                }

                // Parse key-value pairs
                KeyValuePair<string, string> kvp;
                kvp = ParseKeyValuePair(line);
                _logger.LogDebug($"Found export key-value pair: {kvp.Key} = {kvp.Value}");

                switch (kvp.Key)
                {
                    case "- targets":
                        // Start a new export entry
                        currentExport = new ExportEntry();
                        exports.Add(currentExport);

                        // Parse the export targets
                        currentExport.Targets = ParseMultiLineArray(lines, ref lineIndex, kvp.Value);
                        _logger.LogDebug($"Found new export entry at line {lineIndex} with [{string.Join(", ", currentExport.Targets)}] targets.");

                        break;
                    case "symbols":
                        if (currentExport == null)
                        {
                            _logger.LogWarning($"Unexpected symbols entry at line {lineIndex} without a current export entry");
                            break;
                        }

                        // Parse symbols list
                        currentExport.Symbols = ConvertToSymbols(ParseMultiLineArray(lines, ref lineIndex, kvp.Value));
                        _logger.LogDebug($"Parsed {currentExport.Symbols.Count} symbols");
                        break;

                    case "objc-classes":
                        if (currentExport == null)
                        {
                            _logger.LogWarning($"Unexpected objc-classes entry at line {lineIndex} without a current export entry");
                            break;
                        }

                        // Parse objc-classes list
                        currentExport.ObjcClasses = ParseMultiLineArray(lines, ref lineIndex, kvp.Value);
                        _logger.LogDebug($"Parsed {currentExport.ObjcClasses.Count} objc-classes");
                        break;
                    case "objc-ivars":
                        if (currentExport == null)
                        {
                            _logger.LogWarning($"Unexpected objc-ivars entry at line {lineIndex} without a current export entry");
                            break;
                        }

                        // Parse objc-ivars list
                        currentExport.ObjcIvars = ParseMultiLineArray(lines, ref lineIndex, kvp.Value);
                        _logger.LogDebug($"Parsed {currentExport.ObjcIvars.Count} objc-ivars");
                        break;
                    default:
                        _logger.LogWarning($"Unknown export property at line {lineIndex}: {kvp.Key}");
                        break;
                }
            }

            return exports;
        }

        /// <summary>
        /// Split array items respecting quotes
        /// </summary>
        private static List<string> SplitArrayItems(string content)
        {
            var result = new List<string>();
            bool inQuote = false;
            int start = 0;

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];

                if (c == '\'' || c == '"')
                    inQuote = !inQuote;

                else if (c == ',' && !inQuote)
                {
                    result.Add(content[start..i].Trim());
                    start = i + 1;
                }
            }

            // Add the last item
            if (start < content.Length)
                result.Add(content[start..].Trim());

            return result;
        }


        /// <summary>
        /// Parse key-value pairs in the format "key: value"
        /// </summary>
        private static KeyValuePair<string, string> ParseKeyValuePair(string line)
        {
            int colonPos = line.IndexOf(':');
            if (colonPos == -1)
            {
                throw new FormatException($"Invalid key-value pair format: {line}");
            }

            string key = line[..colonPos].Trim();
            string value = line[(colonPos + 1)..].Trim();
            return new KeyValuePair<string, string>(key, value);
        }

        /// <summary>
        /// Convert a list of strings to a list of Symbol objects
        /// </summary>
        private static List<Symbol> ConvertToSymbols(List<string> arr)
        {
            var symbols = new List<Symbol>();
            foreach (var item in arr)
            {
                symbols.Add(new Symbol(item));
            }
            return symbols;
        }

        /// <summary>
        /// Get the indentation level (number of leading spaces)
        /// </summary>
        private static int GetIndentation(string line)
        {
            if (string.IsNullOrEmpty(line))
                return 0;

            ReadOnlySpan<char> span = line.AsSpan();
            int i = 0;
            while (i < span.Length && span[i] == ' ')
                i++;
            return i;
        }
    }
}
