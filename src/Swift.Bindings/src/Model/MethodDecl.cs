// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a method declaration.
    /// </summary>
    public sealed record MethodDecl
    {
        /// <summary>
        /// Name of the method.
        /// </summary>
        public required string Name { get; init; }
        
        /// <summary>
        /// Mangled name of the method.
        /// </summary>
        public required string MangledName { get; init; }
        
        /// <summary>
        /// Indicates if the method requires marshalling.
        /// </summary>
        public required bool RequireMarshalling { get; init; }

        /// <summary>
        /// Signature of the method.
        /// </summary>
        public required List<TypeDecl> Signature { get; set; }

    }
}
