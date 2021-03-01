// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Tests;
using System.Threading.Tasks;
using Xunit;

[assembly: JsonSerializable(typeof(OverridePropertyNameDesignTime_TestClass))]
[assembly: JsonSerializable(typeof(EmptyPropertyName_TestClass))]
[assembly: JsonSerializable(typeof(ClassWithUnicodeProperty))]

namespace System.Text.Json.Serialization.Tests
{
#if !GENERATE_JSON_METADATA
    public class PropertyNameTests_DynamicSerializer : PropertyNameTests
    {
        public PropertyNameTests_DynamicSerializer() : base(SerializationWrapper.StringSerializer, DeserializationWrapper.StringDeserializer) { }
    }
#else
    public class PropertyNameTests_MetadataBasedSerializer : PropertyNameTests
    {
        public PropertyNameTests_MetadataBasedSerializer() : base(SerializationWrapper.StringMetadataSerializer, DeserializationWrapper.StringMetadataDeserialzer) { }
    }
#endif

    public abstract class PropertyNameTests : SerializerTests
    {
        public PropertyNameTests(SerializationWrapper serializer, DeserializationWrapper deserializer) : base(serializer, deserializer) { }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task CamelCaseDeserializeNoMatch()
        {
            var options = new JsonSerializerOptions();
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

            SimpleTestClass obj = await Deserializer.DeserializeWrapper<SimpleTestClass>(@"{""MyInt16"":1}", options);

            // This is 0 (default value) because the data does not match the property "MyInt16" that is assuming camel-casing of "myInt16".
            Assert.Equal(0, obj.MyInt16);
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task CamelCaseDeserializeMatch()
        {
            var options = new JsonSerializerOptions();
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

            SimpleTestClass obj = await Deserializer.DeserializeWrapper<SimpleTestClass>(@"{""myInt16"":1}", options);

            // This is 1 because the data matches the property "MyInt16" that is assuming camel-casing of "myInt16".
            Assert.Equal(1, obj.MyInt16);
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task CamelCaseSerialize()
        {
            var options = new JsonSerializerOptions();
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

            SimpleTestClass obj = await Deserializer.DeserializeWrapper<SimpleTestClass>(@"{}", options);

            string json = await Serializer.SerializeWrapper(obj, options);
            Assert.Contains(@"""myInt16"":0", json);
            Assert.Contains(@"""myInt32"":0", json);
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task CustomNamePolicy()
        {
            var options = new JsonSerializerOptions();
            options.PropertyNamingPolicy = new UppercaseNamingPolicy();

            SimpleTestClass obj = await Deserializer.DeserializeWrapper<SimpleTestClass>(@"{""MYINT16"":1}", options);

            // This is 1 because the data matches the property "MYINT16" that is uppercase of "myInt16".
            Assert.Equal(1, obj.MyInt16);
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task NullNamePolicy()
        {
            var options = new JsonSerializerOptions();
            options.PropertyNamingPolicy = new NullNamingPolicy();

            // A policy that returns null is not allowed.
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Deserializer.DeserializeWrapper<SimpleTestClass>(@"{}", options));
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new SimpleTestClass(), options));
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task IgnoreCase()
        {
            {
                // A non-match scenario with no options (case-sensitive by default).
                SimpleTestClass obj = await Deserializer.DeserializeWrapper<SimpleTestClass>(@"{""myint16"":1}");
                Assert.Equal(0, obj.MyInt16);
            }

            {
                // A non-match scenario with default options (case-sensitive by default).
                var options = new JsonSerializerOptions();
                SimpleTestClass obj = await Deserializer.DeserializeWrapper<SimpleTestClass>(@"{""myint16"":1}", options);
                Assert.Equal(0, obj.MyInt16);
            }

            {
                var options = new JsonSerializerOptions();
                options.PropertyNameCaseInsensitive = true;
                SimpleTestClass obj = await Deserializer.DeserializeWrapper<SimpleTestClass>(@"{""myint16"":1}", options);
                Assert.Equal(1, obj.MyInt16);
            }
        }

        [Fact]
        public async Task JsonPropertyNameAttribute()
        {
            {
                OverridePropertyNameDesignTime_TestClass obj = await Deserializer.DeserializeWrapper<OverridePropertyNameDesignTime_TestClass>(@"{""Blah"":1}");
                Assert.Equal(1, obj.myInt);

                obj.myObject = 2;

                string json = await Serializer.SerializeWrapper(obj);
                Assert.Contains(@"""Blah"":1", json);
                Assert.Contains(@"""BlahObject"":2", json);
            }

            // The JsonPropertyNameAttribute should be unaffected by JsonNamingPolicy and PropertyNameCaseInsensitive.
            {
                var options = new JsonSerializerOptions();
                options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.PropertyNameCaseInsensitive = true;

                OverridePropertyNameDesignTime_TestClass obj = await Deserializer.DeserializeWrapper<OverridePropertyNameDesignTime_TestClass>(@"{""Blah"":1}", options);
                Assert.Equal(1, obj.myInt);

                string json = await Serializer.SerializeWrapper(obj);
                Assert.Contains(@"""Blah"":1", json);
            }
        }

        [Fact]
        public async Task JsonNameAttributeDuplicateDesignTimeFail()
        {
            {
                var options = new JsonSerializerOptions();
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await Deserializer.DeserializeWrapper<DuplicatePropertyNameDesignTime_TestClass>("{}", options));
            }

            {
                var options = new JsonSerializerOptions();
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new DuplicatePropertyNameDesignTime_TestClass(), options));
            }
        }

        [Fact]
        public async Task JsonNullNameAttribute()
        {
            var options = new JsonSerializerOptions();
            options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            options.PropertyNameCaseInsensitive = true;

            // A null name in JsonPropertyNameAttribute is not allowed.
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new NullPropertyName_TestClass(), options));
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task JsonNameConflictOnCamelCasingFail()
        {
            {
                // Baseline comparison - no options set.
                IntPropertyNamesDifferentByCaseOnly_TestClass obj = await Deserializer.DeserializeWrapper<IntPropertyNamesDifferentByCaseOnly_TestClass>("{}");
                await Serializer.SerializeWrapper(obj);
            }

            {
                var options = new JsonSerializerOptions();
                options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await Deserializer.DeserializeWrapper<IntPropertyNamesDifferentByCaseOnly_TestClass>("{}", options));
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new IntPropertyNamesDifferentByCaseOnly_TestClass(), options));
            }

            {
                // Baseline comparison - no options set.
                ObjectPropertyNamesDifferentByCaseOnly_TestClass obj = await Deserializer.DeserializeWrapper<ObjectPropertyNamesDifferentByCaseOnly_TestClass>("{}");
                await Serializer.SerializeWrapper(obj);
            }

            {
                var options = new JsonSerializerOptions();
                options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await Deserializer.DeserializeWrapper<ObjectPropertyNamesDifferentByCaseOnly_TestClass>("{}", options));
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new ObjectPropertyNamesDifferentByCaseOnly_TestClass(), options));
            }
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task JsonNameConflictOnCaseInsensitiveFail()
        {
            string json = @"{""myInt"":1,""MyInt"":2}";

            {
                var options = new JsonSerializerOptions();
                options.PropertyNameCaseInsensitive = true;

                await Assert.ThrowsAsync<InvalidOperationException>(async () => await Deserializer.DeserializeWrapper<IntPropertyNamesDifferentByCaseOnly_TestClass>(json, options));
                await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(new IntPropertyNamesDifferentByCaseOnly_TestClass(), options));
            }
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task JsonOutputNotAffectedByCasingPolicy()
        {
            {
                // Baseline.
                string json = await Serializer.SerializeWrapper(new SimpleTestClass());
                Assert.Contains(@"""MyInt16"":0", json);
            }

            // The JSON output should be unaffected by PropertyNameCaseInsensitive.
            {
                var options = new JsonSerializerOptions();
                options.PropertyNameCaseInsensitive = true;

                string json = await Serializer.SerializeWrapper(new SimpleTestClass(), options);
                Assert.Contains(@"""MyInt16"":0", json);
            }
        }

        [Fact]
        public async Task EmptyPropertyName()
        {
            string json = @"{"""":1}";

            {
                var obj = new EmptyPropertyName_TestClass();
                obj.MyInt1 = 1;

                string jsonOut = await Serializer.SerializeWrapper(obj);
                Assert.Equal(json, jsonOut);
            }

            {
                EmptyPropertyName_TestClass obj = await Deserializer.DeserializeWrapper<EmptyPropertyName_TestClass>(json);
                Assert.Equal(1, obj.MyInt1);
            }
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task EmptyPropertyNameInExtensionData()
        {
            {
                string json = @"{"""":42}";
                EmptyClassWithExtensionProperty obj = await Deserializer.DeserializeWrapper<EmptyClassWithExtensionProperty>(json);
                Assert.Equal(1, obj.MyOverflow.Count);
                Assert.Equal(42, obj.MyOverflow[""].GetInt32());
            }

            {
                // Verify that last-in wins.
                string json = @"{"""":42, """":43}";
                EmptyClassWithExtensionProperty obj = await Deserializer.DeserializeWrapper<EmptyClassWithExtensionProperty>(json);
                Assert.Equal(1, obj.MyOverflow.Count);
                Assert.Equal(43, obj.MyOverflow[""].GetInt32());
            }
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task EmptyPropertyName_WinsOver_ExtensionDataEmptyPropertyName()
        {
            string json = @"{"""":1}";

            ClassWithEmptyPropertyNameAndExtensionProperty obj;

            // Create a new options instances to re-set any caches.
            JsonSerializerOptions options = new JsonSerializerOptions();

            // Verify the real property wins over the extension data property.
            obj = await Deserializer.DeserializeWrapper<ClassWithEmptyPropertyNameAndExtensionProperty>(json, options);
            Assert.Equal(1, obj.MyInt1);
            Assert.Null(obj.MyOverflow);
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task EmptyPropertyNameAndExtensionData_ExtDataFirst()
        {
            // Verify any caching treats real property (with empty name) differently than a missing property.

            ClassWithEmptyPropertyNameAndExtensionProperty obj;

            // Create a new options instances to re-set any caches.
            JsonSerializerOptions options = new JsonSerializerOptions();

            // First populate cache with a missing property name.
            string json = @"{""DoesNotExist"":42}";
            obj = await Deserializer.DeserializeWrapper<ClassWithEmptyPropertyNameAndExtensionProperty>(json, options);
            Assert.Equal(0, obj.MyInt1);
            Assert.Equal(1, obj.MyOverflow.Count);
            Assert.Equal(42, obj.MyOverflow["DoesNotExist"].GetInt32());

            // Then use an empty property.
            json = @"{"""":43}";
            obj = await Deserializer.DeserializeWrapper<ClassWithEmptyPropertyNameAndExtensionProperty>(json, options);
            Assert.Equal(43, obj.MyInt1);
            Assert.Null(obj.MyOverflow);
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task EmptyPropertyAndExtensionData_PropertyFirst()
        {
            // Verify any caching treats real property (with empty name) differently than a missing property.

            ClassWithEmptyPropertyNameAndExtensionProperty obj;

            // Create a new options instances to re-set any caches.
            JsonSerializerOptions options = new JsonSerializerOptions();

            // First use an empty property.
            string json = @"{"""":43}";
            obj = await Deserializer.DeserializeWrapper<ClassWithEmptyPropertyNameAndExtensionProperty>(json, options);
            Assert.Equal(43, obj.MyInt1);
            Assert.Null(obj.MyOverflow);

            // Then populate cache with a missing property name.
            json = @"{""DoesNotExist"":42}";
            obj = await Deserializer.DeserializeWrapper<ClassWithEmptyPropertyNameAndExtensionProperty>(json, options);
            Assert.Equal(0, obj.MyInt1);
            Assert.Equal(1, obj.MyOverflow.Count);
            Assert.Equal(42, obj.MyOverflow["DoesNotExist"].GetInt32());
        }

        [Fact]
        public async Task UnicodePropertyNames()
        {
            ClassWithUnicodeProperty obj = await Deserializer.DeserializeWrapper<ClassWithUnicodeProperty>("{\"A\u0467\":1}");
            Assert.Equal(1, obj.A\u0467);

            // Specifying encoder on options does not impact deserialize.
            var options = new JsonSerializerOptions();
            options.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping;

            obj = await Deserializer.DeserializeWrapper<ClassWithUnicodeProperty>("{\"A\u0467\":1}", options);
            Assert.Equal(1, obj.A\u0467);

            string json;

            // Verify the name is escaped after serialize.
            json = await Serializer.SerializeWrapper(obj);
            Assert.Contains(@"""A\u0467"":1", json);

            // With custom escaper
            json = await Serializer.SerializeWrapper(obj, options);
            Assert.Contains("\"A\u0467\":1", json);

            // Verify the name is unescaped after deserialize.
            obj = await Deserializer.DeserializeWrapper<ClassWithUnicodeProperty>(json);
            Assert.Equal(1, obj.A\u0467);

            // With custom escaper
            obj = await Deserializer.DeserializeWrapper<ClassWithUnicodeProperty>(json, options);
            Assert.Equal(1, obj.A\u0467);
        }

        [Fact]
        public async Task UnicodePropertyNamesWithPooledAlloc()
        {
            // We want to go over StackallocThreshold=256 to force a pooled allocation, so this property is 400 chars and 401 bytes.
            ClassWithUnicodeProperty obj = await Deserializer.DeserializeWrapper<ClassWithUnicodeProperty>("{\"A\u046734567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890\":1}");
            Assert.Equal(1, obj.A\u046734567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890);

            // Verify the name is escaped after serialize.
            string json = await Serializer.SerializeWrapper(obj);
            Assert.Contains(@"""A\u046734567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"":1", json);

            // Verify the name is unescaped after deserialize.
            obj = await Deserializer.DeserializeWrapper<ClassWithUnicodeProperty>(json);
            Assert.Equal(1, obj.A\u046734567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890);
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task ExtensionDataDictionarySerialize_DoesNotHonor()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            EmptyClassWithExtensionProperty obj = await Deserializer.DeserializeWrapper<EmptyClassWithExtensionProperty>(@"{""Key1"": 1}", options);

            // Ignore naming policy for extension data properties by default.
            Assert.False(obj.MyOverflow.ContainsKey("key1"));
            Assert.Equal(1, obj.MyOverflow["Key1"].GetInt32());
        }

        private class ClassWithPropertyNamePermutations
        {
            public int a { get; set; }
            public int aa { get; set; }
            public int aaa { get; set; }
            public int aaaa { get; set; }
            public int aaaaa { get; set; }
            public int aaaaaa { get; set; }

            // 7 characters - caching code only keys up to 7.
            public int aaaaaaa { get; set; }
            public int aaaaaab { get; set; }

            // 8 characters.
            public int aaaaaaaa { get; set; }
            public int aaaaaaab { get; set; }

            // 9 characters.
            public int aaaaaaaaa { get; set; }
            public int aaaaaaaab { get; set; }

            public int \u0467 { get; set; }
            public int \u0467\u0467 { get; set; }
            public int \u0467\u0467a { get; set; }
            public int \u0467\u0467b { get; set; }
            public int \u0467\u0467\u0467 { get; set; }
            public int \u0467\u0467\u0467a { get; set; }
            public int \u0467\u0467\u0467b { get; set; }
            public int \u0467\u0467\u0467\u0467 { get; set; }
            public int \u0467\u0467\u0467\u0467a { get; set; }
            public int \u0467\u0467\u0467\u0467b { get; set; }
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task CachingKeys()
        {
            ClassWithPropertyNamePermutations obj;

            void Verify()
            {
                Assert.Equal(1, obj.a);
                Assert.Equal(2, obj.aa);
                Assert.Equal(3, obj.aaa);
                Assert.Equal(4, obj.aaaa);
                Assert.Equal(5, obj.aaaaa);
                Assert.Equal(6, obj.aaaaaa);
                Assert.Equal(7, obj.aaaaaaa);
                Assert.Equal(7, obj.aaaaaab);
                Assert.Equal(8, obj.aaaaaaaa);
                Assert.Equal(8, obj.aaaaaaab);
                Assert.Equal(9, obj.aaaaaaaaa);
                Assert.Equal(9, obj.aaaaaaaab);

                Assert.Equal(2, obj.\u0467);
                Assert.Equal(4, obj.\u0467\u0467);
                Assert.Equal(5, obj.\u0467\u0467a);
                Assert.Equal(5, obj.\u0467\u0467b);
                Assert.Equal(6, obj.\u0467\u0467\u0467);
                Assert.Equal(7, obj.\u0467\u0467\u0467a);
                Assert.Equal(7, obj.\u0467\u0467\u0467b);
                Assert.Equal(8, obj.\u0467\u0467\u0467\u0467);
                Assert.Equal(9, obj.\u0467\u0467\u0467\u0467a);
                Assert.Equal(9, obj.\u0467\u0467\u0467\u0467b);
            }

            obj = new ClassWithPropertyNamePermutations
            {
                a = 1,
                aa = 2,
                aaa = 3,
                aaaa = 4,
                aaaaa = 5,
                aaaaaa = 6,
                aaaaaaa = 7,
                aaaaaab = 7,
                aaaaaaaa = 8,
                aaaaaaab = 8,
                aaaaaaaaa = 9,
                aaaaaaaab = 9,
                \u0467 = 2,
                \u0467\u0467 = 4,
                \u0467\u0467a = 5,
                \u0467\u0467b = 5,
                \u0467\u0467\u0467 = 6,
                \u0467\u0467\u0467a = 7,
                \u0467\u0467\u0467b = 7,
                \u0467\u0467\u0467\u0467 = 8,
                \u0467\u0467\u0467\u0467a = 9,
                \u0467\u0467\u0467\u0467b = 9,
            };

            // Verify baseline.
            Verify();

            string json = await Serializer.SerializeWrapper(obj);

            // Verify the length is consistent with a verified value.
            Assert.Equal(354, json.Length);

            obj = await Deserializer.DeserializeWrapper<ClassWithPropertyNamePermutations>(json);

            // Verify round-tripped object.
            Verify();
        }

        [Theory]
        [InlineData(0x1, 'v')]
        [InlineData(0x1, '\u0467')]
        [InlineData(0x10, 'v')]
        [InlineData(0x10, '\u0467')]
        [InlineData(0x100, 'v')]
        [InlineData(0x100, '\u0467')]
        [InlineData(0x1000, 'v')]
        [InlineData(0x1000, '\u0467')]
        [InlineData(0x10000, 'v')]
        [InlineData(0x10000, '\u0467')]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
        // Needs support for JsonExtensionData
#endif
        public async Task LongPropertyNames(int propertyLength, char ch)
        {
            // Although the CLR may limit member length to 1023 bytes, the serializer doesn't have a hard limit.

            string val = new string(ch, propertyLength);
            string json = @"{""" + val + @""":1}";

            EmptyClassWithExtensionProperty obj = await Deserializer.DeserializeWrapper<EmptyClassWithExtensionProperty>(json);

            Assert.True(obj.MyOverflow.ContainsKey(val));

            var options = new JsonSerializerOptions
            {
                // Avoid escaping '\u0467'.
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            string jsonRoundTripped = await Serializer.SerializeWrapper(obj, options);
            Assert.Equal(json, jsonRoundTripped);
        }

        [Fact]
        public async Task BadNamingPolicy_ThrowsInvalidOperation()
        {
            var options = new JsonSerializerOptions { DictionaryKeyPolicy = new NullNamingPolicy() };

            var inputPrimitive = new Dictionary<string, int>
            {
                { "validKey", 1 }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(inputPrimitive, options));

            var inputClass = new Dictionary<string, OverridePropertyNameDesignTime_TestClass>
            {
                { "validKey", new OverridePropertyNameDesignTime_TestClass() }
            };

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await Serializer.SerializeWrapper(inputClass, options));
        }
    }

    public class OverridePropertyNameDesignTime_TestClass
    {
        [JsonPropertyName("Blah")]
        public int myInt { get; set; }

        [JsonPropertyName("BlahObject")]
        public object myObject { get; set; }
    }

    public class DuplicatePropertyNameDesignTime_TestClass
    {
        [JsonPropertyName("Blah")]
        public int MyInt1 { get; set; }

        [JsonPropertyName("Blah")]
        public int MyInt2 { get; set; }
    }

    public class EmptyPropertyName_TestClass
    {
        [JsonPropertyName("")]
        public int MyInt1 { get; set; }
    }

    public class NullPropertyName_TestClass
    {
        [JsonPropertyName(null)]
        public int MyInt1 { get; set; }
    }

    public class IntPropertyNamesDifferentByCaseOnly_TestClass
    {
        public int myInt { get; set; }
        public int MyInt { get; set; }
    }

    public class ObjectPropertyNamesDifferentByCaseOnly_TestClass
    {
        public int myObject { get; set; }
        public int MyObject { get; set; }
    }

    public class UppercaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            return name.ToUpperInvariant();
        }
    }

    public class NullNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
        {
            return null;
        }
    }

    public class EmptyClassWithExtensionProperty
    {
        [JsonExtensionData]
        public IDictionary<string, JsonElement> MyOverflow { get; set; }
    }

    public class ClassWithEmptyPropertyNameAndExtensionProperty
    {
        [JsonPropertyName("")]
        public int MyInt1 { get; set; }

        [JsonExtensionData]
        public IDictionary<string, JsonElement> MyOverflow { get; set; }
    }
}
