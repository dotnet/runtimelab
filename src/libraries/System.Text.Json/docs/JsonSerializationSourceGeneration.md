# Compile-time source generation for `System.Text.Json`

The “JSON Serializer recommendations for 5.0” [document](https://github.com/dotnet/designs/blob/ab7ea05a8831177f95cae8eb015ed73e105c13b5/accepted/2020/serializer/SerializerGoals5.0.md) goes over benefits of generating additional source specific to serializable .NET types at compile time. These include faster startup performance, reduced memory usage, improved runtime throughput, and smaller size-on-disk of applications that use `System.Text.Json`. This document will discuss design considerations and technical details for a C# source generator that helps provide these benefits.

## Scenarios
- [“Developers apps using JSON serialization start up and run faster”](https://github.com/dotnet/runtime/issues/1568)
- [“Developers can safely trim their apps which use `System.Text.Json` to reduce the size of their apps”](https://github.com/dotnet/runtime/issues/45441)

## Goals

- Reduce start-up time for apps that use System.Text.Json
- Reduce private bytes usage for apps that use System.Text.Json
- Improve JSON (de)serialization throughput in apps that use System.Text.Json*
- Reduce size of applications that use `System.Text.Json` (post ILLinker trimming)
- Eradicate ILLinker warnings caused by apps using System.Text.Json

\* Current implementation does not provide throughput improvement. A good way to achieve this is to generate (de)serialization logic directly e.g. code that directly uses the low level `Utf8JsonReader` and `Utf8JsonWriter`. The hamper to achieving this are complex serializer usages that would require a large and complex amount of code for each type. The primary approach/mode is to instead pre-generate serialization metadata specific to types at compile time, and rely on the serializers existing code to perform the (de)serialization (hence no change in throughput). A lightweight source generation mode to optimize serialization throughput for simple POCOs and simple serializer options (e.g. TechEmpower benchmarks) is in scope for .NET 6.0. We can achieve the same throughput improvements for more serializer usage scenarios using optional knobs in the future.

## Overview

A source generator is a .NET Standard 2.0 assembly that is loaded by the compiler. Source generators allow developers to generate C# source files that can be added to an assembly during the course of compilation. The `System.Text.Json` source generator (`System.Text.Json.SourceGeneration.dll`) generates serialization metadata for JSON-serializable types in a project. The metadata generated for a type contains structured information in a format that can be optimally utilized by the serializer to serialize and deserialize instances of that type to and from JSON representations. Serializable types are indicated to the source generator via a new `[JsonSerializable]` attribute. The generator then generates metadata for each type in the object graphs of each indicated type.

In previous versions of `System.Text.Json`, serialization metadata was computed at runtime, during the first serialization or deserialization routine of every type in any object graph passed to the serializer. At a high level, this metadata includes delegates to constructors, properter setters and getters, along with user options indicated at both runtime and design (e.g. whether to ignore a property value when serializing and it is `null`). After this metadata is generated, the serializer performs the actual serialization and deserialization. The generation phase is based on reflection, and is computationally expensive both in terms of throughput and allocations. We can refer to this phase as the serializer's "warm-up" phase. With this design, we are concerned not only with the cost of the intial work done within the serializer, but with the cost of all the work when an application is first started because it uses `System.Text.Json`. We can refer to this work collectively as the start time of the application.

The fundamental approach of the design this document goes over is to shift this runtime metadata generation phase to compile-time, substantially reducing the cost of the first serialization or deserialization procedures. This metadata is generated to the compiling assembly, where it can be initialized and passed directly to `JsonSerializer` so that it doesn't have to generate it at runtime. This helps reduce the costs of the first serialization or deserialization of each type.

The serializer supports a wide range of scenarios and has multiple layers of abstraction and indirection to navigate through when serializing and deserializing. In order to improve throughput for a specific scenario, one may consider generating specific and optimized serialization and deserialization logic. For now, there is no change to the actual serialization and deserialization logic for each type. The existing code-paths of the serializer are still used. This is to support complicated serializer usages like complex object graphs, and also complex serialization options that can be indicated at runtime (using `JsonSerializerOptions`) as well as design-time (using serialization attributes like `[JsonIgnore]`). Attempting the generate code that adapts to each scenario can lead to large, complex, and unreliable code. A lightweight mode to generate low-level serialization logic for simple scenarios like POCOs and TechEmpower benchmarks is not yet implemented in this design, but is in scope for .NET 6.0. In the future, we can add optional knobs to improve throughput for other predetermined scenarios.

This project introduces new patterns for using `JsonSerializer`, with the aim of improving performance in consuming applications. Let us see an example of what compile-time generated metadata looks like and how to interact with it in a project. Given an object:

```cs
public class MyClass
{
    public int MyInt { get; set; }
    public string[] MyStrings { get; set; }
}
```

With the existing `JsonSerializer` functionality, this POCO may be serialized and deserialized as follows:

```cs
MyClass obj = new()
{
    MyInt = 1,
    MyStrings = new string[] { "Hello" },
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

`JsonSerializableAttribute.g.cs`

```cs
using System;

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
        /// Initializes a new instance of <see cref="JsonSerializableAttribute"/> with the specified type.
        /// </summary>
        /// <param name="type">The Type of the property.</param>
        public JsonSerializableAttribute(Type type) { }
    }
}
```

`JsonContext.g.cs`

```cs
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Startup.JsonSourceGeneration
{
    internal partial class JsonContext : JsonSerializerContext
    {
        private static JsonContext s_default;
        public static JsonContext Default => s_default ??= new JsonContext();

        private JsonContext()
        {
        }

        public JsonContext(JsonSerializerOptions options) : base(options)
        {
        }
        
        private static JsonConverter GetRuntimeProvidedCustomConverter(System.Type type, JsonSerializerOptions options)
        {
            System.Collections.Generic.IList<JsonConverter> converters = options.Converters;

            for (int i = 0; i < converters.Count; i++)
            {
                JsonConverter converter = converters[i];

                if (converter.CanConvert(type))
                {
                    if (converter is JsonConverterFactory factory)
                    {
                        converter = factory.CreateConverter(type, options);
                        if (converter == null || converter is JsonConverterFactory)
                        {
                            throw new System.InvalidOperationException($"The converter '{factory.GetType()}' cannot return null or a JsonConverterFactory instance.");
                        }
                    }

                    return converter;
                }
            }

            return null;
        }

        public JsonPropertyInfo<TProperty> CreateProperty<TProperty>(
                string clrPropertyName,
                System.Reflection.MemberTypes memberType,
                System.Type declaringType,
                JsonTypeInfo<TProperty> classInfo,
                JsonConverter converter,
                System.Func<object, TProperty> getter,
                System.Action<object, TProperty> setter,
                string jsonPropertyName,
                byte[] nameAsUtf8Bytes,
                byte[] escapedNameSection,
                JsonIgnoreCondition? ignoreCondition,
                JsonNumberHandling? numberHandling)
        {
            JsonSerializerOptions options = GetOptions();
            JsonPropertyInfo<TProperty> jsonPropertyInfo = JsonPropertyInfo<TProperty>.Create();
            jsonPropertyInfo.Options = options;

            if (nameAsUtf8Bytes != null && options.PropertyNamingPolicy == null)
            {
                jsonPropertyInfo.NameAsString = jsonPropertyName ?? clrPropertyName;
                jsonPropertyInfo.NameAsUtf8Bytes = nameAsUtf8Bytes;
                jsonPropertyInfo.EscapedNameSection = escapedNameSection;
            }
            else
            {
                jsonPropertyInfo.NameAsString = jsonPropertyName
                    ?? options.PropertyNamingPolicy?.ConvertName(clrPropertyName)
                    ?? (options.PropertyNamingPolicy == null
                            ? null
                            : throw new System.InvalidOperationException("TODO: PropertyNamingPolicy cannot return null."));
                // NameAsUtf8Bytes and EscapedNameSection will be set in CompleteInitialization() below.
            }

            if (ignoreCondition != JsonIgnoreCondition.Always)
            {
                jsonPropertyInfo.Get = getter;
                jsonPropertyInfo.Set = setter;
                jsonPropertyInfo.ConverterBase = converter ?? throw new System.NotSupportedException("TODO: need custom converter here?");
                jsonPropertyInfo.RuntimeClassInfo = classInfo;
                jsonPropertyInfo.DeclaredPropertyType = typeof(TProperty);
                jsonPropertyInfo.DeclaringType = declaringType;
                jsonPropertyInfo.IgnoreCondition = ignoreCondition;
                jsonPropertyInfo.NumberHandling = numberHandling;
                jsonPropertyInfo.MemberType = memberType;
            }
            jsonPropertyInfo.CompleteInitialization();
            return jsonPropertyInfo;
        }
    }
}
```

`JsonContext.GetJsonClassInfo.g.cs`

```cs
using Startup;
using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Converters;
using System.Text.Json.Serialization.Metadata;

namespace Startup.JsonSourceGeneration
{
    internal partial class JsonContext : JsonSerializerContext
    {
        public override JsonClassInfo GetJsonClassInfo(System.Type type)
        {
            if (type == typeof(Startup.MyClass))
            {
                return this.MyClass;
            }

            return null!;
        }
    }
}
```

`MyClass.g.cs`

```cs
using Startup;
using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Converters;
using System.Text.Json.Serialization.Metadata;

namespace Startup.JsonSourceGeneration
{
    internal partial class JsonContext : JsonSerializerContext
    {
        private JsonTypeInfo<Startup.MyClass> _MyClass;
        public JsonTypeInfo<Startup.MyClass> MyClass
        {
            get
            {
                if (_MyClass == null)
                {
                    JsonSerializerOptions options = GetOptions();

                    JsonConverter customConverter;
                    if (options.Converters.Count > 0 && (customConverter = GetRuntimeProvidedCustomConverter(typeof(Startup.MyClass), options)) != null)
                    {
                        _MyClass = new JsonValueInfo<Startup.MyClass>(customConverter, options);
                        _MyClass.NumberHandling = null;
                    }
                    else
                    {
                        JsonObjectInfo<Startup.MyClass> objectInfo = new(createObjectFunc: static () => new Startup.MyClass(), this.GetOptions());
                        objectInfo.NumberHandling = null;
                        _MyClass = objectInfo;
    
                        objectInfo.AddProperty(CreateProperty<System.Int32>(
                            clrPropertyName: "MyInt",
                            memberType: System.Reflection.MemberTypes.Property,
                            declaringType: typeof(Startup.MyClass),
                            classInfo: this.Int32,
                            converter: this.Int32.ConverterBase,
                            getter: static (obj) => { return ((Startup.MyClass)obj).MyInt; },
                            setter: static (obj, value) => { ((Startup.MyClass)obj).MyInt = value; },
                            jsonPropertyName: null,
                            nameAsUtf8Bytes: new byte[] {77,121,73,110,116},
                            escapedNameSection: new byte[] {34,77,121,73,110,116,34,58},
                            ignoreCondition: null,
                            numberHandling: null));
                    
                        objectInfo.AddProperty(CreateProperty<System.String[]>(
                            clrPropertyName: "MyStrings",
                            memberType: System.Reflection.MemberTypes.Property,
                            declaringType: typeof(Startup.MyClass),
                            classInfo: this.StringArray,
                            converter: this.StringArray.ConverterBase,
                            getter: static (obj) => { return ((Startup.MyClass)obj).MyStrings; },
                            setter: static (obj, value) => { ((Startup.MyClass)obj).MyStrings = value; },
                            jsonPropertyName: null,
                            nameAsUtf8Bytes: new byte[] {77,121,83,116,114,105,110,103,115},
                            escapedNameSection: new byte[] {34,77,121,83,116,114,105,110,103,115,34,58},
                            ignoreCondition: null,
                            numberHandling: null));
                    
                        objectInfo.CompleteInitialization();
                    }
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
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Converters;
using System.Text.Json.Serialization.Metadata;

namespace Startup.JsonSourceGeneration
{
    internal partial class JsonContext : JsonSerializerContext
    {
        private JsonTypeInfo<System.Int32> _Int32;
        public JsonTypeInfo<System.Int32> Int32
        {
            get
            {
                if (_Int32 == null)
                {
                    JsonSerializerOptions options = GetOptions();

                    JsonConverter customConverter;
                    if (options.Converters.Count > 0 && (customConverter = GetRuntimeProvidedCustomConverter(typeof(System.Int32), options)) != null)
                    {
                        _Int32 = new JsonValueInfo<System.Int32>(customConverter, options);
                        _Int32.NumberHandling = null;
                    }
                    else
                    {
                        _Int32 = new JsonValueInfo<System.Int32>(new Int32Converter(), options);
                        _Int32.NumberHandling = null;
                    }
                }

                return _Int32;
            }
        }
    }
}
```

`StringArray.g.cs`

```cs
using System;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Converters;
using System.Text.Json.Serialization.Metadata;

namespace Startup.JsonSourceGeneration
{
    internal partial class JsonContext : JsonSerializerContext
    {
        private JsonTypeInfo<System.String[]> _StringArray;
        public JsonTypeInfo<System.String[]> StringArray
        {
            get
            {
                if (_StringArray == null)
                {
                    JsonSerializerOptions options = GetOptions();

                    JsonConverter customConverter;
                    if (options.Converters.Count > 0 && (customConverter = GetRuntimeProvidedCustomConverter(typeof(System.String[]), options)) != null)
                    {
                        _StringArray = new JsonValueInfo<System.String[]>(customConverter, options);
                        _StringArray.NumberHandling = null;
                    }
                    else
                    {
                        _StringArray = KnownCollectionTypeInfos<System.String>.GetArray(this.String, this);
                        _StringArray.NumberHandling = null;
                    }
                }

                return _StringArray;
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

namespace Startup.JsonSourceGeneration
{
    internal partial class JsonContext : JsonSerializerContext
    {
        private JsonTypeInfo<System.String> _String;
        public JsonTypeInfo<System.String> String
        {
            get
            {
                if (_String == null)
                {
                    JsonSerializerOptions options = GetOptions();

                    JsonConverter customConverter;
                    if (options.Converters.Count > 0 && (customConverter = GetRuntimeProvidedCustomConverter(typeof(System.String), options)) != null)
                    {
                        _String = new JsonValueInfo<System.String>(customConverter, options);
                        _String.NumberHandling = null;
                    }
                    else
                    {
                        _String = new JsonValueInfo<System.String>(new StringConverter(), options);
                        _String.NumberHandling = null;
                    }
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

### Using the source generator

The `System.Text.Json` source generator is  The source generator can be consumed in any .NET C# project, including console application, class libraries, and Blazor applications. If the application's TFM is `net6.0` or upwards (inbox scenarios), then the generator will be part of the SDK(TODO: is this technically correct). For out-of-box scenarios such as .NET framework applications and .NET Standard-compatible libaries, the generator can be consumed via a [`System.Text.Json` NuGet package](https://www.nuget.org/packages/System.Text.Json) reference. See ["Inbox Source Generators"](https://github.com/dotnet/designs/blob/eea3e42d02930a1528c7c1cefe427507fa928b91/accepted/2021/InboxSourceGenerators.md) for details on how we can ship inbox source generators.

## Design

### Type discovery

Type discovery refers to how the source generator is made aware of types that will be passed to `JsonSerializer`. An explicit model where users manually indicate each type using a new assembly-level attribute (`[assembly: JsonSerializable(Type)]`) is employed.  This model is safe and ensures that we do not skip any types, or include unwanted types. In the future we can introduce an implicit model where the generator scan source files for `T`s and `Type` instances passed to the various `JsonSerializer` methods, but it is expected that the explicit model remains relevant to cover cases where serializable types cannot easily be detected by inspecting source code.

Here are some sample usages of the attribute for type discovery:

```cs
[assembly: JsonSerializable(typeof(MyClass))]
[assembly: JsonSerializable(typeof(object[]))]
[assembly: JsonSerializable(typeof(string), CanBeDynamic = true)]
[assembly: JsonSerializable(typeof(int), CanBeDynamic = true)]

_ = JsonSerializer.Serialize(new MyClass(), JsonContext.Default.MyClass);
_ = JsonSerializer.Serialize(new object[] { "Hello", 1 }, JsonContext.Default.ObjectArray);
```

The attribute instances instruct the source generator to generate serialization metadata for the `MyClass`, `object[]`, `string`, and `int` types. The `CanBeDynamic` property tells the generator to structure its output such that the metadata for the `string`, `int` can be retrieved by an internal dictionary-based look up with the types as keys, since those values will be serialized polymorphically. The `MyClass` and 

Based on the serializer usages in the example, we could expect that a source generator can inspect this code and automatically figure out what type needs to be serialized. This expectation is valid, and such functionality can be provided in the future. However, the explicit model will still be needed to handle scenarios such as the following:

```cs
[assembly: JsonSerializable(typeof(MyType))]

byte[] payload = GetJsonPayload();
Type type = GetSomeType(); // the returned type could be `MyType`

object obj = JsonSerializer.Deserialize(payload, JsonContext.Default);
```

### Generated metadata

There are three major classes of types, corresponding to three major generated-metadata representations: primitives, objects (types that map to JSON object representations), and collections.

TODO: deep dive into different types of generated metadata.

- Metadata internal to each compiling assembly
- Other approaches (Steve's doc - https://gist.github.com/steveharter/d71cdfc25df53a8f60f1a3563d13cf0f)

### How generating metadata helps meet goals

#### Faster startup & reduced private memory

Moving the generation of type metadata from runtime to compile time means that there is less work for the serializer to do on start-up, which leads to a reduction in the amount of time it takes to perform the first serialization or deserialization of each type.

The serializer uses Reflection.Emit where possible to generate fast member accessors to constructors, properties, and fields. Generating these IL methods takes a non-trivial time, but also consumes private memory. With source generators, we are able to generate delegates that statically invoke these accessors. These delegates are the used by the serializer alongside other metadata. This eliminates time and allocation cost due to Reflection emit.

All serialization and deserialization of JSON data is ultimately peformed within `System.Text.Json` converters. Today the serializer statically initializes several built-in converter instances to provide default functionality. User applications pay the cost of these allocations, even when only a few of these converters are needed given the input object graphs. With source generation, we can initialize only the converters that are needed by the types indicated to the generator.

Given a very simple POCO like the one used for the TechEmpower benchmark, we can observe startup improvements when [serializing and deserializing](https://github.com/layomia/JsonSourceGenPerf/blob/main/Scenarios/TechEmpower/Startup/Program.cs) instances of the type:

```cs
public struct JsonMessage
{
    public string message { get; set; }
}
```

##### Serialize

**Old**

Private bytes (KB): 2786.0

Elapsed time (ms): 41.0

**New**

Private bytes (KB): 2532.0

Elapsed time (ms): 30.0

**Writer**

Private bytes (KB): 337.0

Elapsed time (ms): 8.25

##### Deserialize

**Old**

Private bytes (KB): 1450

Elapsed time (ms): 30.25

**New**

Private bytes (KB): 916.0

Elapsed time (ms): 13.0

**Reader**

Private bytes (KB): 457.0

Elapsed time (ms): 5.0

* not fair as only one prop with no complex lookup

Given the following object graph designed to similate a microservice that returns the weather forecase for the next 5 days, we can also notice startup improvements: 

```cs
public class WeatherForecast
{
    public DateTime Date { get; set; }

    public int TemperatureC { get; set; }

    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    public string Summary { get; set; }
}
```

##### Serialize

**Old**

Private bytes (KB): 3209.0

Elapsed time (ms): 48.25

**New**

Private bytes (KB): 2693.0

Elapsed time (ms): 36.0

**Writer**

Private bytes (KB): 815

Elapsed time (ms): 15.5

##### Deserialize

**Old**

Private bytes (KB): 1698.0

Elapsed time (ms): 36.5

**New**

Private bytes (KB): 1093

Elapsed time (ms): 19.5

It is natural to wonder if we could yield more performance here given the simple scenarios and the performance of the low level reader and writer. It is indeed possible to do so in limited scenarios like the ones described above. It is in scope for 6.0 to provide a mode that is closer in performance to the reader and writer. It is also planned to provide more knobs in the future to provide better throughput for more complex scenarios. See the "Throughput" section below for more notes on this.

Also see this [gist](https://gist.github.com/layomia/25ce825e2f9abc22e49b344edae65601) to see how the startup performance scales as we add more types, properties, and serialization attributes. TODO: get updated numbers.

#### Reduced app size

By generating metadata at compile-time instead of at runtime, two major things are done to reduce the size of the consuming application. First, we can detect which custom or built-in `System.Text.Json` converter types are needed in the application at runtime, and reference them statically in the generated metadata. This allows the ILLinker to trim out JSON converter types which will not be needed in the application at runtime. Similarly, reflecting over input types at compile-time eradicates the need to do so at compile-time. This eradicates the need for lots of `System.Reflection` APIs at runtime, so the ILLinker can trim code internal code in `System.Text.Json` which interacts with those APIs. Unused source code further in the dependency graph is also trimmed out.

Size reductions are based on how `JsonSerializerOptions` instances are created. If you use the existing patterns, then all the converters and reflection-based support will be rooted in the application for backwards compat:

```cs
var options = new JsonSerializerOptions(); // or
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
```

If you use a new pattern, then we can shed these types when unused:

```cs
var options = JsonSerializerOptions.CreateForSizeOpts(); // or
var options = JsonSerializerOptions.CreateForSizeOpts(JsonSerializerDefaults.Web); // same overload, optional defaults
```

When copying options, the new instance inherits the pattern of the old instance:

```cs
var newOptions = new JsonSerializerOptions(oldOptions); 
```

TODO: `NotSupportedException` is thrown if options is created for size, and metadata for a type hasn't been provided.

TODO: do we need to expose a `JsonSerializerOptions.IsOptimizedForSize` so that users can know whether they can use an options instance with existing overloads.

Given the weather forecast scenario from above, we can observe size reductions in both a Console app and the default Blazor app when processing JSON data. In a Blazor app, we get **~ 121 KB** of compressed dll size savings. In a console app we get about **400 KB** in size reductions, post cross-gen and linker trimming. TODO: get updated numbers for console app.

TODO: if using POCO with 5 props as example, how many of those would we need in app before we start making the app bigger?

#### ILLinker friendliness

By avoiding runtime reflection, we avoid the primary coding pattern that is unfriendly to ILLinker analysis. Given that this code is trimmed out, applications that use the `System.Text.Json` go from having several ILLinker analysis warnings when trimming to having absolutely none. This means that applciations that use the `System.Text.Json` source generator can be safely trimmed, provided there are no other warnings due to the user app itself, or other parts of the BCL.

#### Throughput

##### Why not generate serialization code directly, using the `Utf8JsonReader` and `Utf8JsonWriter`?

The metadata-based design does not currently provide throughput improvements, but is forward-compatible with doing so in the future. Improving throughput means generating specific serialization and deserialization logic for each type in the object graph. This can lead to a large, complex, and unserviceable amount of code to generate, particularly when combined with advanced features like `async` and polymorphic serialization. The large amount of code that can potentially be generated also conflicts with the goal of app size reductions. With this in mind, we decided to start with an approach that works well for all usages of the serializer.

In the feature, we plan to build on the design and provide knobs to employ different source generation modes that also improve throughput for different scenarios. For instance, an application can indicate to the generator ahead of time that it will not use a naming policy or custom converters at runtime. With this information, we could generate a reasonable amount of very fast serialization and deserialization logic using the writer and reader directly. Another mode could be to generate optimal logic for a happy-path scenario, but also generate metadata as a fallback in case runtime-specified options need more complex logic.

A mode to provide throughput improvements for simple scenarios like the TechEmpower JSON benchmark is in scope for .NET 6.0, and this design will be updated to include it.

## API Proposal

<details>
<summary>(click to view)</summary>

`System.Text.Json.SourceGeneration.dll`

The following API will be generated into the compiling assembly, to mark serializable types.

```cs
namespace `System.Text.Json`.SourceGeneration
{
    /// <summary>
    /// Instructs the `System.Text.Json` source generator to generate serialization metadata for a specified type at compile time.
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
namespace `System.Text.Json`
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

namespace `System.Text.Json`.Serialization
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

namespace `System.Text.Json`.Serialization.Converters
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
        public JsonIgnoreCondition? IgnoreCondition { get { throw null; } set { } }
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

## Implementation considerations

### Compatibility with existing `JsonSerializer` functionality

All functionality that exists in `JsonSerializer` today will continue to do so after this new feature is implemented, assuming the `JsonSerializerOptions` are not created for size optimizations. This includes rooting all converters and reflection-based logic for a runtime warm up of the serializer if the existing methods are used. If the `JsonSerializerOptions.CreateForSizeOpts` method is used to create the options, then a `NotSupportedException` will be thrown if serialization is attempted.

### What is the model for implementing and maintaining new features moving forward

For the first release, every feature that the serializer supports will be supported when the source generator is used. This involves implementing logic in `System.Text.Json.SourceGeneration.dll` to recognize static options like serializationa attributes. Each new feature needs to be implemented in the serializer (`System.Text.Json.dll`), but now the source generator must also learn how to check whether it is used at design time, so that the appropriate metadata can be generated.

### Interaction between options instances and context classes

With the current design, a single `JsonSerializerOptions` instance can be used in multiple `JsonSerializerContext` classes. Multiple context instances could populate the options instance with metadata like converters and customization options. This presents a challenge, as APIs like `JsonSerializerOptions.GetConverter(Type)` must now depend on first-one-wins semantics among the context classes to fetch a converter from. This can be worked around with validation to enforce a 1:1 mapping from options instance to context instance. An alternative option might be to make the `JsonSerializerContext` type derive from `JsonSerializerOptions`. This enforces a 1:1 pairing, and might also be a more natural representation of the relationship given that options instances cache references to type metadata, as opposed to being just a lightweight POCO for holding runtime-specified serializer configuration.

Discuss context class deriving from JsonSerializerOptions

### What does the metadata pattern mean for applications, APIs, services that perform JsonSerialization on behalf of others?

Services that perform JSON processing on behalf of others, such as `Blazor` client APIs, `ASP.NET MVC`, and APIs in the `System.Net.Http.Json` library must provide new APIs to accept `JsonSerializerContext` instances that were generated in the user's assembly.

### Versioning

TODO. How to ensure we don't invoke stale/buggy metadata implementations?

Integer property or generated JsonContext class(es) which we can check at runtime.