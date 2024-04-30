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
        /// Struct fields.
        /// </summary>
        public required List<FieldDecl> Fields { get; set; }

        /// <summary>
        /// Declarations within the base declaration.
        /// </summary>
        public required List<BaseDecl> Declarations { get; set; }
    }
}
