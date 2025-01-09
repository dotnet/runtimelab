// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a field declaration.
    /// </summary>
    public record FieldDecl: BaseDecl
    {
        /// <summary>
        /// Type name.
        /// </summary>
        public required TypeDecl CSTypeIdentifier { get; set; }

        /// <summary>
        /// The TypeSpec of the declaration
        ///
        public required TypeSpec SwiftTypeSpec {get; set; }

        /// <summary>
        /// Indicates the visibility of the declaration.
        /// </summary>
        public Visibility? Visibility { get; set; }
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
