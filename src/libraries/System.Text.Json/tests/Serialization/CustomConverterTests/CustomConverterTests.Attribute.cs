// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using Xunit;

#if GENERATE_JSON_METADATA
using System.Text.Json.Serialization.Tests;
using System.Text.Json.SourceGeneration;

[assembly: JsonSerializable(typeof(CustomConverterTests.AttributedPoint))]
[assembly: JsonSerializable(typeof(CustomConverterTests.AttributedPoint_WithPointConverter))]
[assembly: JsonSerializable(typeof(CustomConverterTests.ClassWithJsonConverterAttribute))]
#endif

namespace System.Text.Json.Serialization.Tests
{
    public abstract partial class CustomConverterTests
    {
        /// <summary>
        /// Pass additional information to a converter through an attribute on a property.
        /// </summary>
        [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
        private class PointConverterAttribute : JsonConverterAttribute
        {
            public PointConverterAttribute(int coordinateOffset = 0)
            {
                CoordinateOffset = coordinateOffset;
            }

            public int CoordinateOffset { get; private set; }

            /// <summary>
            /// If overridden, allows a custom attribute to create the converter in order to pass additional state.
            /// </summary>
            /// <returns>The custom converter, or null if the serializer should create the custom converter.</returns>
            public override JsonConverter CreateConverter(Type typeToConvert)
            {
                return new PointConverter(CoordinateOffset);
            }
        }

        private class ClassWithPointConverterAttribute
        {
            [PointConverter(10)]
            public Point Point1 { get; set; }
        }

        [Fact]
        public async Task CustomAttributeOnPropertyExtraInformation()
        {
            const string json = @"{""Point1"":""1,2""}";

            ClassWithPointConverterAttribute obj = await Deserializer.DeserializeWrapper<ClassWithPointConverterAttribute>(json);
            Assert.Equal(11, obj.Point1.X);
            Assert.Equal(12, obj.Point1.Y);

            string jsonSerialized = await Serializer.SerializeWrapper(obj);
            Assert.Equal(json, jsonSerialized);
        }

        internal class ClassWithJsonConverterAttribute
        {
            [JsonConverter(typeof(PointConverter))]
            public Point Point1 { get; set; }
        }

        [Fact]
        public async Task CustomAttributeOnProperty()
        {
            const string json = @"{""Point1"":""1,2""}";

            ClassWithJsonConverterAttribute obj = await Deserializer.DeserializeWrapper<ClassWithJsonConverterAttribute>(json);
            Assert.Equal(1, obj.Point1.X);
            Assert.Equal(2, obj.Point1.Y);

            string jsonSerialized = await Serializer.SerializeWrapper(obj);
            Assert.Equal(json, jsonSerialized);
        }

        // A custom data type representing a point where JSON is "XValue,Yvalue".
        // A struct is used here, but could be a class.
        [JsonConverter(typeof(AttributedPointConverter<AttributedPoint>))]
        public struct AttributedPoint : IAttributedPoint
        {
            public int X { get; set; }
            public int Y { get; set; }
        }


        [AttributedPointConverter(10)]
        public struct AttributedPoint_WithPointConverter : IAttributedPoint
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        public interface IAttributedPoint
        {
            public int X { get; set; }
            public int Y { get; set; }
        }

        /// <summary>
        /// Converter for a custom data type that has additional state (coordinateOffset).
        /// </summary>
        // TODO: add codegen test where this class is private.
        internal class AttributedPointConverter<T> : JsonConverter<T> where T : struct, IAttributedPoint
        {
            private int _offset;

            public AttributedPointConverter() { }

            public AttributedPointConverter(int offset)
            {
                _offset = offset;
            }

            public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType != JsonTokenType.String)
                {
                    throw new JsonException();
                }

                string[] stringValues = reader.GetString().Split(',');
                if (stringValues.Length != 2)
                {
                    throw new JsonException();
                }

                T value = new T();
                if (!int.TryParse(stringValues[0], out int x) || !int.TryParse(stringValues[1], out int y))
                {
                    throw new JsonException();
                }

                value.X = x + _offset;
                value.Y = y + _offset;

                return value;
            }

            public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
            {
                string stringValue = $"{value.X - _offset},{value.Y - _offset}";
                writer.WriteStringValue(stringValue);
            }
        }

