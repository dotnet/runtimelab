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
        /// The module's fields.
        /// </summary>
        public required List<FieldDecl> Fields { get; set; }

        /// <summary>
        /// The module's methods.
        /// </summary>
        public required List<MethodDecl> Methods { get; set; }

        /// <summary>
        /// Declarations within the base declaration.
        /// </summary>
        public required List<BaseDecl> Declarations { get; set; }

        // <summary>
        // The module's `using` dependencies.
        // </summary>
        public required List<string> Dependencies { get; set; }
    }
}
