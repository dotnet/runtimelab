// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a field declaration.
    /// </summary>
    public record FieldDecl : BaseDecl
    {
        /// <summary>
        /// Type name.
        /// </summary>
        public required TypeDecl CSTypeIdentifier { get; set; }

        /// <summary>
        /// The TypeSpec of the declaration
        /// <summary>
        public required TypeSpec SwiftTypeSpec { get; set; }

        /// <summary>
        /// Indicates the visibility of the declaration.
        /// </summary>
        public Visibility? Visibility { get; set; }

        /// <summary>
        /// Indicates if the declaration is static.
        /// </summary>
        public required bool IsStatic { get; set; }
    }

    /// <summary>
    /// Represents the visibility of a declaration.
    /// </summary>
    public enum Visibility
    {
        /// <summary>
        /// Public visibility.
        /// </summary>
        Public,

        /// <summary>
        /// Private visibility.
        /// </summary>
        Private
    }
}
