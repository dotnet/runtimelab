// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json.Serialization.Converters
{
    /// <summary>
    /// Implementation of <cref>JsonObjectConverter{T}</cref> that supports the deserialization
    /// of JSON objects using parameterized constructors.
    /// </summary>
    internal sealed class ObjectSourceGenConverter<T> : ObjectDefaultConverter<T>
    {
        internal override bool OnTryWrite(
            Utf8JsonWriter writer,
            T value,
            JsonSerializerOptions options,
            ref WriteStack state)
        {
            // SourceGenUseFastPath will not be true if JsonTypeInfo is not JsonTypeInfo<T>.
            if (!state.SourceGenUseFastPath && !state.IsContinuation)
            {
                return base.OnTryWrite(writer, value, options, ref state);
            }

            JsonTypeInfo<T> jsonTypeInfo = (JsonTypeInfo<T>)state.Current.JsonTypeInfo;
            Debug.Assert(jsonTypeInfo.SerializeObject != null);

            jsonTypeInfo.SerializeObject(writer, value!, options);
            return true;
        }
    }
}
