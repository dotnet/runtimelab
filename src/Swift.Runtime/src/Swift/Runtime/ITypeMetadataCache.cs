// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics.CodeAnalysis;

namespace Swift.Runtime;

/// <summary>
/// Represents an interface that defines how a cache for TypeMetadata should operate.
/// The implementation of this interface needs to be thread safe.
/// </summary>
public interface ITypeMetadataCache
{
    /// <summary>
    /// Returns true if the cache contains an entry for type and sets metadata to the resulting value,
    /// otherwise it returns false and metadata will be null.
    /// </summary>
    /// <param name="type">The type to look up in the cache</param>
    /// <param name="metadata">The resulting metadata if found</param>
    /// <returns>true if the lookup was successful, false otherwise</returns>
    bool TryGet(Type type, [NotNullWhen(true)]out TypeMetadata? metadata);

    /// <summary>
    /// Gets the TypeMetadata for the given Type type or if it is not present,
    /// adds it to the cache using the given factory to generate the value.
    /// </summary>
    /// <param name="type">The type to look up in the cache</param>
    /// <param name="metadataFactory">a factory to generate the TypeMetadata if not present</param>
    /// <returns>The TypeMetadata associated with the give Type type</returns>
    TypeMetadata GetOrAdd(Type type, Func<Type, TypeMetadata> metadataFactory);
}