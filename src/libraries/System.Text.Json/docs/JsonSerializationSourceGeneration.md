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

Type discovery refers to how the source generator. For v1 we provide an explicit approach where each root serializable type is indicated to the generator via `System.Text.Json.Serialization.JsonSerializableAttribute`. This model is safe and ensures that we do not skip any types, or include unwanted types. In the future we can scan for `T`s and `System.Type` instances passed to the various serialization overloads.

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
Blazor: contributes ~70 KB compressed size reduction in default Blazor app.

## API Proposal

TODO.
See https://github.com/dotnet/runtimelab/blob/feature/JsonCodeGen/src/libraries/System.Text.Json/ref/System.Text.Json.cs.


## Versioning

TODO. How to ensure we don't invoke stale/buggy metadata implementations?

Integer property or generated JsonContext class(es) which we can check at runtime.

## Compatibility with existing `JsonSerializer` functionality

TODO. Assert that all functionality that exists today will continue to in source-gen mode. Discuss how new features need to be honored by generator (not only implemented by serializer internals) moving foward.