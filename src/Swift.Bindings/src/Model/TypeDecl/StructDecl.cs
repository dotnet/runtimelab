// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a struct declaration.
    /// </summary>
    public sealed record StructDecl : TypeDecl
    {
        public required bool IsBlittable { get; set; }

        public required bool IsFrozen { get; set; }
    }
}
