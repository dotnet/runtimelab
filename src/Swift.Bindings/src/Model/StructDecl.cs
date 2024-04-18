// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a struct declaration.
    /// </summary>
    public sealed record StructDecl : BaseDecl
    {
        /// <summary>
        /// Mangled name of the struct.
        /// </summary>
        public required string MangledName { get; set; }
    }
}