        [Fact]
        public async Task CustomAttributeOnType()
        {
            const string json = @"""1,2""";

            AttributedPoint point = await Deserializer.DeserializeWrapper<AttributedPoint>(json);
            Assert.Equal(1, point.X);
            Assert.Equal(2, point.Y);

            string jsonSerialized = await Serializer.SerializeWrapper(point);
            Assert.Equal(json, jsonSerialized);
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("Needs code-gen support for attributes deriving from JsonConverterAttribute")]
#endif
        public async Task CustomAttributeOnTypeExtraInformation()
        {
            const string json = @"""1,2""";

            AttributedPoint_WithPointConverter point = await Deserializer.DeserializeWrapper<AttributedPoint_WithPointConverter>(json);
            Assert.Equal(11, point.X);
            Assert.Equal(12, point.Y);

            string jsonSerialized = await Serializer.SerializeWrapper(point);
            Assert.Equal(json, jsonSerialized);
        }

        [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Property, AllowMultiple = false)]
        private class AttributedPointConverterAttribute : JsonConverterAttribute
        {
            public AttributedPointConverterAttribute(int offset = 0)
            {
                Offset = offset;
            }

            public int Offset { get; private set; }

            /// <summary>
            /// If overridden, allows a custom attribute to create the converter in order to pass additional state.
            /// </summary>
            /// <returns>The custom converter, or null if the serializer should create the custom converter.</returns>
            public override JsonConverter CreateConverter(Type typeToConvert)
            {
                if (typeToConvert == typeof(AttributedPoint))
                {
                    return new AttributedPointConverter<AttributedPoint>(Offset);
                }
                else if (typeToConvert == typeof(AttributedPoint_WithPointConverter))
                {
                    return new AttributedPointConverter<AttributedPoint_WithPointConverter>(Offset);
                }

                throw new NotSupportedException();
            }
        }

        private class ClassWithJsonConverterAttributeOverride
        {
            [AttributedPointConverter(100)] // overrides the type attribute on AttributedPoint
            public AttributedPoint Point1 { get; set; }
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("Needs code-gen support for attributes deriving from JsonConverterAttribute")]
#endif
        public async Task CustomAttributeOnTypeAndProperty()
        {
            const string json = @"{""Point1"":""1,2""}";

            ClassWithJsonConverterAttributeOverride point = await Deserializer.DeserializeWrapper<ClassWithJsonConverterAttributeOverride>(json);

            // The property attribute overrides the type attribute.
            Assert.Equal(101, point.Point1.X);
            Assert.Equal(102, point.Point1.Y);

            string jsonSerialized = await Serializer.SerializeWrapper(point);
            Assert.Equal(json, jsonSerialized);
        }

        [Fact]
        public async Task CustomAttributeOnPropertyAndRuntime()
        {
            const string json = @"{""Point1"":""1,2""}";

            var options = new JsonSerializerOptions();
            options.Converters.Add(new AttributedPointConverter<AttributedPoint>(200));

            ClassWithJsonConverterAttributeOverride point = await Deserializer.DeserializeWrapper<ClassWithJsonConverterAttributeOverride>(json, options);

            // The property attribute overrides the runtime.
            Assert.Equal(101, point.Point1.X);
            Assert.Equal(102, point.Point1.Y);

            string jsonSerialized = await Serializer.SerializeWrapper(point, options);
            Assert.Equal(json, jsonSerialized);
        }

        [Fact]
        public async Task CustomAttributeOnTypeAndRuntime()
        {
            const string json = @"""1,2""";

            // Baseline
            AttributedPoint point = await Deserializer.DeserializeWrapper<AttributedPoint>(json);
            Assert.Equal(1, point.X);
            Assert.Equal(2, point.Y);
            Assert.Equal(json, JsonSerializer.Serialize(point));

            // Now use options.
            var options = new JsonSerializerOptions();
            options.Converters.Add(new AttributedPointConverter<AttributedPoint>(200));

            point = await Deserializer.DeserializeWrapper<AttributedPoint>(json, options);

            // The runtime overrides the type attribute.
            Assert.Equal(201, point.X);
            Assert.Equal(202, point.Y);

            string jsonSerialized = await Serializer.SerializeWrapper(point, options);
            Assert.Equal(json, jsonSerialized);
        }
    }
}
