// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a module declaration.
    /// </summary>
    public sealed record ModuleDecl : BaseDecl
    {
        // <summary>
        // The module's dependencies.
        // </summary>
        public required List<string> Dependencies { get; set; }
    }
}
