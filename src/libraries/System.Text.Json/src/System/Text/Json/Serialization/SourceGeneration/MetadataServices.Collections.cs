// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization.Converters;

namespace System.Text.Json.Serialization.SourceGeneration
{
    /// <summary>
    /// TODO
    /// </summary>
    public static partial class MetadataServices
    {
        /// <summary>
        /// todo
        /// </summary>
        // TODO: Should this return JsonCollectionTypeInfo<T>?
        public static JsonTypeInfo<TElement[]> CreateArrayInfo<TElement>(
            JsonSerializerOptions options,
            JsonTypeInfo elementInfo,
            JsonNumberHandling? numberHandling)
            => new JsonCollectionInfo<TElement[]>(
                options,
                () => new List<TElement>(),
                new ArrayConverter<TElement[], TElement>(),
                elementInfo,
                numberHandling);

        /// <summary>
        /// todo
        /// </summary>
        public static JsonTypeInfo<TCollection> CreateListInfo<TCollection, TElement>(
            JsonSerializerOptions options,
            ConstructorDelegate createObjectFunc,
            JsonTypeInfo elementInfo,
            JsonNumberHandling? numberHandling)
            where TCollection : List<TElement>
            => new JsonCollectionInfo<TCollection>(
                options,
                createObjectFunc,
                new ListOfTConverter<TCollection, TElement>(),
                elementInfo,
                numberHandling);

        /// <summary>
        /// todo
        /// </summary>
        public static JsonTypeInfo<TCollection> CreateDictionaryInfo<TCollection, TKey, TValue>(
            JsonSerializerOptions options,
            ConstructorDelegate createObjectFunc,
            JsonTypeInfo keyInfo,
            JsonTypeInfo valueInfo,
            JsonNumberHandling? numberHandling)
            where TCollection : Dictionary<TKey, TValue>
            where TKey : notnull
            => new JsonCollectionInfo<TCollection>(
                options,
                createObjectFunc,
                new DictionaryOfTKeyTValueConverter<TCollection, TKey, TValue>(),
                keyInfo,
                valueInfo,
                numberHandling);
    }
}
