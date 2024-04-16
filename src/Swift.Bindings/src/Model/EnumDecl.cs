// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents an enum declaration.
    /// </summary>
    public sealed record EnumDecl : BaseDecl
    {
        /// <summary>
        /// Mangled name of the enum.
        /// </summary>
        public required string MangledName { get; set; }
    }
}
