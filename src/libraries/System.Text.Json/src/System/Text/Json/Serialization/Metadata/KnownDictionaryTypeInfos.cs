// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Json.Serialization.Converters;

namespace System.Text.Json.Serialization.Metadata
{
    /// <summary>
    /// todo
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TValue"></typeparam>
    public static class KnownDictionaryTypeInfos<TKey, TValue> where TKey : notnull
    {
        private static JsonCollectionTypeInfo<Dictionary<TKey, TValue>>? s_dictionaryOfTKeyTValue;
        /// <summary>
        /// todo
        /// </summary>
        // TODO: Should this return JsonCollectionTypeInfo<T>?
        public static JsonCollectionTypeInfo<Dictionary<TKey, TValue>> GetDictionary(JsonTypeInfo elementInfo, JsonSerializerContext context)
            // TODO: We should also be passing the typeInfo for the key type.
            => s_dictionaryOfTKeyTValue ??= new JsonCollectionTypeInfo<Dictionary<TKey, TValue>>(
                CreateDictionary,
                new DictionaryOfTKeyTValueConverter<Dictionary<TKey, TValue>, TKey, TValue>(),
                elementInfo, context._options);

        private static Dictionary<TKey, TValue> CreateDictionary() => new Dictionary<TKey, TValue>();
    }
}
