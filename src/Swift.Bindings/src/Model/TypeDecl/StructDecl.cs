// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a struct declaration.
    /// </summary>
    public sealed record StructDecl : TypeDecl
    {
        /// <summary>
        /// Whether the struct is frozen.
        /// </summary>
        public required bool IsFrozen { get; set; }

        /// <summary>
        /// Protocol conformances.
        /// </summary>
        public required List<TypeConformance> Conformances { get; set; }

        /// <summary>
        /// Metadata accessor.
        /// </summary>
        public required string MetadataAccessor { get; set; }
    }
}
