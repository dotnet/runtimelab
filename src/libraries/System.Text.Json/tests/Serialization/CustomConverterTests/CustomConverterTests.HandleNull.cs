// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Json.Serialization.Tests
{
    public abstract partial class CustomConverterTests
    {
        [Fact]
        public async Task ValueTypeConverter_NoOverride()
        {
            // Baseline
            await Assert.ThrowsAsync<JsonException>(async () => await Deserializer.DeserializeWrapper<int>("null"));

            // Per null handling default value for value types (true), converter handles null.
            var options = new JsonSerializerOptions();
            options.Converters.Add(new Int32NullConverter_SpecialCaseNull());

            Assert.Equal(-1, await Deserializer.DeserializeWrapper<int>("null", options));
        }

        private class Int32NullConverter_SpecialCaseNull : JsonConverter<int>
        {
            public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return -1;
                }

                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public async Task ValueTypeConverter_OptOut()
        {
            // Per null handling opt-out, serializer handles null.
            var options = new JsonSerializerOptions();
            options.Converters.Add(new Int32NullConverter_OptOut());

            // Serializer throws JsonException if null is assigned to value that can't be null.
            await Assert.ThrowsAsync<JsonException>(async () => await Deserializer.DeserializeWrapper<int>("null", options));
            await Assert.ThrowsAsync<JsonException>(async () => await Deserializer.DeserializeWrapper<ClassWithInt>(@"{""MyInt"":null}", options));
            await Assert.ThrowsAsync<JsonException>(async () => await Deserializer.DeserializeWrapper<List<int>>("[null]", options));
            await Assert.ThrowsAsync<JsonException>(async () => await Deserializer.DeserializeWrapper<Dictionary<string, int>>(@"{""MyInt"":null}", options));
        }

        private class Int32NullConverter_OptOut : Int32NullConverter_SpecialCaseNull
        {
            public override bool HandleNull => false;
        }

        private class ClassWithInt
        {
            public int MyInt { get; set; }
        }

        [Fact]
        public async Task ValueTypeConverter_NullOptIn()
        {
            // Per null handling opt-in, converter handles null.
            var options = new JsonSerializerOptions();
            options.Converters.Add(new Int32NullConverter_NullOptIn());

            Assert.Equal(-1, await Deserializer.DeserializeWrapper<int>("null", options));
        }

        private class Int32NullConverter_NullOptIn : Int32NullConverter_SpecialCaseNull
        {
            public override bool HandleNull => true;
        }

        [Fact]
        public async Task ComplexValueTypeConverter_NoOverride()
        {
            // Baseline
            await Assert.ThrowsAsync<JsonException>(async () => await Deserializer.DeserializeWrapper<Point_2D_Struct>("null"));

            var options = new JsonSerializerOptions();
            options.Converters.Add(new PointStructConverter_SpecialCaseNull());

            // Per null handling default value for value types (true), converter handles null.
            var obj = await Deserializer.DeserializeWrapper<Point_2D_Struct>("null", options);
            Assert.Equal(-1, obj.X);
            Assert.Equal(-1, obj.Y);
        }

        private class PointStructConverter_SpecialCaseNull : JsonConverter<Point_2D_Struct>
        {
            public override Point_2D_Struct Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return new Point_2D_Struct(-1, -1);
                }

                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, Point_2D_Struct value, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }

        [Fact]
        public async Task ComplexValueTypeConverter_OptOut()
        {
            // Per null handling opt-out, serializer handles null.
            var options = new JsonSerializerOptions();
            options.Converters.Add(new PointStructConverter_OptOut());

            // Serializer throws JsonException if null is assigned to value that can't be null.
            await Assert.ThrowsAsync<JsonException>(async () => await Deserializer.DeserializeWrapper<Point_2D_Struct>("null", options));
            await Assert.ThrowsAsync<JsonException>(async () => await Deserializer.DeserializeWrapper<ClassWithPoint>(@"{""MyPoint"":null}", options));
            await Assert.ThrowsAsync<JsonException>(async () => await Deserializer.DeserializeWrapper<ImmutableClassWithPoint>(@"{""MyPoint"":null}", options));
            await Assert.ThrowsAsync<JsonException>(async () => await Deserializer.DeserializeWrapper<List<Point_2D_Struct>>("[null]", options));
            await Assert.ThrowsAsync<JsonException>(async () => await Deserializer.DeserializeWrapper<Dictionary<string, Point_2D_Struct>>(@"{""MyPoint"":null}", options));
        }

        private class PointStructConverter_OptOut : PointStructConverter_SpecialCaseNull
        {
            public override bool HandleNull => false;
        }

        private class ClassWithPoint
        {
            public Point_2D_Struct MyPoint { get; set; }
        }

        private class ImmutableClassWithPoint
        {
            public Point_2D_Struct MyPoint { get; }

            public ImmutableClassWithPoint(Point_2D_Struct myPoint) => MyPoint = myPoint;
        }

        [Fact]
        public async Task ComplexValueTypeConverter_NullOptIn()
        {
            // Baseline
            await Assert.ThrowsAsync<JsonException>(async () => await Deserializer.DeserializeWrapper<Point_2D_Struct>("null"));

            // Per null handling opt-in, converter handles null.
            var options = new JsonSerializerOptions();
            options.Converters.Add(new PointStructConverter_NullOptIn());

            var obj = await Deserializer.DeserializeWrapper<Point_2D_Struct>("null", options);
            Assert.Equal(-1, obj.X);
            Assert.Equal(-1, obj.Y);
        }

        private class PointStructConverter_NullOptIn : PointStructConverter_SpecialCaseNull
        {
            public override bool HandleNull => true;
        }

        [Fact]
        public async Task NullableValueTypeConverter_NoOverride()
        {
            // Baseline
            int? val = await Deserializer.DeserializeWrapper<int?>("null");
            Assert.Null(val);
            Assert.Equal("null", JsonSerializer.Serialize(val));

            // For compat, deserialize does not call converter for null token unless the type doesn't support
            // null or HandleNull is overridden and returns 'true'.
            // For compat, serialize does not call converter for null unless null is a valid value and HandleNull is true.
            var options = new JsonSerializerOptions();
            options.Converters.Add(new NullableInt32NullConverter_SpecialCaseNull());

            val = await Deserializer.DeserializeWrapper<int?>("null", options);
            Assert.Null(val);

            val = null;
            Assert.Equal("null", JsonSerializer.Serialize(val, options));
        }

        private class NullableInt32NullConverter_SpecialCaseNull : JsonConverter<int?>
        {
            public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return -1;
                }

                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
            {
                if (!value.HasValue)
                {
                    writer.WriteNumberValue(-1);
                    return;
                }

                throw new NotSupportedException();
            }
        }

        [Fact]
        public async Task NullableValueTypeConverter_OptOut()
        {
            // Baseline
            int? val = await Deserializer.DeserializeWrapper<int?>("null");
            Assert.Null(val);
            Assert.Equal("null", JsonSerializer.Serialize(val));

            // Per null handling opt-out, serializer handles null.
            var options = new JsonSerializerOptions();
            options.Converters.Add(new NullableInt32NullConverter_NullOptOut());

            val = await Deserializer.DeserializeWrapper<int?>("null", options);
            Assert.Null(val);
            Assert.Equal("null", JsonSerializer.Serialize(val, options));
        }

        private class NullableInt32NullConverter_NullOptOut : NullableInt32NullConverter_SpecialCaseNull
        {
            public override bool HandleNull => false;
        }

        [Fact]
        public async Task ReferenceTypeConverter_NoOverride()
        {
            // Baseline
            Uri val = await Deserializer.DeserializeWrapper<Uri>("null");
            Assert.Null(val);
            Assert.Equal("null", JsonSerializer.Serialize(val));

            // Per null handling default value for reference types (false), serializer handles null.
            var options = new JsonSerializerOptions();
            options.Converters.Add(new UriNullConverter_SpecialCaseNull());

            // Serializer sets default value.
            val = await Deserializer.DeserializeWrapper<Uri>("null", options);
            Assert.Null(val);

            // Serializer serializes null.
            Assert.Equal("null", JsonSerializer.Serialize(val, options));
        }

        private class UriNullConverter_SpecialCaseNull : JsonConverter<Uri>
        {
            public override Uri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return new Uri("https://default");
                }

                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteStringValue("https://default");
                    return;
                }

                throw new NotSupportedException();
            }
        }

        [Fact]
        public async Task ReferenceTypeConverter_OptOut()
        {
            // Per null handling opt-out, serializer handles null.
            var options = new JsonSerializerOptions();
            options.Converters.Add(new UriNullConverter_OptOut());

            Uri val = await Deserializer.DeserializeWrapper<Uri>("null", options);
            Assert.Null(val);
            Assert.Equal("null", JsonSerializer.Serialize(val, options));
        }

        private class UriNullConverter_OptOut : UriNullConverter_SpecialCaseNull
        {
            public override bool HandleNull => false;
        }

        [Fact]
        public async Task ReferenceTypeConverter_NullOptIn()
        {
            // Per null handling opt-in, converter handles null.
            var options = new JsonSerializerOptions();
            options.Converters.Add(new UriNullConverter_NullOptIn());

            Uri val = await Deserializer.DeserializeWrapper<Uri>("null", options);
            Assert.Equal(new Uri("https://default"), val);

            val = null;
            Assert.Equal(@"""https://default""", JsonSerializer.Serialize(val, options));
        }

        private class UriNullConverter_NullOptIn : UriNullConverter_SpecialCaseNull
        {
            public override bool HandleNull => true;
        }

        [Fact]
        public async Task ComplexReferenceTypeConverter_NoOverride()
        {
            // Baseline
            Point_2D obj = await Deserializer.DeserializeWrapper<Point_2D>("null");
            Assert.Null(obj);
            Assert.Equal("null", JsonSerializer.Serialize(obj));

            // Per null handling default value for reference types (false), serializer handles null.
            var options = new JsonSerializerOptions();
            options.Converters.Add(new PointClassConverter_SpecialCaseNull());

            obj = await Deserializer.DeserializeWrapper<Point_2D>("null", options);
            Assert.Null(obj);
            Assert.Equal("null", JsonSerializer.Serialize(obj));
        }

        private class PointClassConverter_SpecialCaseNull : JsonConverter<Point_2D>
        {
            public override Point_2D Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return new Point_2D(-1, -1);
                }

                throw new JsonException();
            }

            public override void Write(Utf8JsonWriter writer, Point_2D value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteStartObject();
                    writer.WriteNumber("X", -1);
                    writer.WriteNumber("Y", -1);
                    writer.WriteEndObject();
                    return;
                }

                throw new JsonException();
            }
        }

        [Fact]
        public async Task ComplexReferenceTypeConverter_NullOptIn()
        {
            // Per null handling opt-in, converter handles null.
            var options = new JsonSerializerOptions();
            options.Converters.Add(new PointClassConverter_NullOptIn());

            Point_2D obj = await Deserializer.DeserializeWrapper<Point_2D>("null", options);
            Assert.Equal(-1, obj.X);
            Assert.Equal(-1, obj.Y);

            obj = null;
            JsonTestHelper.AssertJsonEqual(@"{""X"":-1,""Y"":-1}", JsonSerializer.Serialize(obj, options));
        }

        private class PointClassConverter_NullOptIn : PointClassConverter_SpecialCaseNull
        {
            public override bool HandleNull => true;
        }

        [Fact]
        public async Task ConverterNotCalled_IgnoreNullValues()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new UriNullConverter_NullOptIn());

            // Converter is called - JsonIgnoreCondition.WhenWritingDefault does not apply to deserialization.
            ClassWithIgnoredUri obj = await Deserializer.DeserializeWrapper<ClassWithIgnoredUri>(@"{""MyUri"":null}", options);
            Assert.Equal(new Uri("https://default"), obj.MyUri);

            obj.MyUri = null;
            // Converter is not called - value is ignored on serialization.
            Assert.Equal("{}", JsonSerializer.Serialize(obj, options));
        }

        private class ClassWithIgnoredUri
        {
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
            public Uri MyUri { get; set; } = new Uri("https://microsoft.com");
        }

        [Fact]
        public async Task ConverterWritesBadAmount()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new BadUriConverter());
            options.Converters.Add(new BadObjectConverter());

            // Using serializer overload in Release mode uses a writer with SkipValidation = true.
            var writerOptions = new JsonWriterOptions { SkipValidation = false };
            using (Utf8JsonWriter writer = new Utf8JsonWriter(new ArrayBufferWriter<byte>(), writerOptions))
            {
                Assert.Throws<JsonException>(() => JsonSerializer.Serialize(writer, new ClassWithUri(), options));
            }

            using (Utf8JsonWriter writer = new Utf8JsonWriter(new ArrayBufferWriter<byte>(), writerOptions))
            {
                await Assert.ThrowsAsync<JsonException>(async () => await Serializer.SerializeWrapper(new StructWithObject(), options));
            }
        }

        private class BadUriConverter : UriNullConverter_NullOptIn
        {
            public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options) { }
        }

        private class BadObjectConverter : JsonConverter<object>
        {
            public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();
                writer.WritePropertyName("hello");
                writer.WriteNullValue();
            }

            public override bool HandleNull => true;
        }

        private class ClassWithUri
        {
            public Uri MyUri { get; set; }
        }


        private class StructWithObject
        {
            public object MyObj { get; set; }
        }

        [Fact]
        public async Task ObjectAsRootValue()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ObjectConverter());

            object obj = null;
            Assert.Equal(@"""NullObject""", JsonSerializer.Serialize(obj, options));
            Assert.Equal("NullObject", await Deserializer.DeserializeWrapper<object>("null", options));

            options = new JsonSerializerOptions();
            options.Converters.Add(new BadObjectConverter());
            await Assert.ThrowsAsync<JsonException>(async () => await Serializer.SerializeWrapper(obj, options));
        }

        [Fact]
        public async Task ObjectAsCollectionElement()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ObjectConverter());

            List<object> list = new List<object> {  null };
            Assert.Equal(@"[""NullObject""]", JsonSerializer.Serialize(list, options));

            list = await Deserializer.DeserializeWrapper<List<object>>("[null]", options);
            Assert.Equal("NullObject", list[0]);

            options = new JsonSerializerOptions();
            options.Converters.Add(new BadObjectConverter());

            list[0] = null;
            await Assert.ThrowsAsync<JsonException>(async () => await Serializer.SerializeWrapper(list, options));
        }

        public class ObjectConverter : JsonConverter<object>
        {
            public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return "NullObject";
                }

                throw new NotSupportedException();
            }

            public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteStringValue("NullObject");
                    return;
                }

                throw new NotSupportedException();
            }

            public override bool HandleNull => true;
        }

        [Fact]
        public async Task SetterCalledWhenConverterReturnsNull()
        {
            var options = new JsonSerializerOptions
            {
                IgnoreNullValues = true,
                Converters = { new UriToNullConverter() }
            };

            // Baseline - null values ignored, converter is not called.
            string json = @"{""MyUri"":null}";

            ClassWithInitializedUri obj = await Deserializer.DeserializeWrapper<ClassWithInitializedUri>(json, options);
            Assert.Equal(new Uri("https://microsoft.com"), obj.MyUri);

            // Test - setter is called if payload is not null and converter returns null.
            json = @"{""MyUri"":""https://default""}";
            obj = await Deserializer.DeserializeWrapper<ClassWithInitializedUri>(json, options);
            Assert.Null(obj.MyUri);
        }

        private class ClassWithInitializedUri
        {
            public Uri MyUri { get; set; } = new Uri("https://microsoft.com");
        }

        public class UriToNullConverter : JsonConverter<Uri>
        {
            public override Uri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => null;

            public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options) => throw new NotImplementedException();

            public override bool HandleNull => true;
        }
    }
}
