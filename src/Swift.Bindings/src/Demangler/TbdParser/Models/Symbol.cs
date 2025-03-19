// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace TbdParsing.Models
{
    /// <summary>
    /// Represents the type of a symbol in a TBD file
    /// </summary>
    public enum SymbolType
    {
        /// <summary>
        /// A Swift symbol (starts with _$s)
        /// </summary>
        Swift,

        /// <summary>
        /// An Objective-C symbol (starts with underscore)
        /// </summary>
        ObjectiveC,

        /// <summary>
        /// Other symbol that doesn't match specific patterns
        /// </summary>
        Other
    }

    /// <summary>
    /// Represents a symbol in a TBD file
    /// </summary>
    public class Symbol
    {
        /// <summary>
        /// The name of the symbol as it appears in the TBD file
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The type of the symbol based on its prefix
        /// </summary>
        public SymbolType Type { get; }

        /// <summary>
        /// Creates a new Symbol instance
        /// </summary>
        /// <param name="name">The symbol name</param>
        public Symbol(string name)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = DetermineSymbolType(name);
        }

        private static SymbolType DetermineSymbolType(string name)
        {
            if (name.StartsWith("_$s"))
                return SymbolType.Swift;

            if (name.StartsWith('_') && !name.StartsWith("_$"))
                return SymbolType.ObjectiveC;

            return SymbolType.Other;
        }

        /// <summary>
        /// Returns a string representation of this symbol
        /// </summary>
        public override string ToString()
        {
            return $"{Name} ({Type})";
        }
    }
}
