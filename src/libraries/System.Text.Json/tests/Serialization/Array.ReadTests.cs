// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Tests;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Json.Serialization.Tests
{
#if !GENERATE_JSON_METADATA
    public class ArrayTests_DynamicSerializer : ArrayTests
    {
        public ArrayTests_DynamicSerializer() : base(SerializationWrapper.StringSerializer, DeserializationWrapper.StringDeserializer) { }
    }
#else
    public class ArrayTests_MetadataBasedSerializer : ArrayTests
    {
        public ArrayTests_MetadataBasedSerializer() : base(SerializationWrapper.StringMetadataSerializer, DeserializationWrapper.StringMetadataDeserialzer) { }
    }
#endif

    public abstract partial class ArrayTests
    {
        private SerializationWrapper Serializer { get; }

        private DeserializationWrapper Deserializer { get; }

        public ArrayTests(SerializationWrapper serializer, DeserializationWrapper deserializer)
        {
            Serializer = serializer;
            Deserializer = deserializer;
        }

        [Fact]
        public async Task ReadObjectArray()
        {
            string data =
                "[" +
                SimpleTestClass.s_json +
                "," +
                SimpleTestClass.s_json +
                "]";

            SimpleTestClass[] i = await Deserializer.DeserializeWrapper<SimpleTestClass[]>(data);

            i[0].Verify();
            i[1].Verify();
        }

        [Fact]
        public async Task ReadNullByteArray()
        {
            string json = @"null";
            byte[] arr = await Deserializer.DeserializeWrapper<byte[]>(json);
            Assert.Null(arr);
        }

        [Fact]
        public async Task ReadEmptyByteArray()
        {
            string json = @"""""";
            byte[] arr = await Deserializer.DeserializeWrapper<byte[]>(json);
            Assert.Equal(0, arr.Length);
        }

        [Fact]
        public async Task ReadByteArray()
        {
            string json = $"\"{Convert.ToBase64String(new byte[] { 1, 2 })}\"";
            byte[] arr = await Deserializer.DeserializeWrapper<byte[]>(json);

            Assert.Equal(2, arr.Length);
            Assert.Equal(1, arr[0]);
            Assert.Equal(2, arr[1]);
        }

        [Fact]
        public async Task Read2dByteArray()
        {
            // Baseline for comparison.
            Assert.Equal("AQI=", Convert.ToBase64String(new byte[] { 1, 2 }));

            string json = "[\"AQI=\",\"AQI=\"]";
            byte[][] arr = await Deserializer.DeserializeWrapper<byte[][]>(json);
            Assert.Equal(2, arr.Length);

            Assert.Equal(2, arr[0].Length);
            Assert.Equal(1, arr[0][0]);
            Assert.Equal(2, arr[0][1]);

            Assert.Equal(2, arr[1].Length);
            Assert.Equal(1, arr[1][0]);
            Assert.Equal(2, arr[1][1]);
        }

        [Theory]
        [InlineData(@"""1""")]
        [InlineData(@"""A===""")]
        [InlineData(@"[1, 2]")]  // Currently not support deserializing JSON arrays as byte[] - only Base64 string.
        public async Task ReadByteArrayFail(string json)
        {
            await Assert.ThrowsAsync<JsonException>(() => Deserializer.DeserializeWrapper<byte[]>(json));
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/issues/388")]
#endif
        public async Task ReadByteListAsJsonArray()
        {
            string json = $"[1, 2]";
            List<byte> list = await Deserializer.DeserializeWrapper<List<byte>>(json);

            Assert.Equal(2, list.Count);
            Assert.Equal(1, list[0]);
            Assert.Equal(2, list[1]);
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task DeserializeObjectArray()
        {
            // https://github.com/dotnet/runtime/issues/29019
            object[] data = await Deserializer.DeserializeWrapper<object[]>("[1]");
            Assert.Equal(1, data.Length);
            Assert.IsType<JsonElement>(data[0]);
            Assert.Equal(1, ((JsonElement)data[0]).GetInt32());
        }

        [Fact]
        public async Task ReadEmptyObjectArray()
        {
            SimpleTestClass[] data = await Deserializer.DeserializeWrapper<SimpleTestClass[]>("[{}]");
            Assert.Equal(1, data.Length);
            Assert.NotNull(data[0]);
        }

        [Fact]
        public async Task ReadPrimitiveJagged2dArray()
        {
            int[][] i = await Deserializer.DeserializeWrapper<int[][]>(Encoding.UTF8.GetBytes(@"[[1,2],[3,4]]"));
            Assert.Equal(1, i[0][0]);
            Assert.Equal(2, i[0][1]);
            Assert.Equal(3, i[1][0]);
            Assert.Equal(4, i[1][1]);
        }

        [Fact]
        public async Task ReadPrimitiveJagged3dArray()
        {
            int[][][] i = await Deserializer.DeserializeWrapper<int[][][]>(Encoding.UTF8.GetBytes(@"[[[11,12],[13,14]], [[21,22],[23,24]]]"));
            Assert.Equal(11, i[0][0][0]);
            Assert.Equal(12, i[0][0][1]);
            Assert.Equal(13, i[0][1][0]);
            Assert.Equal(14, i[0][1][1]);

            Assert.Equal(21, i[1][0][0]);
            Assert.Equal(22, i[1][0][1]);
            Assert.Equal(23, i[1][1][0]);
            Assert.Equal(24, i[1][1][1]);
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task ReadArrayWithInterleavedComments()
        {
            var options = new JsonSerializerOptions();
            options.ReadCommentHandling = JsonCommentHandling.Skip;

            int[][] i = await Deserializer.DeserializeWrapper<int[][]>(Encoding.UTF8.GetBytes("[[1,2] // Inline [\n,[3, /* Multi\n]] Line*/4]]"), options);
            Assert.Equal(1, i[0][0]);
            Assert.Equal(2, i[0][1]);
            Assert.Equal(3, i[1][0]);
            Assert.Equal(4, i[1][1]);
        }

        [Fact]
        public async Task ReadEmpty()
        {
            SimpleTestClass[] arr = await Deserializer.DeserializeWrapper<SimpleTestClass[]>("[]");
            Assert.Equal(0, arr.Length);

            List<SimpleTestClass> list = await Deserializer.DeserializeWrapper<List<SimpleTestClass>>("[]");
            Assert.Equal(0, list.Count);
        }

        [Fact]
        public async Task ReadPrimitiveArray()
        {
            int[] i = await Deserializer.DeserializeWrapper<int[]>(Encoding.UTF8.GetBytes(@"[1,2]"));
            Assert.Equal(1, i[0]);
            Assert.Equal(2, i[1]);

            i = await Deserializer.DeserializeWrapper<int[]>(Encoding.UTF8.GetBytes(@"[]"));
            Assert.Equal(0, i.Length);
        }

        [Fact]
        public async Task ReadInitializedArrayTest()
        {
            string serialized = "{\"Values\":[1,2,3]}";
            TestClassWithInitializedArray testClassWithInitializedArray = await Deserializer.DeserializeWrapper<TestClassWithInitializedArray>(serialized);

            Assert.Equal(1, testClassWithInitializedArray.Values[0]);
            Assert.Equal(2, testClassWithInitializedArray.Values[1]);
            Assert.Equal(3, testClassWithInitializedArray.Values[2]);
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task ReadArrayWithEnums()
        {
            SampleEnum[] i = await Deserializer.DeserializeWrapper<SampleEnum[]>(Encoding.UTF8.GetBytes(@"[1,2]"));
            Assert.Equal(SampleEnum.One, i[0]);
            Assert.Equal(SampleEnum.Two, i[1]);
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task ReadPrimitiveArrayFail()
        {
            // Invalid data
            await Assert.ThrowsAsync<JsonException>(() => Deserializer.DeserializeWrapper<int[]>(Encoding.UTF8.GetBytes(@"[1,""a""]")));

            // Invalid data
            await Assert.ThrowsAsync<JsonException>(() => Deserializer.DeserializeWrapper<List<int?>>(Encoding.UTF8.GetBytes(@"[1,""a""]")));

            // Multidimensional arrays currently not supported
            await Assert.ThrowsAsync<NotSupportedException>(() => Deserializer.DeserializeWrapper<int[,]>(Encoding.UTF8.GetBytes(@"[[1,2],[3,4]]")));
        }

        public static IEnumerable<object[]> ReadNullJson
        {
            get
            {
                yield return new object[] { $"[null, null, null]", true, true, true };
                yield return new object[] { $"[null, null, {SimpleTestClass.s_json}]", true, true, false };
                yield return new object[] { $"[null, {SimpleTestClass.s_json}, null]", true, false, true };
                yield return new object[] { $"[null, {SimpleTestClass.s_json}, {SimpleTestClass.s_json}]", true, false, false };
                yield return new object[] { $"[{SimpleTestClass.s_json}, {SimpleTestClass.s_json}, {SimpleTestClass.s_json}]", false, false, false };
                yield return new object[] { $"[{SimpleTestClass.s_json}, {SimpleTestClass.s_json}, null]", false, false, true };
                yield return new object[] { $"[{SimpleTestClass.s_json}, null, {SimpleTestClass.s_json}]", false, true, false };
                yield return new object[] { $"[{SimpleTestClass.s_json}, null, null]", false, true, true };
            }
        }

        [Theory]
        [MemberData(nameof(ReadNullJson))]
        public async Task ReadNull(string json, bool element0Null, bool element1Null, bool element2Null)
        {
            SimpleTestClass[] arr = await Deserializer.DeserializeWrapper<SimpleTestClass[]>(json);
            Assert.Equal(3, arr.Length);
            VerifyReadNull(arr[0], element0Null);
            VerifyReadNull(arr[1], element1Null);
            VerifyReadNull(arr[2], element2Null);

            List<SimpleTestClass> list = await Deserializer.DeserializeWrapper<List<SimpleTestClass>>(json);
            Assert.Equal(3, list.Count);
            VerifyReadNull(list[0], element0Null);
            VerifyReadNull(list[1], element1Null);
            VerifyReadNull(list[2], element2Null);

            static void VerifyReadNull(SimpleTestClass obj, bool isNull)
            {
                if (isNull)
                {
                    Assert.Null(obj);
                }
                else
                {
                    obj.Verify();
                }
            }
        }

        [Fact]
        public async Task ReadClassWithStringArray()
        {
            TestClassWithStringArray obj = await Deserializer.DeserializeWrapper<TestClassWithStringArray>(TestClassWithStringArray.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithObjectList()
        {
            TestClassWithObjectList obj = await Deserializer.DeserializeWrapper<TestClassWithObjectList>(TestClassWithObjectList.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithObjectArray()
        {
            TestClassWithObjectArray obj = await Deserializer.DeserializeWrapper<TestClassWithObjectArray>(TestClassWithObjectArray.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithGenericList()
        {
            TestClassWithGenericList obj = await Deserializer.DeserializeWrapper<TestClassWithGenericList>(TestClassWithGenericList.s_data);
            obj.Verify();
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task ReadClassWithObjectIEnumerable()
        {
            TestClassWithObjectIEnumerable obj = await Deserializer.DeserializeWrapper<TestClassWithObjectIEnumerable>(TestClassWithObjectIEnumerable.s_data);
            obj.Verify();
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task ReadClassWithObjectIList()
        {
            TestClassWithObjectIList obj = await Deserializer.DeserializeWrapper<TestClassWithObjectIList>(TestClassWithObjectIList.s_data);
            obj.Verify();
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task ReadClassWithObjectICollection()
        {
            TestClassWithObjectICollection obj = await Deserializer.DeserializeWrapper<TestClassWithObjectICollection>(TestClassWithObjectICollection.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithObjectIEnumerableT()
        {
            TestClassWithObjectIEnumerableT obj = await Deserializer.DeserializeWrapper<TestClassWithObjectIEnumerableT>(TestClassWithObjectIEnumerableT.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithObjectIListT()
        {
            TestClassWithObjectIListT obj = await Deserializer.DeserializeWrapper<TestClassWithObjectIListT>(TestClassWithObjectIListT.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithObjectICollectionT()
        {
            TestClassWithObjectICollectionT obj = await Deserializer.DeserializeWrapper<TestClassWithObjectICollectionT>(TestClassWithObjectICollectionT.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithObjectIReadOnlyCollectionT()
        {
            TestClassWithObjectIReadOnlyCollectionT obj = await Deserializer.DeserializeWrapper<TestClassWithObjectIReadOnlyCollectionT>(TestClassWithObjectIReadOnlyCollectionT.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithObjectIReadOnlyListT()
        {
            TestClassWithObjectIReadOnlyListT obj = await Deserializer.DeserializeWrapper<TestClassWithObjectIReadOnlyListT>(TestClassWithObjectIReadOnlyListT.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithGenericIEnumerable()
        {
            TestClassWithGenericIEnumerable obj = await Deserializer.DeserializeWrapper<TestClassWithGenericIEnumerable>(TestClassWithGenericIEnumerable.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithGenericIList()
        {
            TestClassWithGenericIList obj = await Deserializer.DeserializeWrapper<TestClassWithGenericIList>(TestClassWithGenericIList.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithGenericICollection()
        {
            TestClassWithGenericICollection obj = await Deserializer.DeserializeWrapper<TestClassWithGenericICollection>(TestClassWithGenericICollection.s_data);
        }

        [Fact]
        public async Task ReadClassWithObjectISetT()
        {
            TestClassWithObjectISetT obj = await Deserializer.DeserializeWrapper<TestClassWithObjectISetT>(TestClassWithObjectISetT.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithGenericIEnumerableT()
        {
            TestClassWithGenericIEnumerableT obj = await Deserializer.DeserializeWrapper<TestClassWithGenericIEnumerableT>(TestClassWithGenericIEnumerableT.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithGenericIListT()
        {
            TestClassWithGenericIListT obj = await Deserializer.DeserializeWrapper<TestClassWithGenericIListT>(TestClassWithGenericIListT.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithGenericICollectionT()
        {
            TestClassWithGenericICollectionT obj = await Deserializer.DeserializeWrapper<TestClassWithGenericICollectionT>(TestClassWithGenericICollectionT.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithGenericIReadOnlyCollectionT()
        {
            TestClassWithGenericIReadOnlyCollectionT obj = await Deserializer.DeserializeWrapper<TestClassWithGenericIReadOnlyCollectionT>(TestClassWithGenericIReadOnlyCollectionT.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithGenericIReadOnlyListT()
        {
            TestClassWithGenericIReadOnlyListT obj = await Deserializer.DeserializeWrapper<TestClassWithGenericIReadOnlyListT>(TestClassWithGenericIReadOnlyListT.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithGenericISetT()
        {
            TestClassWithGenericISetT obj = await Deserializer.DeserializeWrapper<TestClassWithGenericISetT>(TestClassWithGenericISetT.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithObjectIEnumerableConstructibleTypes()
        {
            TestClassWithObjectIEnumerableConstructibleTypes obj = await Deserializer.DeserializeWrapper<TestClassWithObjectIEnumerableConstructibleTypes>(TestClassWithObjectIEnumerableConstructibleTypes.s_data);
            obj.Verify();
        }

        [Fact]
        public async Task ReadClassWithObjectImmutableTypes()
        {
            TestClassWithObjectImmutableTypes obj = await Deserializer.DeserializeWrapper<TestClassWithObjectImmutableTypes>(TestClassWithObjectImmutableTypes.s_data);
            obj.Verify();
        }

        public class ClassWithPopulatedListAndNoSetter
        {
            public List<int> MyList { get; } = new List<int>() { 1 };
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task ClassWithNoSetter()
        {
            // We replace the contents of this collection; we don't attempt to add items to the existing collection instance.
            string json = @"{""MyList"":[1,2]}";
            ClassWithPopulatedListAndNoSetter obj = await Deserializer.DeserializeWrapper<ClassWithPopulatedListAndNoSetter>(json);
            Assert.Equal(1, obj.MyList.Count);
        }

        public class ClassWithPopulatedListAndSetter
        {
            public List<int> MyList { get; set; } = new List<int>() { 1 };
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task ClassWithPopulatedList()
        {
            // We replace the contents of this collection; we don't attempt to add items to the existing collection instance.
            string json = @"{""MyList"":[2,3]}";
            ClassWithPopulatedListAndSetter obj = await Deserializer.DeserializeWrapper<ClassWithPopulatedListAndSetter>(json);
            Assert.Equal(2, obj.MyList.Count);
        }

        public class ClassWithMixedSetters
        {
            public List<int> SkippedChild1 { get; }
            public List<int> ParsedChild1 { get; set; }
            public IEnumerable<int> SkippedChild2 { get; }
            public IEnumerable<int> ParsedChild2 { get; set; }
            [JsonIgnore] public IEnumerable<int> SkippedChild3 { get; set; } // Note this has a setter.
            public IEnumerable<int> ParsedChild3 { get; set; }
        }

        [Theory]
        [InlineData(@"{
                ""SkippedChild1"": {},
                ""ParsedChild1"": [1],
                ""UnmatchedProp"": null,
                ""SkippedChild2"": [{""DrainProp1"":{}, ""DrainProp2"":{""SubProp"":0}}],
                ""SkippedChild2"": {},
                ""ParsedChild2"": [2,2],
                ""SkippedChild3"": {},
                ""ParsedChild3"": [3,3]}")]
        [InlineData(@"{
                ""SkippedChild1"": null,
                ""ParsedChild1"": [1],
                ""UnmatchedProp"": null,
                ""SkippedChild2"": [],
                ""SkippedChild2"": null,
                ""ParsedChild2"": [2,2],
                ""SkippedChild3"": null,
                ""ParsedChild3"": [3,3]}")]

#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task ClassWithMixedSettersIsParsed(string json)
        {
            ClassWithMixedSetters parsedObject = await Deserializer.DeserializeWrapper<ClassWithMixedSetters>(json);

            Assert.Null(parsedObject.SkippedChild1);

            Assert.NotNull(parsedObject.ParsedChild1);
            Assert.Equal(1, parsedObject.ParsedChild1.Count);
            Assert.Equal(1, parsedObject.ParsedChild1[0]);

            Assert.Null(parsedObject.SkippedChild2);

            Assert.NotNull(parsedObject.ParsedChild2);
            Assert.True(parsedObject.ParsedChild2.SequenceEqual(new int[] { 2, 2 }));

            Assert.NotNull(parsedObject.ParsedChild3);
            Assert.True(parsedObject.ParsedChild3.SequenceEqual(new int[] { 3, 3 }));
        }

        public class ClassWithNonNullEnumerableGetters
        {
            private string[] _array = null;
            private List<string> _list = null;
            private StringListWrapper _listWrapper = null;
            // Immutable array is a struct.
            private ImmutableArray<string> _immutableArray = default;
            private ImmutableList<string> _immutableList = null;

            public string[] Array
            {
                get => _array ?? new string[] { "-1" };
                set { _array = value; }
            }

            public List<string> List
            {
                get => _list ?? new List<string> { "-1" };
                set { _list = value; }
            }

            public StringListWrapper ListWrapper
            {
                get => _listWrapper ?? new StringListWrapper { "-1" };
                set { _listWrapper = value; }
            }

            public ImmutableArray<string> MyImmutableArray
            {
                get => _immutableArray.IsDefault ? ImmutableArray.CreateRange(new List<string> { "-1" }) : _immutableArray;
                set { _immutableArray = value; }
            }

            public ImmutableList<string> MyImmutableList
            {
                get => _immutableList ?? ImmutableList.CreateRange(new List<string> { "-1" });
                set { _immutableList = value; }
            }

            internal object GetRawArray => _array;
            internal object GetRawList => _list;
            internal object GetRawListWrapper => _listWrapper;
            internal object GetRawImmutableArray => _immutableArray;
            internal object GetRawImmutableList => _immutableList;
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task ClassWithNonNullEnumerableGettersIsParsed()
        {
            async Task TestRoundTrip(ClassWithNonNullEnumerableGetters obj)
            {
                ClassWithNonNullEnumerableGetters roundtrip = await Deserializer.DeserializeWrapper<ClassWithNonNullEnumerableGetters>(JsonSerializer.Serialize(obj));

                if (obj.Array != null)
                {
                    Assert.Equal(obj.Array.Length, roundtrip.Array.Length);
                    Assert.Equal(obj.List.Count, roundtrip.List.Count);
                    Assert.Equal(obj.ListWrapper.Count, roundtrip.ListWrapper.Count);
                    Assert.Equal(obj.MyImmutableArray.Length, roundtrip.MyImmutableArray.Length);
                    Assert.Equal(obj.MyImmutableList.Count, roundtrip.MyImmutableList.Count);

                    if (obj.Array.Length > 0)
                    {
                        Assert.Equal(obj.Array[0], roundtrip.Array[0]);
                        Assert.Equal(obj.List[0], roundtrip.List[0]);
                        Assert.Equal(obj.ListWrapper[0], roundtrip.ListWrapper[0]);
                        Assert.Equal(obj.MyImmutableArray[0], roundtrip.MyImmutableArray[0]);
                        Assert.Equal(obj.MyImmutableList[0], roundtrip.MyImmutableList[0]);
                    }
                }
                else
                {
                    Assert.Null(obj.GetRawArray);
                    Assert.Null(obj.GetRawList);
                    Assert.Null(obj.GetRawListWrapper);
                    Assert.Null(obj.GetRawImmutableList);
                    Assert.Null(roundtrip.GetRawArray);
                    Assert.Null(roundtrip.GetRawList);
                    Assert.Null(roundtrip.GetRawListWrapper);
                    Assert.Null(roundtrip.GetRawImmutableList);
                    Assert.Equal(obj.GetRawImmutableArray, roundtrip.GetRawImmutableArray);
                }
            }

            const string inputJsonWithCollectionElements =
                @"{
                    ""Array"":[""1""],
                    ""List"":[""2""],
                    ""ListWrapper"":[""3""],
                    ""MyImmutableArray"":[""4""],
                    ""MyImmutableList"":[""5""]
                }";

            ClassWithNonNullEnumerableGetters obj = await Deserializer.DeserializeWrapper<ClassWithNonNullEnumerableGetters>(inputJsonWithCollectionElements);
            Assert.Equal(1, obj.Array.Length);
            Assert.Equal("1", obj.Array[0]);

            Assert.Equal(1, obj.List.Count);
            Assert.Equal("2", obj.List[0]);

            Assert.Equal(1, obj.ListWrapper.Count);
            Assert.Equal("3", obj.ListWrapper[0]);

            Assert.Equal(1, obj.MyImmutableArray.Length);
            Assert.Equal("4", obj.MyImmutableArray[0]);

            Assert.Equal(1, obj.MyImmutableList.Count);
            Assert.Equal("5", obj.MyImmutableList[0]);

            await TestRoundTrip(obj);

            const string inputJsonWithoutCollectionElements =
                @"{
                    ""Array"":[],
                    ""List"":[],
                    ""ListWrapper"":[],
                    ""MyImmutableArray"":[],
                    ""MyImmutableList"":[]
                }";

            obj = await Deserializer.DeserializeWrapper<ClassWithNonNullEnumerableGetters>(inputJsonWithoutCollectionElements);
            Assert.Equal(0, obj.Array.Length);
            Assert.Equal(0, obj.List.Count);
            Assert.Equal(0, obj.ListWrapper.Count);
            Assert.Equal(0, obj.MyImmutableArray.Length);
            Assert.Equal(0, obj.MyImmutableList.Count);
            await TestRoundTrip(obj);

            string inputJsonWithNullCollections =
                @"{
                    ""Array"":null,
                    ""List"":null,
                    ""ListWrapper"":null,
                    ""MyImmutableList"":null
                }";

            obj = await Deserializer.DeserializeWrapper<ClassWithNonNullEnumerableGetters>(inputJsonWithNullCollections);
            await TestRoundTrip(obj);

            // ImmutableArray<T> is a struct and cannot be null.
            inputJsonWithNullCollections = @"{""MyImmutableArray"":null}";
            await Assert.ThrowsAsync<JsonException>(() => Deserializer.DeserializeWrapper<ClassWithNonNullEnumerableGetters>(inputJsonWithNullCollections));
        }

        [Fact]
#if GENERATE_JSON_METADATA
        [ActiveIssue("https://github.com/dotnet/runtimelab/projects/1#card-48716081")]
#endif
        public async Task DoNotDependOnPropertyGetterWhenDeserializingCollections()
        {
            Dealer dealer = new Dealer { NetworkCodeList = new List<string> { "Network1", "Network2" } };

            string serialized = JsonSerializer.Serialize(dealer);
            Assert.Equal(@"{""NetworkCodeList"":[""Network1"",""Network2""]}", serialized);

            dealer = await Deserializer.DeserializeWrapper<Dealer>(serialized);

            List<string> expected = new List<string> { "Network1", "Network2" };
            int i = 0;

            foreach (string str in dealer.NetworkCodeList)
            {
                Assert.Equal(expected[i], str);
                i++;
            }

            Assert.Equal("Network1,Network2", dealer.Networks);
        }

        class Dealer
        {
            private string _networks;

            [JsonIgnore]
            public string Networks
            {
                get => _networks;
                set => _networks = value ?? string.Empty;
            }

            public IEnumerable<string> NetworkCodeList
            {
                get => !string.IsNullOrEmpty(Networks) ? Networks?.Split(',') : new string[0];
                set => Networks = (value != null) ? string.Join(",", value) : string.Empty;
            }
        }
    }
}
