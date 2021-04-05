// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json.Serialization.Metadata
{
    /// <summary>
    /// todo
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal sealed class JsonValueInfo<T> : JsonTypeInfo<T>
    {
        /// <summary>
        /// todo
        /// </summary>
        /// <param name="options"></param>
        /// <param name="converter"></param>
        public JsonValueInfo(JsonSerializerOptions options, JsonConverter converter) : base(typeof(T), options, ClassType.Value)
        {
            Converter = converter;
            PropertyInfoForTypeInfo = SourceGenCreatePropertyInfoForTypeInfo<T>(Type, runtimeTypeInfo: this, converter, Options);
        }
    }
}
