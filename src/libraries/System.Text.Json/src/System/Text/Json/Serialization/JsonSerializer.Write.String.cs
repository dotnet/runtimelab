// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace System.Text.Json
{
    public static partial class JsonSerializer
    {
        /// <summary>
        /// Convert the provided value into a <see cref="string"/>.
        /// </summary>
        /// <returns>A <see cref="string"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Json.Serialization.JsonConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using UTF-8
        /// encoding since the implementation internally uses UTF-8. See also <see cref="SerializeToUtf8Bytes"/>
        /// and <see cref="SerializeAsync"/>.
        /// </remarks>
        public static string Serialize<[DynamicallyAccessedMembers(MembersAccessedOnWrite)] TValue>(TValue value, JsonSerializerOptions? options = null)
        {
            return Serialize<TValue>(value, typeof(TValue), options);
        }

        /// <summary>
        /// Convert the provided value into a <see cref="string"/>.
        /// </summary>
        /// <returns>A <see cref="string"/> representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Json.Serialization.JsonConverter"/>
        /// for <paramref name="inputType"/>  or its serializable members.
        /// </exception>
        /// <remarks>Using a <see cref="string"/> is not as efficient as using UTF-8
        /// encoding since the implementation internally uses UTF-8. See also <see cref="SerializeToUtf8Bytes"/>
        /// and <see cref="SerializeAsync"/>.
        /// </remarks>
        public static string Serialize(
            object? value,
            [DynamicallyAccessedMembers(MembersAccessedOnWrite)] Type inputType,
            JsonSerializerOptions? options = null)
        {
            if (inputType == null)
            {
                throw new ArgumentNullException(nameof(inputType));
            }

            if (value != null && !inputType.IsAssignableFrom(value.GetType()))
            {
                ThrowHelper.ThrowArgumentException_DeserializeWrongType(inputType, value);
            }

            return Serialize<object?>(value, inputType, options);
        }

        private static string Serialize<TValue>(in TValue value, Type inputType, JsonSerializerOptions? options)
        {
            if (options == null)
            {
                options = JsonSerializerOptions.DefaultOptions;
            }

            using (var output = new PooledByteBufferWriter(options.DefaultBufferSize))
            {
                using (var writer = new Utf8JsonWriter(output, options.GetWriterOptions()))
                {
                    WriteCore(writer, value, inputType, options);
                }

                return JsonReaderHelper.TranscodeHelper(output.WrittenMemory.Span);
            }
        }

        /// <summary>
        /// todo
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="value"></param>
        /// <param name="jsonTypeInfo"></param>
        /// <returns></returns>
        public static string Serialize<[DynamicallyAccessedMembers(MembersAccessedOnWrite)] TValue>(TValue value, JsonTypeInfo<TValue> jsonTypeInfo)
        {
            // null check for jsonTypeInfo occurs here.
            return SerializeUsingMetadata(value, jsonTypeInfo);
        }

        /// <summary>
        /// todo
        /// </summary>
        /// <param name="value"></param>
        /// <param name="inputType"></param>
        /// <param name="jsonSerializerContext"></param>
        /// <returns></returns>
        public static string Serialize(
            object? value,
            [DynamicallyAccessedMembers(MembersAccessedOnWrite)] Type inputType,
            JsonSerializerContext jsonSerializerContext)
        {
            if (inputType == null)
            {
                throw new ArgumentNullException(nameof(inputType));
            }

            if (value != null && !inputType.IsAssignableFrom(value.GetType()))
            {
                ThrowHelper.ThrowArgumentException_DeserializeWrongType(inputType, value);
            }

            return SerializeUsingMetadata(
                value,
                JsonHelpers.GetJsonClassInfo(jsonSerializerContext, inputType));
        }

        /// <summary>
        /// todo
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="value"></param>
        /// <param name="jsonSerializerContext"></param>
        /// <returns></returns>
        public static string Serialize<[DynamicallyAccessedMembers(MembersAccessedOnWrite)] TValue>(TValue value, JsonSerializerContext jsonSerializerContext)
        {
            if (jsonSerializerContext == null)
            {
                throw new ArgumentNullException(nameof(jsonSerializerContext));
            }

            Type type = typeof(TValue) == typeof(object) && value != null
                ? value.GetType()
                : typeof(TValue);

            return SerializeUsingMetadata(
                value,
                JsonHelpers.GetJsonClassInfo(jsonSerializerContext, type));
        }

        private static string SerializeUsingMetadata<TValue>(in TValue value, JsonClassInfo? jsonClassInfo)
        {
            if (jsonClassInfo == null)
            {
                throw new ArgumentNullException(nameof(jsonClassInfo));
            }

            JsonSerializerOptions options = jsonClassInfo.Options;

            WriteStack state = default;
            state.Initialize(jsonClassInfo, options, supportContinuation: false);

            using (var output = new PooledByteBufferWriter(options.DefaultBufferSize))
            {
                using (var writer = new Utf8JsonWriter(output, options.GetWriterOptions()))
                {
                    JsonConverter? jsonConverter = jsonClassInfo.PropertyInfoForClassInfo.ConverterBase;
                    WriteCore(jsonConverter, writer, value, ref state, options);
                }

                return JsonReaderHelper.TranscodeHelper(output.WrittenMemory.Span);
            }
        }
    }
}
