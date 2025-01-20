// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a base declaration.
    /// </summary>
    public record BaseDecl
    {
        /// <summary>
        /// Name of the declaration.
        /// </summary>
        public required string Name { get; set; } //TODO: Hide or remove this property. This might not contain a correct name.

        /// <summary>
        /// The fully qualified name of the declaration used for type registration.
        /// </summary>
        public required string FullyQualifiedName { get; set; }

        /// <summary>
        /// Fully qualified name of the declaration without the module name.
        /// </summary>
        public string FullyQualifiedNameWithoutModule => FullyQualifiedName.IndexOf('.') >= 0 ? FullyQualifiedName.Substring(FullyQualifiedName.IndexOf('.') + 1) : FullyQualifiedName;

        /// <summary>
        /// The parent declaration.
        /// </summary>
        public required BaseDecl? ParentDecl { get; set; }

        /// <summary>
        /// The module declaration.
        /// </summary>
        public required ModuleDecl? ModuleDecl { get; set; }
    }
}
