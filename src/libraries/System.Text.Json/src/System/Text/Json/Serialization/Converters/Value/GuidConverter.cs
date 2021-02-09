// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Text.Json.Serialization.Converters
{
    /// <summary>
    /// todo
    /// </summary>
    public sealed class GuidConverter : JsonConverter<Guid>
    {
        /// <summary>
        /// todo
        /// </summary>
        public override Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.GetGuid();
        }

        /// <summary>
        /// todo
        /// </summary>
        public override void Write(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value);
        }

        internal override Guid ReadWithQuotes(ref Utf8JsonReader reader)
        {
            return reader.GetGuidNoValidation();
        }

        internal override void WriteWithQuotes(Utf8JsonWriter writer, Guid value, JsonSerializerOptions options, ref WriteStack state)
        {
            writer.WritePropertyName(value);
        }
    }
}
