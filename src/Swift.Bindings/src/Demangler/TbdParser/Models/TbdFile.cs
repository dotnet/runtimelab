// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace TbdParsing.Models
{
    /// <summary>
    /// Represents a TBD file
    /// </summary>
    public class TbdFile
    {
        /// <summary>
        /// Version of the TBD format
        /// </summary>
        public int Version { get; set; }

        /// <summary>
        /// List of targets supported by the library
        /// </summary>
        public List<string> Targets { get; set; } = new List<string>();

        /// <summary>
        /// Installation path for the library
        /// </summary>
        public string InstallName { get; set; } = string.Empty;

        /// <summary>
        /// Swift ABI version
        /// </summary>
        public int SwiftAbiVersion { get; set; }

        /// <summary>
        /// List of export entries
        /// </summary>
        public List<ExportEntry> Exports { get; set; } = new List<ExportEntry>();
    }

    /// <summary>
    /// Represents an export entry in a TBD file
    /// </summary>
    public class ExportEntry
    {
        /// <summary>
        /// List of targets for the export entry
        /// </summary>
        public List<string> Targets { get; set; } = new List<string>();

        /// <summary>
        /// List of categorized symbols in the export entry
        /// </summary>
        public List<Symbol> Symbols { get; set; } = new List<Symbol>();

        /// <summary>
        /// List of Objective-C classes in the export entry
        /// </summary>
        public List<string> ObjcClasses { get; set; } = new List<string>();

        /// <summary>
        /// List of Objective-C ivars in the export entry
        /// </summary>
        public List<string> ObjcIvars { get; set; } = new List<string>();

        /// <summary>
        /// Get Swift symbols only
        /// </summary>
        public IEnumerable<Symbol> SwiftSymbols => Symbols.Where(s => s.Type == SymbolType.Swift);

        /// <summary>
        /// Get Objective-C symbols only (excluding ObjcClasses)
        /// </summary>
        public IEnumerable<Symbol> ObjectiveCSymbols => Symbols.Where(s => s.Type == SymbolType.ObjectiveC);

        /// <summary>
        /// Get other symbols that are neither Swift nor Objective-C
        /// </summary>
        public IEnumerable<Symbol> OtherSymbols => Symbols.Where(s => s.Type == SymbolType.Other);
    }
}
