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
        /// Name of the struct.
        /// </summary>
        public required string Name { get; set; }

        /// <summary>
        /// Declarations within the base declaration.
        /// </summary>
        public required List<BaseDecl> Declarations { get; set; }
    }
}
