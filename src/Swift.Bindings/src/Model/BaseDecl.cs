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
        public required string Name { get; set; }
    }
}
