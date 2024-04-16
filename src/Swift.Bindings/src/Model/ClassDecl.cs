// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a struct declaration.
    /// </summary>
    public sealed record ClassDecl : BaseDecl
    {
        /// <summary>
        /// Mangled name of the class.
        /// </summary>
        public required string MangledName { get; set; }
    }
}
