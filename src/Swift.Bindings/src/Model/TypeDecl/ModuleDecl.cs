// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a module declaration.
    /// </summary>
    public sealed record ModuleDecl : BaseDecl
    {
        /// <summary>
        /// The module's properties.
        /// </summary>
        public required List<PropertyDecl> Properties { get; set; }

        /// <summary>
        /// The module's methods.
        /// </summary>
        public required List<MethodDecl> Methods { get; set; }

        /// <summary>
        /// The module's type declarations.
        /// </summary>
        public required List<TypeDecl> Types { get; set; }

        // <summary>
        // The module's `using` dependencies.
        // </summary>
        public required List<string> Dependencies { get; set; }
    }
}
