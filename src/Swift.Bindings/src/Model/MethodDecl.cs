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
        /// Mangled name of the declaration.
        /// </summary>
        public required string MangledName { get; set; }

        /// <summary>
        /// Indicates if the method is a static method.
        /// </summary>
        public required MethodType MethodType { get; set; }

        /// <summary>
        /// Indicates if the method is a constructor.
        /// </summary>
        public required bool IsConstructor { get; set; }

        /// <summary>
        /// Signature of the method.
        /// </summary>
        public required List<ArgumentDecl> CSSignature { get; set; }
    }

    /// <summary>
    /// Represents a method type.
    /// </summary>
    public enum MethodType
    {
        /// <summary>
        /// Indicates that the method is an instance method.
        /// </summary>
        Instance,

        /// <summary>
        /// Indicates that the method is a static method.
        /// </summary>
        Static
    }
}
