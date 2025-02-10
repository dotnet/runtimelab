// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a property declaration.
    /// </summary>
    public record PropertyDecl : BaseDecl
    {
        /// <summary>
        /// The TypeSpec of the declaration
        /// <summary>
        public required TypeSpec SwiftTypeSpec { get; set; }

        /// <summary>
        /// Indicates if the declaration is static.
        /// </summary>
        public required bool IsStatic { get; set; }

        /// <summary>
        /// The accessors available for this field.
        /// </summary>
        public required IReadOnlyList<AccessorDecl> Accessors { get; init; }
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
