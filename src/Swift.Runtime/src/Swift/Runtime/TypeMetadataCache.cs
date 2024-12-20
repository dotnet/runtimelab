// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Swift.Runtime;

/// <summary>
/// Internal implementation of ITypeMetadataCache
/// </summary>
internal class TypeMetadataCache : ITypeMetadataCache
{
    readonly ConcurrentDictionary<Type, TypeMetadata> cache = new();

    /// <summary>
    /// Constructs an empty cache.
    /// </summary>
    public TypeMetadataCache()
    {

    }

    /// <summary>
    /// Constructs a cache with the supplied initial values.
    /// </summary>
    /// <param name="initialValues">An enumeration of tuples of Type and TypeMetadata to initialize the cache</param>
    public TypeMetadataCache(IEnumerable<(Type, TypeMetadata)> initialValues)
    {
        var dictCache = (IDictionary<Type, TypeMetadata>)cache;
        foreach (var (key, value) in initialValues)
        {
            dictCache.Add(key, value);
        }
    }

    /// <summary>
    /// Returns true if the cache contains an entry for type and sets metadata to the resulting value,
    /// otherwise it returns false and metadata will be null.
    /// </summary>
    /// <param name="type">The type to look up in the cache</param>
    /// <param name="metadata">The resulting metadata if found</param>
    /// <returns>true if the lookup was successful, false otherwise</returns>
    public bool TryGet(Type type, [NotNullWhen(true)] out TypeMetadata? metadata)
    {
        if (cache.TryGetValue(type, out var md))
        {
            metadata = md;
            return true;
        }
        else
        {
            metadata = null;
            return false;
        }
    }

    /// <summary>
    /// Gets the TypeMetadata for the given Type type or if it is not present,
    /// adds it to the cache using the given factory to generate the value.
    /// </summary>
    /// <param name="type">The type to look up in the cache</param>
    /// <param name="metadataFactory">a factory to generate the TypeMetadata if not present</param>
    /// <returns>The TypeMetadata associated with the give Type type</returns>
    public TypeMetadata GetOrAdd(Type type, Func<Type, TypeMetadata> metadataFactory)
    {
        return cache.GetOrAdd(type, metadataFactory);
    }
}
