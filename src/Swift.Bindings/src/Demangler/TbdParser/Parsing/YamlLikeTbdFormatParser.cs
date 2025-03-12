using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TbdParser.Models;

namespace TbdParser.Parsing
{
    /// <summary>
    /// Parser for YAML-like TBD format (versions 1-4)
    /// </summary>
    public class YamlLikeTbdFormatParser : ITbdFormatParser
    {
        private readonly int _verbosity;
        /// <summary>
        /// Creates a new YAML-like TBD format parser
        /// </summary>
        public YamlLikeTbdFormatParser(int verbosity)
        {
            _verbosity = verbosity;
        }

        public bool CanParse(string[] lines)
        {
            if (lines == null || lines.Length == 0)
            {
                if (_verbosity > 2)
                    Console.WriteLine("Cannot parse empty file");

                return false;
            }

            // YAML-like format typically starts with "--- !tapi-tbd"
            string firstLine = lines[0].Trim();
            if (firstLine != "--- !tapi-tbd")
            {
                if (_verbosity > 2)
                    Console.WriteLine($"First line does not contain \"--- !tapi-tbd\"");

                return false;
            }

            return true;
        }

        public TbdFile Parse(string[] lines)
        {
            if (_verbosity > 2)
                Console.WriteLine("Starting YAML-like TBD format parsing");

            var tbdFile = new TbdFile();
            int lineIndex = 0;

            // Skip the YAML document marker if present
            if (lineIndex < lines.Length && lines[lineIndex].Trim() == "--- !tapi-tbd")
            {
                if (_verbosity > 2)
                    Console.WriteLine("Skipping YAML document marker");

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

                // Parse key-value pairs
                int colonPos = line.IndexOf(':');
                if (colonPos == -1)
                {
                    if (_verbosity > 1)
                        Console.WriteLine($"Line {lineIndex}: Expected key-value pair but no colon found: '{line}'");

                    continue;
                }

                string key = line.Substring(0, colonPos).Trim();
                string value = line.Substring(colonPos + 1).Trim();
                if (_verbosity > 2)
                    Console.WriteLine($"Found key-value pair: {key} = {value}");

                switch (key)
                {
                    case "tbd-version":
                        try
                        {
                            tbdFile.Version = int.Parse(value);
                            if (_verbosity > 2)
                                Console.WriteLine($"Parsed tbd-version = {tbdFile.Version}");
                        }
                        catch (FormatException)
                        {
                            if (_verbosity > 1)
                                Console.WriteLine($"Failed to parse tbd-version: {value}");
                        }
                        break;

                    case "install-name":
                        tbdFile.InstallName = value.Trim('\'', '"');
                        if (_verbosity > 2)
                            Console.WriteLine($"Parsed install-name = {tbdFile.InstallName}");
                        break;

                    case "swift-abi-version":
                        try
                        {
                            tbdFile.SwiftAbiVersion = int.Parse(value);
                            if (_verbosity > 2)
                                Console.WriteLine($"Parsed swift-abi-version = {tbdFile.SwiftAbiVersion}");
                        }
                        catch (FormatException)
                        {
                            if (_verbosity > 1)
                                Console.WriteLine($"Failed to parse swift-abi-version: {value}");
                        }
                        break;

                    case "targets":
                        tbdFile.Targets = ParseArray(value);
                        if (_verbosity > 2)
                            Console.WriteLine($"Parsed {tbdFile.Targets.Count} targets");
                        break;

                    case "exports":
                        if (_verbosity > 2)
                            Console.WriteLine($"Starting exports section parsing at line {lineIndex}");

                        tbdFile.Exports = ParseExports(lines, ref lineIndex);

                        if (_verbosity > 2)
                            Console.WriteLine($"Parsed {tbdFile.Exports.Count} export entries");
                        break;

                    default:
                        if (_verbosity > 1)
                            Console.WriteLine($"Unknown top-level key: {key}");
                        break;
                }
            }

            if (_verbosity > 2)
                Console.WriteLine("Completed YAML-like TBD format parsing");

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
            if (value.StartsWith("[") && value.EndsWith("]"))
            {
                string content = value.Substring(1, value.Length - 2).Trim();

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
                if (_verbosity > 1)
                    Console.WriteLine($"Invalid array format: {value}");
            }

            return items;
        }

