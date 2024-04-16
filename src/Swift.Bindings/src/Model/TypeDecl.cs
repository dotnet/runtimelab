// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a type declaration.
    /// </summary>
    public sealed record TypeDecl : BaseDecl
    {
        /// <summary>
        /// Type identifier.
        /// </summary>
        public required string TypeIdentifier { get; set; }

        /// <summary>
        /// Generics of the type.
        /// </summary>
        public required List<TypeDecl> Generics { get; set; }
    }
}
