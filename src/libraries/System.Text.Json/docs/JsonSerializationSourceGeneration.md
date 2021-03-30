# Compile-time source generation for System.Text.Json

## Background

The “JSON Serializer recommendations for 5.0” [document](https://github.com/dotnet/designs/blob/ab7ea05a8831177f95cae8eb015ed73e105c13b5/accepted/2020/serializer/SerializerGoals5.0.md) details the benefits of generating additional source code specific to serializable .NET types. These include reach, faster cold startup performance, reducing memory usage, improved runtime throughput, and smaller size-on-disk of consuming applications. After evaluating various approaches, we decided to go implement a source generator to aid JSON serialization using [Roslyn C# source generator](https://devblogs.microsoft.com/dotnet/introducing-c-source-generators/) infrasture. This document will outline the roadmap, design considerations, and implementation details for this endeavor.

## Goals

### Faster start up and run-time performance ([“Developers apps using JSON serialization start up and run faster”](https://github.com/dotnet/runtime/issues/1568) user story for .NET 6.0)

- Reduced start-up time
- Reduced private bytes usage
- Improved run-time throughput* 

*Little improvement targeted here for .NET 6.0. No noticeable regression expected; can achieve this with knobs in future.

### Reduced application size and ILLinker warnings ([“Developers can safely trim their apps which use System.Text.Json to reduce the size of their apps”](https://github.com/dotnet/runtime/issues/45441) user story for .NET 6.0)

- Reduced application size when System.Text.Json is used (post ILLinker trimming)
- Reduced ILLinker warnings due to avoiding runtime reflection (reflection based code-paths trimmed)

## Overview

The System.Text.Json source generator is a Roslyn source generator that generates serialization metadata for JSON serializable types in a project. These serializable types are indicated to the source generator via a new `[JsonSerializable]` attribute. The generator then proceeds to generate metadata for each type in the object graphs of each type indicated to the serializer. The metadata generated for a type contains structured information in a format that can be optimally utilized by the serializer to serialize and deserialize instances of that type to and from JSON representations.

Given a POCO:

```cs
public class MyClass
{
    public int MyInt { get; set; }
    public string MyString { get; set; }
}
```

With the existing `JsonSerializer` functionality, this POCO may be serialized and deserialized as follows:

```cs
MyClass obj = new()
{
    MyInt = 1,
    MyString = "Hello",
};

byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(obj);
obj = JsonSerializer.Deserialize<MyClass>(serialized);

Console.WriteLine(obj.MyInt); // 1
Console.WriteLine(obj.MyString); // “Hello”
```

With source generation, the serializable type may be indicated to the generator via `JsonSerializableAttribute`:

```cs
[assembly: JsonSerializable(typeof(MyClass))]
```

<details>

<summary>
The generator will then generate structured type metadata to the compilation assembly (click to view).
</summary>

`JsonContext.g.cs`

```cs
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConsoleAfter.JsonSourceGeneration
{
    internal partial sealed class JsonContext : JsonSerializerContext
    {
        private static JsonContext s_default;
        public static JsonContext Default
        {
            get
            {
                s_default ??= new JsonContext();
                return s_default;
            }
        }

        private JsonContext()
        {
        }

        public JsonContext(JsonSerializerOptions options) : base(options)
        {   
        }
    }
}
```

`MyClass.g.cs`

```cs
using ConsoleAfter;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Converters;
using System.Text.Json.Serialization.Metadata;

namespace ConsoleAfter.JsonSourceGeneration
{
    internal partial sealed class JsonContext : JsonSerializerContext
    {
        private JsonObjectInfo<ConsoleAfter.MyClass> _MyClass;
        public JsonTypeInfo<ConsoleAfter.MyClass> MyClass
        {
            get
            {
                if (_MyClass == null)
                {
                    _MyClass = new(createObjectFunc: static () => new ConsoleAfter.MyClass(), numberHandling: null, this.GetOptions());

                    _MyClass.AddProperty(
                        clrPropertyName: "MyInt",
                        memberType: System.Reflection.MemberTypes.Property,
                        declaringType: typeof(ConsoleAfter.MyClass),
                        classInfo: this.Int32,
                        getter: static (obj) => { return ((ConsoleAfter.MyClass)obj).MyInt; },
                        setter: static (obj, value) => { ((ConsoleAfter.MyClass)obj).MyInt = value; },
                        jsonPropertyName: null,
                        ignoreCondition: null,
                        numberHandling: null);
                
                    _MyClass.AddProperty(
                        clrPropertyName: "MyString",
                        memberType: System.Reflection.MemberTypes.Property,
                        declaringType: typeof(ConsoleAfter.MyClass),
                        classInfo: this.String,
                        getter: static (obj) => { return ((ConsoleAfter.MyClass)obj).MyString; },
                        setter: static (obj, value) => { ((ConsoleAfter.MyClass)obj).MyString = value; },
                        jsonPropertyName: null,
                        ignoreCondition: null,
                        numberHandling: null);
                
                    _MyClass.CompleteInitialization(canBeDynamic: false);
                }

                return _MyClass;
            }
        }
    }
}
```

`Int32.g.cs`

```cs
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Converters;
using System.Text.Json.Serialization.Metadata;

namespace ConsoleAfter.JsonSourceGeneration
{
    internal partial class JsonContext : JsonSerializerContext
    {
        private JsonValueInfo<System.Int32> _Int32;
        public JsonTypeInfo<System.Int32> Int32
        {
            get
            {
                if (_Int32 == null)
                {
                    _Int32 = new JsonValueInfo<System.Int32>(new Int32Converter(), numberHandling: null, GetOptions());
                }

                return _Int32;
            }
        }
    }
}
```

`String.g.cs`

```cs
using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Converters;
using System.Text.Json.Serialization.Metadata;

namespace ConsoleAfter.JsonSourceGeneration
{
    internal partial class JsonContext : JsonSerializerContext
    {
        private JsonValueInfo<System.String> _String;
        public JsonTypeInfo<System.String> String
        {
            get
            {
                if (_String == null)
                {
                    _String = new JsonValueInfo<System.String>(new StringConverter(), numberHandling: null, GetOptions());
                }

                return _String;
            }
        }
    }
}
```
</details>

<br>

The generated type metadata can then be passed to new (de)serialization overloads as follows:

```cs
MyClass obj = new()
{
    MyInt = 1,
    MyString = "Hello",
};

byte[] serialized = JsonSerializer.SerializeToUtf8Bytes(obj, JsonContext.Default.MyClass);
obj = JsonSerializer.Deserialize<MyClass>(serialized, JsonContext.Default.MyClass);

Console.WriteLine(obj.MyInt); // 1
Console.WriteLine(obj.MyString); // “Hello”
```

## Type discovery

TODO.

Type discovery refers to how the source generator. For v1 we provide an explicit approach where each root serializable type is indicated to the generator via `System.Text.Json.Serialization.JsonSerializableAttribute`. This model is safe and ensures that we do not skip any types, or include unwanted types. In the future we can scan for `T`s and `Type` instances passed to the various serialization overloads.

## Generated metadata

TODO: deep dive into different types of generated metadata.


There are three major classes of types, corresponding to three major generated-metadata representations: primitives, POCOs (types that map to JSON object representations), and collections.

## How generating metadata helps meet performance goals

## Performance

TODO. Summarize perf characteristics

Theoretical:

Real-world:

## Size

Console app:

Blazor: JSON generation contributes to ~121 KB compressed DLL size reduction in default Blazor app.

## API Proposal

<details>
<summary>(click to view)</summary>

`System.Text.Json.SourceGeneration.dll`

The following API will be generated into the compiling assembly, to mark serializable types.

```cs
namespace System.Text.Json.SourceGeneration
{
    /// <summary>
    /// Instructs the System.Text.Json source generator to generate serialization metadata for a specified type at compile time.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class JsonSerializableAttribute : Attribute
    {
        /// <summary>
        /// Indicates whether the specified type might be the runtime type of an object instance which was declared as
        /// a different type (polymorphic serialization).
        /// </summary>
        public bool CanBeDynamic { get; set; }
        /// <summary>
        /// Initializes a new instance of <see cref=""JsonSerializableAttribute""/> with the specified type.
        /// </summary>
        /// <param name=""type"">The Type of the property.</param>
        public JsonSerializableAttribute(Type type) { }
    }
}
```

`System.Text.Json.dll`

```cs
namespace System.Text.Json
{
    public static partial class JsonSerializer
    {
        public static object? Deserialize(string json, Type type, JsonSerializerContext jsonSerializerContext) { throw null; }
        public static object? Deserialize(ref Utf8JsonReader reader, Type returnType, JsonSerializerContext jsonSerializerContext) { throw null; }
        public static ValueTask<object?> DeserializeAsync(Stream utf8Json, Type returnType, JsonSerializerContext jsonSerializerContext, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static ValueTask<TValue?> DeserializeAsync<TValue>(Stream utf8Json, JsonSerializerContext jsonSerializerContext, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static ValueTask<TValue?> DeserializeAsync<TValue>(Stream utf8Json, JsonTypeInfo<TValue> jsonTypeInfo, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static TValue? Deserialize<TValue>(System.ReadOnlySpan<byte> utf8Json, JsonTypeInfo<TValue> jsonTypeInfo) { throw null; }
        public static TValue? Deserialize<TValue>(string json, JsonSerializerContext jsonSerializerContext) { throw null; }
        public static TValue? Deserialize<TValue>(string json, JsonTypeInfo<TValue> jsonTypeInfo) { throw null; }
        public static TValue? Deserialize<TValue>(ref Utf8JsonReader reader, JsonTypeInfo<TValue> jsonTypeInfo) { throw null; }
        public static string Serialize(object? value, Type inputType, JsonSerializerContext jsonSerializerContext) { throw null; }
        public static byte[] SerializeToUtf8Bytes<TValue>(TValue value, JsonTypeInfo<TValue> jsonTypeInfo) { throw null; }
        public static string Serialize<TValue>(TValue value, JsonSerializerContext jsonSerializerContext) { throw null; }
        public static string Serialize<TValue>(TValue value, JsonTypeInfo<TValue> jsonTypeInfo) { throw null; }
    }

    public sealed partial class JsonSerializerOptions
    {
        public static JsonSerializerOptions CreateForSizeOpts(JsonSerializerDefaults defaults = default) { throw null; }
    }
}

namespace System.Text.Json.Serialization
{
    public partial class JsonSerializerContext : System.IDisposable
    {
        public JsonSerializerContext() { }
        public JsonSerializerContext(JsonSerializerOptions options) { }
        public void Dispose() { }
        protected virtual void Dispose(bool disposing) { }
        public virtual JsonClassInfo? GetJsonClassInfo(Type type) { throw null; }
        public JsonSerializerOptions GetOptions() { throw null; }
    }
}

namespace System.Text.Json.Serialization.Converters
{
    public sealed class BooleanConverter : JsonConverter<bool>
    {
        public BooleanConverter() { }
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) {  }
    }
    public sealed class ByteArrayConverter : JsonConverter<byte[]>
    {
        public ByteArrayConverter() { }
        public override byte[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options) { }
    }
    public sealed class ByteConverter : JsonConverter<byte>
    {
        public ByteConverter() { }
        public override byte Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, byte value, JsonSerializerOptions options) { }
    }
    public sealed class CharConverter : JsonConverter<char>
    {
        public CharConverter() { }
        public override char Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, char value, JsonSerializerOptions options) { }
    }
    public sealed class DateTimeConverter : JsonConverter<System.DateTime>
    {
        public DateTimeConverter() { }
        public override System.DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, System.DateTime value, JsonSerializerOptions options) { }
    }
    public sealed class DateTimeOffsetConverter : JsonConverter<System.DateTimeOffset>
    {
        public DateTimeOffsetConverter() { }
        public override System.DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, System.DateTimeOffset value, JsonSerializerOptions options) { }
    }
    public sealed class DecimalConverter : JsonConverter<decimal>
    {
        public DecimalConverter() { }
        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) { }
    }
    public sealed class DoubleConverter : JsonConverter<double>
    {
        public DoubleConverter() { }
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) { }
    }
    public sealed class EnumConverter<T> : JsonConverter<T> where T : struct, Enum
    {
        public EnumConverter(JsonSerializerOptions serializerOptions) { }
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) { }
    }
    public sealed class GuidConverter : JsonConverter<System.Guid>
    {
        public GuidConverter() { }
        public override System.Guid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, System.Guid value, JsonSerializerOptions options) { }
    }
    public sealed class Int16Converter : JsonConverter<short>
    {
        public Int16Converter() { }
        public override short Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, short value, JsonSerializerOptions options) { }
    }
    public sealed class Int32Converter : JsonConverter<int>
    {
        public Int32Converter() { }
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) { }
    }
    public sealed class Int64Converter : JsonConverter<long>
    {
        public Int64Converter() { }
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) { }
    }
    public sealed class NullableConverter<T> : JsonConverter<T?> where T : struct
    {
        public NullableConverter(JsonConverter<T> converter) { }
        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options) { }
    }
    public sealed class ObjectConverter : JsonConverter<object>
    {
        public ObjectConverter() { }
        public override object Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) { }
    }
    public sealed class SingleConverter : JsonConverter<float>
    {
        public SingleConverter() { }
        public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options) { }
    }
    [System.CLSCompliant(false)]
    public sealed class SByteConverter : JsonConverter<sbyte>
    {
        public SByteConverter() { }
        public override sbyte Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, sbyte value, JsonSerializerOptions options) { }
    }
    public sealed class StringConverter : JsonConverter<string>
    {
        public StringConverter() { }
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) { }
    }
    [System.CLSCompliant(false)]
    public sealed class UInt16Converter : JsonConverter<ushort>
    {
        public UInt16Converter() { }
        public override ushort Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, ushort value, JsonSerializerOptions options) { }
    }
    [System.CLSCompliant(false)]
    public sealed class UInt32Converter : JsonConverter<uint>
    {
        public UInt32Converter() { }
        public override uint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, uint value, JsonSerializerOptions options) { }
    }
    [System.CLSCompliant(false)]
    public sealed class UInt64Converter : JsonConverter<ulong>
    {
        public UInt64Converter() { }
        public override ulong Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, ulong value, JsonSerializerOptions options) { }
    }
    public sealed class UriConverter : JsonConverter<Uri>
    {
        public UriConverter() { }
        public override Uri Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, Uri value, JsonSerializerOptions options) { }
    }
    public sealed class VersionConverter : JsonConverter<Version>
    {
        public VersionConverter() { }
        public override Version Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) { throw null; }
        public override void Write(Utf8JsonWriter writer, Version value, JsonSerializerOptions options) { }
    }
}

namespace System.Text.Json.Serialization.Metadata
{
    public partial class JsonClassInfo
    {
        internal JsonClassInfo() { }
        public JsonNumberHandling? NumberHandling { get { throw null; } set { } }
        public JsonConverter ConverterBase { get { throw null; } }
        public JsonClassInfo.ConstructorDelegate? CreateObject { get { throw null; } set { } }
        public JsonSerializerOptions Options { get { throw null; } }
        public Type Type { get { throw null; } }
        public delegate object? ConstructorDelegate();
    }
    public sealed partial class JsonObjectInfo<T> : JsonTypeInfo<T>
    {
        public JsonObjectInfo(JsonClassInfo.ConstructorDelegate? createObjectFunc, JsonSerializerOptions options) { }
        public void AddProperty(JsonPropertyInfo jsonPropertyInfo) { throw null; }
        public void CompleteInitialization() { }
    }
    public abstract partial class JsonPropertyInfo
    {
        internal JsonPropertyInfo() { }
        public byte[] EscapedNameSection { get { throw null; } set { } }
        public byte[] NameAsUtf8Bytes { get { throw null; } set { } }
        public abstract JsonConverter ConverterBase { get; set; }
        public Type DeclaredPropertyType { get { throw null; } set { } }
        public Type DeclaringType { get { throw null; } set { } }
        public string NameAsString { get { throw null; } set { } }
        public bool ShouldDeserialize { get { throw null; } }
        public bool ShouldSerialize { get { throw null; } }
        public System.Text.Json.Serialization.JsonIgnoreCondition? IgnoreCondition { get { throw null; } set { } }
        public JsonNumberHandling? NumberHandling { get { throw null; } set { } }
        public System.Reflection.MemberTypes MemberType { get { throw null; } set { } }
        public JsonSerializerOptions Options { get { throw null; } set { } }
        public JsonClassInfo RuntimeClassInfo { get { throw null; } set { } }
    }
    public sealed partial class JsonPropertyInfo<T> : JsonPropertyInfo
    {
        public JsonConverter<T> Converter { get { throw null; } }
        public override JsonConverter ConverterBase { get { throw null; } set { } }
        public Func<object, T>? Get { get { throw null; } set { } }
        public Action<object, T>? Set { get { throw null; } set { } }
        public void CompleteInitialization() { }
        public static JsonPropertyInfo<T> Create() { throw null; }
    }
    public abstract partial class JsonTypeInfo<T> : JsonClassInfo
    {
        internal JsonTypeInfo() { }
        public void RegisterToOptions() { }
    }
    public sealed partial class JsonValueInfo<T> : JsonTypeInfo<T>
    {
        public JsonValueInfo(JsonConverter converter, JsonSerializerOptions options) { }
    }
    public sealed partial class JsonCollectionTypeInfo<T> : JsonTypeInfo<T>
    {
        public JsonCollectionTypeInfo(JsonClassInfo.ConstructorDelegate createObjectFunc, JsonConverter<T> converter, JsonClassInfo elementClassInfo, JsonSerializerOptions options) { }
        public JsonCollectionTypeInfo(JsonClassInfo.ConstructorDelegate createObjectFunc, JsonConverter<T> converter, JsonClassInfo elementClassInfo, JsonSerializerOptions options) { }
    }
    public static partial class KnownCollectionTypeInfos<T>
    {
        public static JsonCollectionTypeInfo<T[]> GetArray(JsonClassInfo elementClassInfo, JsonSerializerContext context) { throw null; }
        public static JsonCollectionTypeInfo<IEnumerable<T>> GetIEnumerable(JsonClassInfo elementClassInfo, JsonSerializerContext context) { throw null; }
        public static JsonCollectionTypeInfo<IList<T>> GetIList(JsonClassInfo elementClassInfo, JsonSerializerContext context) { throw null; }
        public static JsonCollectionTypeInfo<List<T>> GetList(JsonClassInfo elementClassInfo, JsonSerializerContext context) { throw null; }
    }
    public static partial class KnownDictionaryTypeInfos<TKey, TValue> where TKey : notnull
    {
        public static JsonCollectionTypeInfo<Dictionary<TKey, TValue>> GetDictionary(JsonClassInfo keyClassInfo, JsonClassInfo valueClassInfo, JsonSerializerContext context) { throw null; }
    }
}
```

`System.Net.Http.Json.dll`

```cs
namespace System.Net.Http.Json
{
    public static partial class HttpClientJsonExtensions
    {
        public static Task<object?> GetFromJsonAsync(this HttpClient client, string? requestUri, Type type, JsonSerializerOptions? options, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static Task<object?> GetFromJsonAsync(this HttpClient client, string? requestUri, Type type, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static Task<object?> GetFromJsonAsync(this HttpClient client, Uri? requestUri, Type type, JsonSerializerOptions? options, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static Task<object?> GetFromJsonAsync(this HttpClient client, Uri? requestUri, Type type, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static Task<TValue?> GetFromJsonAsync<TValue>(this HttpClient client, string? requestUri, JsonSerializerContext? context, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static Task<TValue?> GetFromJsonAsync<TValue>(this HttpClient client, string? requestUri, JsonSerializerOptions? options, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static Task<TValue?> GetFromJsonAsync<TValue>(this HttpClient client, string? requestUri, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static Task<TValue?> GetFromJsonAsync<TValue>(this HttpClient client, Uri? requestUri, JsonSerializerOptions? options, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static Task<TValue?> GetFromJsonAsync<TValue>(this HttpClient client, Uri? requestUri, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static Task<HttpResponseMessage> PostAsJsonAsync<TValue>(this HttpClient client, string? requestUri, TValue value, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static Task<HttpResponseMessage> PostAsJsonAsync<TValue>(this HttpClient client, string? requestUri, TValue value, CancellationToken cancellationToken) { throw null; }
        public static Task<HttpResponseMessage> PostAsJsonAsync<TValue>(this HttpClient client, Uri? requestUri, TValue value, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static Task<HttpResponseMessage> PostAsJsonAsync<TValue>(this HttpClient client, Uri? requestUri, TValue value, CancellationToken cancellationToken) { throw null; }
        public static Task<HttpResponseMessage> PutAsJsonAsync<TValue>(this HttpClient client, string? requestUri, TValue value, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static Task<HttpResponseMessage> PutAsJsonAsync<TValue>(this HttpClient client, string? requestUri, TValue value, CancellationToken cancellationToken) { throw null; }
        public static Task<HttpResponseMessage> PutAsJsonAsync<TValue>(this HttpClient client, Uri? requestUri, TValue value, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static Task<HttpResponseMessage> PutAsJsonAsync<TValue>(this HttpClient client, Uri? requestUri, TValue value, CancellationToken cancellationToken) { throw null; }
    }

    public static partial class HttpContentJsonExtensions
    {
        public static Task<object?> ReadFromJsonAsync(this HttpContent content, Type type, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }

        public static Task<object?> ReadFromJsonAsync(this HttpContent content, JsonClassInfo jsonTypeInfo, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
        public static Task<object?> ReadFromJsonAsync(this HttpContent content, JsonSerializerContext, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }


        public static Task<T?> ReadFromJsonAsync<T>(this HttpContent content, JsonSerializerOptions? options = null, CancellationToken cancellationToken = default(CancellationToken)) { throw null; }
    }
}
```
</details>

## Versioning

TODO. How to ensure we don't invoke stale/buggy metadata implementations?

Integer property or generated JsonContext class(es) which we can check at runtime.

## Compatibility with existing `JsonSerializer` functionality

TODO. Assert that all functionality that exists today will continue to in source-gen mode. Discuss how new features need to be honored by generator (not only implemented by serializer internals) moving foward.