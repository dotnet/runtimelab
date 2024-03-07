// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a module declaration.
    /// </summary>
    public sealed record ModuleDecl
    {
        /// <summary>
        /// Name of the module.
        /// </summary>
        public required string Name { get; init; }
        
        /// <summary>
        /// Methods declared within the module.
        /// </summary>
        public required IEnumerable<MethodDecl> Methods { get; set; }
    }
}
