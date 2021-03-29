// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

#if GENERATE_JSON_METADATA
using System.Text.Json.Serialization.Tests;
using System.Text.Json.SourceGeneration;

[assembly: JsonSerializable(typeof(CustomConverterTests.InvalidTypeConverterClass))]
#endif

namespace System.Text.Json.Serialization.Tests
{
    public abstract partial class CustomConverterTests
    {
        private class PocoWithNoBaseClass { }
        private class DerivedCustomer : Customer { }
        private class SuccessException : Exception { }

        private class BadCustomerConverter : JsonConverter<Customer>
        {
            public override bool CanConvert(Type typeToConvert)
            {
                // Say this converter supports all types even though we specify "Customer".
                return true;
            }

            public override Customer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new SuccessException();
            }

            public override void Write(Utf8JsonWriter writer, Customer value, JsonSerializerOptions options)
            {
                throw new SuccessException();
            }
        }

        [Fact]
        public async Task ContraVariantConverterFail()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new BadCustomerConverter());

            // Incompatible types.
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Deserializer.DeserializeWrapper<int>("0", options));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(0, options));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Deserializer.DeserializeWrapper<PocoWithNoBaseClass>("{}", options));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new PocoWithNoBaseClass(), options));

            // Contravariant to Customer.
            await Assert.ThrowsAsync<SuccessException>(async () => await Deserializer.DeserializeWrapper<DerivedCustomer>("{}", options));
            await Assert.ThrowsAsync<SuccessException>(async () => await Serializer.SerializeWrapper(new DerivedCustomer(), options));

            // Covariant to Customer.
            await Assert.ThrowsAsync<SuccessException>(async () => await Deserializer.DeserializeWrapper<Customer>("{}", options));
            await Assert.ThrowsAsync<SuccessException>(async () => await Serializer.SerializeWrapper(new Customer(), options));
            await Assert.ThrowsAsync<SuccessException>(async () => await Serializer.SerializeWrapper<Customer>(new DerivedCustomer(), options));

            await Assert.ThrowsAsync<SuccessException>(async () => await Deserializer.DeserializeWrapper<Person>("{}", options));
            await Assert.ThrowsAsync<SuccessException>(async () => await Serializer.SerializeWrapper<Person>(new Customer(), options));
            await Assert.ThrowsAsync<SuccessException>(async () => await Serializer.SerializeWrapper<Person>(new DerivedCustomer(), options));
        }

        private class InvalidConverterAttribute : JsonConverterAttribute
        {
            // converterType is not valid since typeof(int) is not a type that derives from JsonConverter.
            public InvalidConverterAttribute() : base(converterType: typeof(int)) { }
        }

        private class PocoWithInvalidConverter
        {
            [InvalidConverter]
            public int MyInt { get; set; }
        }

        private class NullConverterAttribute : JsonConverterAttribute
        {
            public NullConverterAttribute() : base(null) { }

            public override JsonConverter CreateConverter(Type typeToConvert)
            {
                return null;
            }
        }

        private class PocoWithNullConverter
        {
            [NullConverter]
            public int MyInt { get; set; }
        }

        [Fact]
        public async Task AttributeCreateConverterFail()
        {
            InvalidOperationException ex;

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new PocoWithInvalidConverter()));
            // Message should be in the form "The converter specified on 'System.Text.Json.Serialization.Tests.CustomConverterTests+PocoWithInvalidConverter.MyInt' does not derive from JsonConverter or have a public parameterless constructor."
            Assert.Contains("'System.Text.Json.Serialization.Tests.CustomConverterTests+PocoWithInvalidConverter.MyInt'", ex.Message);

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Deserializer.DeserializeWrapper<PocoWithInvalidConverter>("{}"));
            Assert.Contains("'System.Text.Json.Serialization.Tests.CustomConverterTests+PocoWithInvalidConverter.MyInt'", ex.Message);

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new PocoWithNullConverter()));
            // Message should be in the form "The converter specified on 'System.Text.Json.Serialization.Tests.CustomConverterTests+PocoWithNullConverter.MyInt'  is not compatible with the type 'System.Int32'."
            Assert.Contains("'System.Text.Json.Serialization.Tests.CustomConverterTests+PocoWithNullConverter.MyInt'", ex.Message);
            Assert.Contains("'System.Int32'", ex.Message);

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Deserializer.DeserializeWrapper<PocoWithNullConverter>("{}"));
            Assert.Contains("'System.Text.Json.Serialization.Tests.CustomConverterTests+PocoWithNullConverter.MyInt'", ex.Message);
            Assert.Contains("'System.Int32'", ex.Message);
        }

        internal class InvalidTypeConverterClass
        {
            [JsonConverter(typeof(JsonStringEnumConverter))]
            public ICollection<InvalidTypeConverterEnum> MyEnumValues { get; set; }
        }

        internal enum InvalidTypeConverterEnum
        {
            Value1,
            Value2,
        }

        [Fact]
        public async Task AttributeOnPropertyFail()
        {
            InvalidOperationException ex;

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new InvalidTypeConverterClass()));
            // Message should be in the form "The converter specified on 'System.Text.Json.Serialization.Tests.CustomConverterTests+InvalidTypeConverterClass.MyEnumValues' is not compatible with the type 'System.Collections.Generic.ICollection`1[System.Text.Json.Serialization.Tests.CustomConverterTests+InvalidTypeConverterEnum]'."
            Assert.Contains("'System.Text.Json.Serialization.Tests.CustomConverterTests+InvalidTypeConverterClass.MyEnumValues'", ex.Message);

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Deserializer.DeserializeWrapper<InvalidTypeConverterClass>("{}"));
            Assert.Contains("'System.Text.Json.Serialization.Tests.CustomConverterTests+InvalidTypeConverterClass.MyEnumValues'", ex.Message);
            Assert.Contains("'System.Collections.Generic.ICollection`1[System.Text.Json.Serialization.Tests.CustomConverterTests+InvalidTypeConverterEnum]'", ex.Message);
        }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        private class InvalidTypeConverterClassWithAttribute { }

        [Fact]
        public async Task AttributeOnClassFail()
        {
            const string expectedSubStr = "'System.Text.Json.Serialization.Tests.CustomConverterTests+InvalidTypeConverterClassWithAttribute'";

            InvalidOperationException ex;

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new InvalidTypeConverterClassWithAttribute()));
            // Message should be in the form "The converter specified on 'System.Text.Json.Serialization.Tests.CustomConverterTests+InvalidTypeConverterClassWithAttribute' is not compatible with the type 'System.Text.Json.Serialization.Tests.CustomConverterTests+InvalidTypeConverterClassWithAttribute'."

            int pos = ex.Message.IndexOf(expectedSubStr);
            Assert.True(pos > 0);
            Assert.Contains(expectedSubStr, ex.Message.Substring(pos + expectedSubStr.Length)); // The same string is repeated again.

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Deserializer.DeserializeWrapper<InvalidTypeConverterClassWithAttribute>("{}"));
            pos = ex.Message.IndexOf(expectedSubStr);
            Assert.True(pos > 0);
            Assert.Contains(expectedSubStr, ex.Message.Substring(pos + expectedSubStr.Length));
        }

        private class ConverterFactoryThatReturnsNull : JsonConverterFactory
        {
            public override bool CanConvert(Type typeToConvert)
            {
                // To verify the nullable converter, don't convert Nullable.
                return Nullable.GetUnderlyingType(typeToConvert) == null;
            }

            public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
            {
                return null;
            }
        }

        [Fact]
        public async Task ConverterThatReturnsNullFail()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new ConverterFactoryThatReturnsNull());

            // A null return value from CreateConverter() will generate a InvalidOperationException with the type name.
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(0, options));
            Assert.Contains(typeof(ConverterFactoryThatReturnsNull).ToString(), ex.Message);

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Deserializer.DeserializeWrapper<int>("0", options));
            Assert.Contains(typeof(ConverterFactoryThatReturnsNull).ToString(), ex.Message);

            // This will invoke the Nullable converter which should detect a null converter.
            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Deserializer.DeserializeWrapper<int?>("0", options));
            Assert.Contains(typeof(ConverterFactoryThatReturnsNull).ToString(), ex.Message);
        }

        private class Level1
        {
            public Level1()
            {
                Level2 = new Level2();
                Level2.Level3s = new Level3[] { new Level3() };
            }

            public Level2 Level2 { get; set; }
        }

        private class Level2
        {
            public Level3[] Level3s {get; set; }
        }

        private class Level3
        {
            // If true, read\write too much instead of too little.
            public bool ReadWriteTooMuch { get; set; }
        }

        private class Level3ConverterThatsBad: JsonConverter<Level3>
        {
            public override Level3 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

                reader.Read();
                Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

                reader.Read();
                Assert.True(reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False);

                // Determine if we should read too much.
                if (reader.TokenType == JsonTokenType.True)
                {
                    // Do the correct read.
                    reader.Read();
                    Assert.Equal(JsonTokenType.EndObject, reader.TokenType);

                    // Do an extra read.
                    reader.Read();
                    Assert.Equal(JsonTokenType.EndArray, reader.TokenType);

                    // End on EndObject token so it looks good, but wrong depth.
                    reader.Read();
                    Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
                }

                return new Level3();
            }

            public override void Write(Utf8JsonWriter writer, Level3 value, JsonSerializerOptions options)
            {
                if (value.ReadWriteTooMuch)
                {
                    writer.WriteStartObject();
                }
            }
        }

        [Fact]
        public async Task ConverterReadTooLittle()
        {
            const string json = @"{""Level2"":{""Level3s"":[{""ReadWriteTooMuch"":false}]}}";

            var options = new JsonSerializerOptions();
            options.Converters.Add(new Level3ConverterThatsBad());

            try
            {
                await Deserializer.DeserializeWrapper<Level1>(json, options);
                Assert.True(false, "Expected exception");
            }
            catch (JsonException ex)
            {
                Assert.Contains("$.Level2.Level3s[0]", ex.ToString());
                Assert.Equal("$.Level2.Level3s[0]", ex.Path);
            }
        }

        [Fact]
        public async Task ConverterReadTooMuch()
        {
            const string json = @"{""Level2"":{""Level3s"":[{""ReadWriteTooMuch"":true}]}}";

            var options = new JsonSerializerOptions();
            options.Converters.Add(new Level3ConverterThatsBad ());

            try
            {
                await Deserializer.DeserializeWrapper<Level1>(json, options);
                Assert.True(false, "Expected exception");
            }
            catch (JsonException ex)
            {
                Assert.Contains("$.Level2.Level3s[0]", ex.ToString());
                Assert.Equal("$.Level2.Level3s[0]", ex.Path);
            }
        }

        [Fact]
        public async Task ConverterWroteNothing()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new Level3ConverterThatsBad());

            // Not writing is allowed.
            string str = await Serializer.SerializeWrapper(new Level1(), options);
            Assert.False(string.IsNullOrEmpty(str));
        }

        [Fact]
        public async Task ConverterWroteTooMuch()
        {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new Level3ConverterThatsBad());

            try
            {
                var l1 = new Level1();
                l1.Level2.Level3s[0].ReadWriteTooMuch = true;

                await Serializer.SerializeWrapper(l1, options);
                Assert.True(false, "Expected exception");
            }
            catch (JsonException ex)
            {
                Assert.Contains("$.Level2.Level3s", ex.ToString());
                Assert.Equal("$.Level2.Level3s", ex.Path);
            }
        }

        private class PocoWithTwoConvertersOnProperty
        {
            [InvalidConverter]
            [PointConverter]
            public int MyInt { get; set; }
        }

        [Fact]
        public async Task PropertyHasMoreThanOneConverter()
        {
            InvalidOperationException ex;

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new PocoWithTwoConvertersOnProperty()));
            // Message should be in the form "The attribute 'System.Text.Json.Serialization.JsonConverterAttribute' cannot exist more than once on 'System.Text.Json.Serialization.Tests.CustomConverterTests+PocoWithTwoConvertersOnProperty.MyInt'."
            Assert.Contains("'System.Text.Json.Serialization.JsonConverterAttribute'", ex.Message);
            Assert.Contains("'System.Text.Json.Serialization.Tests.CustomConverterTests+PocoWithTwoConvertersOnProperty.MyInt'", ex.Message);

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Deserializer.DeserializeWrapper<PocoWithTwoConvertersOnProperty>("{}"));
            Assert.Contains("'System.Text.Json.Serialization.JsonConverterAttribute'", ex.Message);
            Assert.Contains("'System.Text.Json.Serialization.Tests.CustomConverterTests+PocoWithTwoConvertersOnProperty.MyInt'", ex.Message);
        }

        [InvalidConverter]
        [PointConverter]
        private class PocoWithTwoConverters
        {
            public int MyInt { get; set; }
        }

        [Fact]
        public async Task TypeHasMoreThanOneConverter()
        {
            InvalidOperationException ex;

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new PocoWithTwoConverters()));
            // Message should be in the form "The attribute 'System.Text.Json.Serialization.JsonConverterAttribute' cannot exist more than once on 'System.Text.Json.Serialization.Tests.CustomConverterTests+PocoWithTwoConverters'."
            Assert.Contains("'System.Text.Json.Serialization.JsonConverterAttribute'", ex.Message);
            Assert.Contains("'System.Text.Json.Serialization.Tests.CustomConverterTests+PocoWithTwoConverters'", ex.Message);

            ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Deserializer.DeserializeWrapper<PocoWithTwoConverters>("{}"));
            Assert.Contains("'System.Text.Json.Serialization.JsonConverterAttribute'", ex.Message);
            Assert.Contains("'System.Text.Json.Serialization.Tests.CustomConverterTests+PocoWithTwoConverters'", ex.Message);
        }

        [Fact]
        public async Task ConverterWithoutDefaultCtor()
        {
            string json = @"{""MyType"":""ABC""}";

            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(async () => await Deserializer.DeserializeWrapper<ClassWithConverterWithoutPublicEmptyCtor>(json));
            Assert.Contains("'System.Text.Json.Serialization.Tests.CustomConverterTests+ClassWithConverterWithoutPublicEmptyCtor'", ex.Message);
        }

        [JsonConverter(typeof(ConverterWithoutPublicEmptyCtor))]
        public class ClassWithConverterWithoutPublicEmptyCtor
        {
            public string MyType { get; set; }
        }

        internal class ConverterWithoutPublicEmptyCtor : JsonConverter<ClassWithConverterWithoutPublicEmptyCtor>
        {
            public ConverterWithoutPublicEmptyCtor(int x)
            {
            }

            public override ClassWithConverterWithoutPublicEmptyCtor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }

            public override void Write(Utf8JsonWriter writer, ClassWithConverterWithoutPublicEmptyCtor value, JsonSerializerOptions options)
            {
                throw new NotImplementedException();
            }
        }
    }
}
