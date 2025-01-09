// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a Swift parser interface.
    /// </summary>
    public interface ISwiftParser
    {
        /// <summary>
        /// Gets the module name.
        /// </summary>
        public string GetModuleName();

        /// <summary>
        /// Gets the module declaration.
        /// </summary>
        public ModuleParsingResult ParseModule();

    }
}
