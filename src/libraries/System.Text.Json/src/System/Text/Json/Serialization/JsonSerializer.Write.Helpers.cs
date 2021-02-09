// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json
{
    public static partial class JsonSerializer
    {
        // Members accessed by the serializer when serializing.
        private const DynamicallyAccessedMemberTypes MembersAccessedOnWrite = DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.PublicFields;

        private static void WriteCore<TValue>(
            Utf8JsonWriter writer,
            in TValue value,
            Type inputType,
            JsonSerializerOptions options)
        {
            Debug.Assert(writer != null);

            //  We treat typeof(object) special and allow polymorphic behavior.
            if (inputType == JsonClassInfo.ObjectType && value != null)
            {
                inputType = value.GetType();
            }

            WriteStack state = default;
            JsonConverter jsonConverter = state.Initialize(inputType, options, supportContinuation: false);

            bool success = WriteCore(jsonConverter, writer, value, ref state, options);
            Debug.Assert(success);
        }

        private static bool WriteCore<TValue>(
            JsonConverter jsonConverter,
            Utf8JsonWriter writer,
            in TValue value,
            ref WriteStack state,
            JsonSerializerOptions options)
        {
            Debug.Assert(writer != null);

            bool success;

            if (jsonConverter is JsonConverter<TValue> converter)
            {
                // Call the strongly-typed WriteCore that will not box structs.
                success = converter.WriteCore(writer, value, options, ref state);
            }
            else
            {
                // The non-generic API was called or we have a polymorphic case where TValue is not equal to the T in JsonConverter<T>.
                success = jsonConverter.WriteCoreAsObject(writer, value, options, ref state);
            }

            writer.Flush();
            return success;
        }

        private static ReadOnlySpan<byte> WriteUsingMetadata<TValue>(in TValue value, JsonClassInfo? jsonClassInfo)
        {
            // TODO: this would be when to fallback to regular warm-up code-paths.
            // For validation during development, we don't expect this to be null.
            if (jsonClassInfo == null)
            {
                throw new ArgumentNullException(nameof(jsonClassInfo));
            }

            WriteStack state = default;
            state.Initialize(jsonClassInfo);

            JsonSerializerOptions options = jsonClassInfo.Options;

            using (var output = new PooledByteBufferWriter(options.DefaultBufferSize))
            {
                using (var writer = new Utf8JsonWriter(output, options.GetWriterOptions()))
                {
                    JsonConverter? jsonConverter = jsonClassInfo.PropertyInfoForClassInfo.ConverterBase as JsonConverter<TValue>;
                    if (jsonConverter == null)
                    {
                        throw new InvalidOperationException("todo: classInfo not compatible");
                    }

                    WriteCore(jsonConverter, writer, value, ref state, options);
                }

                return output.WrittenMemory.Span;
            }
        }
    }
}
