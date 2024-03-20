// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Newtonsoft.Json.Linq;

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

        // <summary>
        // The module's dependencies.
        // </summary>
        public required List<string> Dependencies {get; set;}
        
        /// <summary>
        /// Methods declared within the module.
        /// </summary>
        public required List<MethodDecl> Methods { get; set; }
    }
}
