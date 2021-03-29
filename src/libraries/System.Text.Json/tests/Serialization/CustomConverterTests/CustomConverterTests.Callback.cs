// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Json.Serialization.Tests
{
    public abstract partial class CustomConverterTests
    {
        /// <summary>
        /// A converter that calls back in the serializer.
        /// </summary>
        private class CustomerCallbackConverter : JsonConverter<Customer>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeof(Customer).IsAssignableFrom(typeToConvert);
            }

            public override Customer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                // The options are not passed here as that would cause an infinite loop.
                Customer value = JsonSerializer.Deserialize<Customer>(ref reader);

                value.Name += "Hello!";
                return value;
            }

            public override void Write(Utf8JsonWriter writer, Customer value, JsonSerializerOptions options)
            {
                writer.WriteStartArray();

                long bytesWrittenSoFar = writer.BytesCommitted + writer.BytesPending;

                JsonSerializer.Serialize(writer, value);

                Debug.Assert(writer.BytesPending == 0);
                long payloadLength =  writer.BytesCommitted - bytesWrittenSoFar;
                writer.WriteNumberValue(payloadLength);
                writer.WriteEndArray();
            }
        }

        [Fact]
        public async Task ConverterWithCallback()
        {
            const string json = @"{""Name"":""MyName""}";

            var options = new JsonSerializerOptions();
            options.Converters.Add(new CustomerCallbackConverter());

            Customer customer = await Deserializer.DeserializeWrapper<Customer>(json, options);
            Assert.Equal("MyNameHello!", customer.Name);

            string result = await Serializer.SerializeWrapper(customer, options);
            int expectedLength = (await Serializer.SerializeWrapper(customer)).Length;
            Assert.Equal(@"[{""CreditLimit"":0,""Name"":""MyNameHello!"",""Address"":{""City"":null}}," + $"{expectedLength}]", result);
        }

        /// <summary>
        /// A converter that calls back in the serializer with not supported types.
        /// </summary>
        private class PocoWithNotSupportedChildConverter : JsonConverter<ChildPocoWithConverter>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                return typeof(ChildPocoWithConverter).IsAssignableFrom(typeToConvert);
            }

            public override ChildPocoWithConverter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                reader.Read();
                Debug.Assert(reader.TokenType == JsonTokenType.PropertyName);
                Debug.Assert(reader.GetString() == "Child");

                reader.Read();
                Debug.Assert(reader.TokenType == JsonTokenType.StartObject);

                // The options are not passed here as that would cause an infinite loop.
                ChildPocoWithNoConverter value = JsonSerializer.Deserialize<ChildPocoWithNoConverter>(ref reader);

                // Should not get here due to exception.
                Debug.Assert(false);
                return default;
            }

            public override void Write(Utf8JsonWriter writer, ChildPocoWithConverter value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("Child");

                JsonSerializer.Serialize<ChildPocoWithNoConverter>(writer, value.Child);

                // Should not get here due to exception.
                Debug.Assert(false);
            }
        }

        private class TopLevelPocoWithNoConverter
        {
            public ChildPocoWithConverter Child { get; set; }
        }

        private class ChildPocoWithConverter
        {
            public ChildPocoWithNoConverter Child { get; set; }
        }

        private class ChildPocoWithNoConverter
        {
            public ChildPocoWithNoConverterAndInvalidProperty InvalidProperty { get; set; }
        }

        private class ChildPocoWithNoConverterAndInvalidProperty
        {
            public int[,] NotSupported { get; set; }
        }

        [Fact]
        public async Task ConverterWithReentryFail()
        {
            const string Json = @"{""Child"":{""Child"":{""InvalidProperty"":{""NotSupported"":[1]}}}}";

            NotSupportedException ex;

            var options = new JsonSerializerOptions();
            options.Converters.Add(new PocoWithNotSupportedChildConverter());

            // This verifies:
            // - Path does not flow through to custom converters that re-enter the serializer.
            // - "Path:" is not repeated due to having two try\catch blocks (the second block does not append "Path:" again).

            ex = await Assert.ThrowsAsync<NotSupportedException>(async () => await Deserializer.DeserializeWrapper<TopLevelPocoWithNoConverter>(Json, options));
            Assert.Contains(typeof(int[,]).ToString(), ex.ToString());
            Assert.Contains(typeof(ChildPocoWithNoConverterAndInvalidProperty).ToString(), ex.ToString());
            Assert.Contains("Path: $.InvalidProperty | LineNumber: 0 | BytePositionInLine: 20.", ex.ToString());
            Assert.Equal(2, ex.ToString().Split(new string[] { "Path:" }, StringSplitOptions.None).Length);

            var poco = new TopLevelPocoWithNoConverter()
            {
                Child = new ChildPocoWithConverter()
                {
                    Child = new ChildPocoWithNoConverter()
                    {
                        InvalidProperty = new ChildPocoWithNoConverterAndInvalidProperty()
                        {
                            NotSupported = new int[,] { { 1, 2 } }
                        }
                    }
                }
            };

            ex = await Assert.ThrowsAsync<NotSupportedException>(async () => await Serializer.SerializeWrapper(poco, options));
            Assert.Contains(typeof(int[,]).ToString(), ex.ToString());
            Assert.Contains(typeof(ChildPocoWithNoConverterAndInvalidProperty).ToString(), ex.ToString());
            Assert.Contains("Path: $.InvalidProperty.", ex.ToString());
            Assert.Equal(2, ex.ToString().Split(new string[] { "Path:" }, StringSplitOptions.None).Length);
        }
    }
}
