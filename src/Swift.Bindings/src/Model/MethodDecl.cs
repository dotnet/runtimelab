// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a method declaration.
    /// </summary>
    public sealed record MethodDecl : BaseDecl
    {
        /// <summary>
        /// Mangled name of the method.
        /// </summary>
        public required string MangledName { get; set; }

        /// <summary>
        /// Indicates if the method requires marshalling.
        /// </summary>
        public required bool RequireMarshalling { get; set; }

        /// <summary>
        /// Indicates if the method is a static method.
        /// </summary>
        public required bool IsStatic { get; set; }

        /// <summary>
        /// Signature of the method.
        /// </summary>
        public required List<TypeDecl> Signature { get; set; }
    }
}