        /// <summary>
        /// Split array items respecting quotes
        /// </summary>
        private List<string> SplitArrayItems(string content)
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
                    result.Add(content.Substring(start, i - start).Trim());
                    start = i + 1;
                }
            }

            // Add the last item
            if (start < content.Length)
                result.Add(content.Substring(start).Trim());

            return result;
        }

        /// <summary>
        /// Parse the exports section which has a nested structure
        /// </summary>
        private List<ExportEntry> ParseExports(string[] lines, ref int lineIndex)
        {
            var exports = new List<ExportEntry>();
            ExportEntry? currentExport = null;
            int baseIndentation = -1;
            int exportEntryIndentation = -1;
            bool insideMultilineArray = false;
            StringBuilder? multilineArrayBuilder = null;
            string currentArrayType = string.Empty;

            if (_verbosity > 2)
                Console.WriteLine("Parsing exports section");
            while (lineIndex < lines.Length)
            {
                string rawLine = lines[lineIndex];

                // Get indentation level before trimming
                int indentation = GetIndentation(rawLine);
                string line = rawLine.Trim();

                // If we haven't determined base indentation yet, set it now
                if (baseIndentation == -1)
                {
                    baseIndentation = indentation;
                    if (_verbosity > 2)
                        Console.WriteLine($"Base indentation set to {baseIndentation}");
                }

                // If we're back at a lower indentation than the exports level,
                // we've exited the exports section
                if (indentation < baseIndentation && currentExport != null && !insideMultilineArray)
                {
                    if (_verbosity > 2)
                        Console.WriteLine($"Exiting exports section at line {lineIndex}, indentation {indentation} < base {baseIndentation}");
                    break;
                }

                // New export entry starts with "- targets:"
                if (line.StartsWith("- targets:"))
                {
                    exportEntryIndentation = indentation;
                    currentExport = new ExportEntry();
                    exports.Add(currentExport);

                    // Parse the targets on this line
                    string targetsValue = line.Substring("- targets:".Length).Trim();
                    currentExport.Targets = ParseArray(targetsValue);
                    if (_verbosity > 2)
                        Console.WriteLine($"Found new export entry at line {lineIndex} with {currentExport.Targets.Count} targets");

                    lineIndex++;
                    continue;
                }

                // If we have a current export and we're handling a potential property line
                if (currentExport != null && !insideMultilineArray)
                {
                    // Check for property keys (symbols: or objc-classes:)
                    if (line.StartsWith("symbols:"))
                    {
                        string symbolsValue = line.Substring("symbols:".Length).Trim();

                        if (symbolsValue.StartsWith("[") && !symbolsValue.EndsWith("]"))
                        {
                            // Start of multi-line array format
                            insideMultilineArray = true;
                            currentArrayType = "symbols";
                            multilineArrayBuilder = new StringBuilder("[");
                            multilineArrayBuilder.Append(symbolsValue.Substring(1));
                            lineIndex++;
                            continue;
                        }
                        else
                        {
                            // Single line array format
                            currentExport.Symbols = ParseArrayOfSymbols(symbolsValue);
                            if (_verbosity > 2)
                                Console.WriteLine($"Parsed {currentExport.Symbols.Count} inline symbols");
                        }
                    }
                    else if (line.StartsWith("objc-classes:"))
                    {
                        string objcClassesValue = line.Substring("objc-classes:".Length).Trim();

                        if (objcClassesValue.StartsWith("[") && !objcClassesValue.EndsWith("]"))
                        {
                            // Start of multi-line array format
                            insideMultilineArray = true;
                            currentArrayType = "objc-classes";
                            multilineArrayBuilder = new StringBuilder("[");
                            multilineArrayBuilder.Append(objcClassesValue.Substring(1));
                            lineIndex++;
                            continue;
                        }
                        else
                        {
                            // Single line array format
                            currentExport.ObjcClasses = ParseArray(objcClassesValue);
                            if (_verbosity > 2)
                                Console.WriteLine($"Parsed {currentExport.ObjcClasses.Count} inline objc-classes");
                        }
                    }
                    else
                    {
                        if (_verbosity > 1)
                            Console.WriteLine($"Unknown export property at line {lineIndex}: {line}");
                    }
                }
                // Track if we're inside a multi-line array by watching for the ending bracket
                else if (insideMultilineArray)
                {
                    // Append this line to the array builder
                    multilineArrayBuilder!.Append(" ").Append(line);

                    // Check if we've found the closing bracket
                    if (line.Contains("]"))
                    {
                        // We've reached the end of the multi-line array
                        insideMultilineArray = false;

                        // Get the complete array value including brackets
                        string arrayValue = multilineArrayBuilder.ToString();

                        // Determine which export property we're filling based on the array type
                        if (currentArrayType == "symbols")
                        {
                            currentExport!.Symbols = ParseArrayOfSymbols(arrayValue);
                            if (_verbosity > 2)
                                Console.WriteLine($"Parsed {currentExport.Symbols.Count} symbols from multi-line array");
                        }
                        else if (currentArrayType == "objc-classes")
                        {
                            currentExport!.ObjcClasses = ParseArray(arrayValue);
                            if (_verbosity > 2)
                                Console.WriteLine($"Parsed {currentExport.ObjcClasses.Count} objc-classes from multi-line array");
                        }

                        multilineArrayBuilder = null;
                        currentArrayType = string.Empty;
                    }

                    lineIndex++;
                    continue;
                }
                else
                {
                    if (_verbosity > 1)
                        Console.WriteLine($"Unexpected export content at line {lineIndex}: {line}");
                }

                lineIndex++;
            }

            return exports;
        }

        /// <summary>
        /// Parse an array of symbols and categorize them
        /// </summary>
        private List<Symbol> ParseArrayOfSymbols(string value)
        {
            List<string> parsedArray = ParseArray(value);
            var symbols = new List<Symbol>();
            foreach (var item in parsedArray)
            {
                symbols.Add(new Symbol(item));
            }
            return symbols;
        }

        /// <summary>
        /// Parse a list of symbols from multi-line format
        /// </summary>
        private List<Symbol> ParseSymbolsList(string[] lines, ref int lineIndex, int expectedIndentation)
        {
            var items = new List<Symbol>();
            bool foundAnyItem = false;
            int actualIndentation = -1;

            while (lineIndex < lines.Length)
            {
                string rawLine = lines[lineIndex];
                int indentation = GetIndentation(rawLine);
                string line = rawLine.Trim();

                // Skip blank lines and comments
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                {
                    lineIndex++;
                    continue;
                }

                // If this is the first item, capture its actual indentation
                if (!foundAnyItem && line.StartsWith("-"))
                {
                    foundAnyItem = true;
                    actualIndentation = indentation;
                }

                // If we've found items before and now the indentation is less than the actual
                // indentation of items, we've exited the list
                if (foundAnyItem && indentation < actualIndentation)
                {
                    break;
                }

                // Parse symbol (should start with dash)
                if (line.StartsWith("-"))
                {
                    string symbolValue = line.Substring(1).Trim().Trim('\'', '"');
                    items.Add(new Symbol(symbolValue));
                    foundAnyItem = true;
                }
                else if (foundAnyItem)
                {
                    // If we're here, this line might be a continuation of the previous item
                    // or it could be the start of a new section
                    if (_verbosity > 2)
                        Console.WriteLine($"Found non-dash line after items at line {lineIndex}, assuming end of list: {line}");
                    break;
                }
                else
                {
                    if (_verbosity > 1)
                        Console.WriteLine($"Expected list item at line {lineIndex} but found: {line}");
                    break;
                }

                lineIndex++;
            }

            return items;
        }

        /// <summary>
        /// Get the indentation level (number of leading spaces)
        /// </summary>
        private int GetIndentation(string line)
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
