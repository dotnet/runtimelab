// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Json.Serialization.Tests
{
    public abstract partial class CustomConverterTests
    {
        private class FailConverter<TException> : JsonConverter<int> where TException: Exception, new()
        {
            public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new TException();
            }

            public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
            {
                throw new TException();
            }
        }

        private async Task ConverterFailNoRethrow<TException>() where TException : Exception, new()
        {
            var options = new JsonSerializerOptions();
            JsonConverter converter = new FailConverter<TException>();
            options.Converters.Add(converter);

            await Assert.ThrowsAsync<TException>(async () => await Deserializer.DeserializeWrapper<int>("0", options));
            await Assert.ThrowsAsync<TException>(async () => await Deserializer.DeserializeWrapper<int[]>("[0]", options));
            await Assert.ThrowsAsync<TException>(async () => await Serializer.SerializeWrapper(0, options));
            await Assert.ThrowsAsync<TException>(async () => await Serializer.SerializeWrapper(new int[] { 0 }, options));

            var obj = new Dictionary<string, int>();
            obj["key"] = 0;

            await Assert.ThrowsAsync<TException>(async () => await Serializer.SerializeWrapper(obj, options));
        }

        [Fact]
        public async Task ConverterExceptionsNotRethrownFail()
        {
            // We should not catch these unless thrown from the reader\document.
            await ConverterFailNoRethrow<FormatException>();
            await ConverterFailNoRethrow<ArgumentException>();

            // Other misc exception we should not catch:
            await ConverterFailNoRethrow<Exception>();
            await ConverterFailNoRethrow<InvalidOperationException>();
            await ConverterFailNoRethrow<IndexOutOfRangeException>();
            await ConverterFailNoRethrow<NotSupportedException>();
        }
    }
}
