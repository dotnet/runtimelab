// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Json.Serialization.Tests
{
    public abstract partial class CustomConverterTests
    {
        // A custom long[] converter as comma-delimited string "1,2,3".
        internal class LongArrayConverter : JsonConverter<long[]>
        {
            public LongArrayConverter() { }

            public override long[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                string json = reader.GetString();

                var list = new List<long>();

                foreach (string str in json.Split(','))
                {
                    if (!long.TryParse(str, out long l))
                    {
                        throw new JsonException("Too big for a long");
                    }

                    list.Add(l);
                }

                return list.ToArray();
            }

            public override void Write(Utf8JsonWriter writer, long[] value, JsonSerializerOptions options)
            {
                var builder = new StringBuilder();

                for (int i = 0; i < value.Length; i++)
                {
                    builder.Append(value[i].ToString());

                    if (i != value.Length - 1)
                    {
                        builder.Append(",");
                    }
                }

                writer.WriteStringValue(builder.ToString());
            }
        }

        [Fact]
        public async Task CustomArrayConverterAsRoot()
        {
            const string json = @"""1,2,3""";

            var options = new JsonSerializerOptions();
            options.Converters.Add(new LongArrayConverter());

            long[] arr = await Deserializer.DeserializeWrapper<long[]>(json, options);
            Assert.Equal(1, arr[0]);
            Assert.Equal(2, arr[1]);
            Assert.Equal(3, arr[2]);

            string jsonSerialized = await Serializer.SerializeWrapper(arr, options);
            Assert.Equal(json, jsonSerialized);
        }

        [Fact]
        public async Task CustomArrayConverterFail()
        {
            string json = $"\"{Int64.MaxValue.ToString()}0\"";

            var options = new JsonSerializerOptions();
            options.Converters.Add(new LongArrayConverter());

            try
            {
                await Deserializer.DeserializeWrapper<long[]>(json, options);
                Assert.True(false, "Expected exception");
            }
            catch (JsonException ex)
            {
                Assert.Null(ex.InnerException);
                Assert.Equal("$", ex.Path);
                Assert.Equal("Too big for a long", ex.Message);
            }
        }

        private class ClassWithProperty
        {
            public long[] Array1 { get; set; }
            public long[] Array2 { get; set; }
        }

        [Fact]
        public async Task CustomArrayConverterInProperty()
        {
            const string json = @"{""Array1"":""1,2,3"",""Array2"":""4,5""}";

            var options = new JsonSerializerOptions();
            options.Converters.Add(new LongArrayConverter());

            ClassWithProperty obj = await Deserializer.DeserializeWrapper<ClassWithProperty>(json, options);
            Assert.Equal(1, obj.Array1[0]);
            Assert.Equal(2, obj.Array1[1]);
            Assert.Equal(3, obj.Array1[2]);
            Assert.Equal(4, obj.Array2[0]);
            Assert.Equal(5, obj.Array2[1]);

            string jsonSerialized = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal(json, jsonSerialized);
        }
    }
}
