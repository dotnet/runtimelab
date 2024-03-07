// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace BindingsGeneration
{
    /// <summary>
    /// Represents a type declaration.
    /// </summary>
    public sealed record TypeDecl
    {
        /// <summary>
        /// Name of the type.
        /// </summary>
        public required string Name { get; init; }
        
        /// <summary>
        /// Fully qualified name of the type.
        /// </summary>
        public required string FullyQualifiedName { get; init; }
        
        /// <summary>
        /// Value indicating whether the type is a value type.
        /// </summary>
        public required bool IsValueType { get; init; }
        
        /// <summary>
        /// Kind of the type.
        /// </summary>
        public required TypeKind TypeKind { get; init; }
        
        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <param name="other">The object to compare with the current object.</param>
        public bool Equals(TypeDecl? other) => other != null && FullyQualifiedName == other.FullyQualifiedName;
        
        /// <summary>
        /// Default hash function.
        /// </summary>
        public override int GetHashCode() => FullyQualifiedName.GetHashCode();
    }

    /// <summary>
    /// Represents the kind of a type.
    /// </summary>
    public enum TypeKind
    {
        Named = 0,
        Tuple,
        Closure,
    }
}
