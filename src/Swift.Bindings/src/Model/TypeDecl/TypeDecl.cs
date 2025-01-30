// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a type declaration.
    /// </summary>
    public record TypeDecl : BaseDecl
    {
        /// <summary>
        /// Mangled name of the declaration.
        /// </summary>
        public required string MangledName { get; set; }

        /// <summary>
        /// Type fields.
        /// </summary>
        public required List<FieldDecl> Fields { get; set; }

        /// <summary>
        /// Methods within the base declaration.
        /// </summary>
        public required List<MethodDecl> Methods { get; set; }

        /// <summary>
        /// Types declarations within the base declaration.
        /// </summary>
        public required List<TypeDecl> Types { get; set; }
    }
}
