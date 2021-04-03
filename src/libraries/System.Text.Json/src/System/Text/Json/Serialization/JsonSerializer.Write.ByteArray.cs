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
        /// Convert the provided value into a <see cref="byte"/> array.
        /// </summary>
        /// <returns>A UTF-8 representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Json.Serialization.JsonConverter"/>
        /// for <typeparamref name="TValue"/> or its serializable members.
        /// </exception>
        public static byte[] SerializeToUtf8Bytes<[DynamicallyAccessedMembers(MembersAccessedOnWrite)] TValue>(
            TValue value,
            JsonSerializerOptions? options = null)
        {
            return WriteCoreBytes<TValue>(value, typeof(TValue), options);
        }

        /// <summary>
        /// todo
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="value"></param>
        /// <param name="jsonTypeInfo"></param>
        /// <returns></returns>
        public static byte[] SerializeToUtf8Bytes<[DynamicallyAccessedMembers(MembersAccessedOnWrite)] TValue>(
            TValue value,
            JsonTypeInfo<TValue> jsonTypeInfo)
        {
            if (jsonTypeInfo == null)
            {
                throw new ArgumentNullException(nameof(jsonTypeInfo));
            }

            JsonSerializerOptions options = jsonTypeInfo.Options;

            WriteStack state = default;
            state.Initialize(jsonTypeInfo, options, supportContinuation: false);

            using (var output = new PooledByteBufferWriter(options.DefaultBufferSize))
            {
                using (var writer = new Utf8JsonWriter(output, options.GetWriterOptions()))
                {
                    WriteCore(jsonTypeInfo.PropertyInfoForTypeInfo.ConverterBase, writer, value, ref state, options);
                }

                return output.WrittenMemory.Span.ToArray();
            }
        }

        /// <summary>
        /// Convert the provided value into a <see cref="byte"/> array.
        /// </summary>
        /// <returns>A UTF-8 representation of the value.</returns>
        /// <param name="value">The value to convert.</param>
        /// <param name="inputType">The type of the <paramref name="value"/> to convert.</param>
        /// <param name="options">Options to control the conversion behavior.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="inputType"/> is not compatible with <paramref name="value"/>.
        /// </exception>
        /// <exception cref="System.ArgumentNullException">
        /// <paramref name="inputType"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="NotSupportedException">
        /// There is no compatible <see cref="System.Text.Json.Serialization.JsonConverter"/>
        /// for <paramref name="inputType"/>  or its serializable members.
        /// </exception>
        public static byte[] SerializeToUtf8Bytes(
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

            return WriteCoreBytes<object>(value!, inputType, options);
        }

        private static byte[] WriteCoreBytes<TValue>(in TValue value, Type inputType, JsonSerializerOptions? options)
        {
            if (options == null)
            {
                options = JsonSerializerOptions.DefaultOptions;
            }

            using (var output = new PooledByteBufferWriter(options.DefaultBufferSize))
            {
                using (var writer = new Utf8JsonWriter(output, options.GetWriterOptions()))
                {
                    WriteCore<TValue>(writer, value, inputType, options);
                }

                return output.WrittenMemory.ToArray();
            }
        }
    }
}
