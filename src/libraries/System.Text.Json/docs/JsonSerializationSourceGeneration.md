# System.Text.Json build-time generation for serializers using SourceGenerators

## Motivation

There are comprehensive [documents](https://github.com/dotnet/designs/pull/113) detailing the needs and benefits of generating JSON serializers at compile time. Some of these benefits are faster throughput, **improved startup time**, and **reduction in private memory usage** for serialization/deserialization. After discussing some approaches and pros/cons of some of them we decided to go ahead and implement this feature using [Roslyn Source Generators](https://github.com/dotnet/roslyn/blob/master/docs/features/source-generators.cookbook.md). This document will outline the roadmap for the initial experiment and highlight actionable items for the base prototype.

## Feature Behavior
There are 3 main points in this project: type discovery, source code generation and generated source code integration (with user applications):

### Type Discovery

Type discovery can be divided into two different models, an implicit model (where the user does not have to specify which types to generate code for) and an explicit model (user specifically specifies through code which types they want us to generate code for). 

#### Implicit Model
Various implicit approaches have been discussed such as source generating all partial classes or scanning for calls into the serializer using the Roslyn tree scanning. These models can be revisited in the future as the value/feasibility of the approach becomes clearer based on user feedback. It is important to note that some downsides to such model can result in missing types to source generate or source generating types when not needed due to a bug or edge cases we didn’t consider. 

#### Proposed Explicit Model
There are two scenarios within the proposed explicit model:

1. Base case: Code generates a facade ```JsonClassInfo``` for the attribute target class/struct.

```c#
 // (Base Case) Codegen (De)Serialization ExampleLoginClassInfo. 
[JsonSerializable] 
public class ExampleLogin
{ 
    public string Email { get; set; } 
    public string Password { get; set; } 
}
```
2. Pass in type: Code generates a ```JsonClassInfo``` for the attribute target class/struct using the passed in type. This scenario can be used if you don't own the serializable class.

```c#
// (Pass in Type) Codegen (De)Serialization to create ExampleExternalClassInfo using ExternClass. 
[JsonSerializable(typeof(ExternalClass))] 
public static class ExampleExternal { }
```

The output of this phase would be a list of reflection-type-like models where we can iterate through the type's members in order to codegen recursively. The scope of this phase is to only find the root serializable types instead of the whole type-graph since we want to recursively codegen without storing the whole type-graph in-memory. 

We believe that an explicit model using attributes would be a simple first-approach to the problem given that the source code generation needs the user to declare their type class as partial anyway. We can then use Roslyn tree to find the JsonSerializable attribute for both types the user owns and doesn’t own to source generate using Roslyn Source Generators.

#### New API Proposal

```C#
namespace System.Text.Json.Serialization
{
    /// <summary>
    /// When placed on a type, will source generate (de)serialization for the specified type and all types in it's object graph.
    /// </summary>
    /// <remarks>
    /// Must take into account that type discovery using this attribute is at compile time using Source Generators.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class JsonSerializableAttribute : JsonAttribute
    {
        /// <summary>
        /// Takes target class/struct to construct a facade JsonClassInfo as TargetNameClassInfo.
        /// </summary>
        public JsonSerializableAttribute() { }

        /// <summary>
        /// Takes type as an argument and uses it to create a facade JsonClassInfo as TargetNameClassInfo.
        /// </summary>
        public JsonSerializableAttribute(Type type) { }
    }
}
```

#### Validation and Testing

For validations we will handle cases where the type representation has missing required fields to source generate. This can be done in the current phase or the Source Code Generation phase but must be handled in both.

Testing for this phase will consist of unit tests where given different source code and referenced assemblies, we verify that the source generation pass detects and creates all of the type representation with necessary data.

### Source Code Generation
This phase consist of taking the discovered types and recursively codegenerating ```JsonClassInfo``` along with its registration.

#### Proposed Approach

The design for the generated source focuses mainly on performance gains and extendibility to the current codebase. This approach improves performance in two ways. The first would be during the first-time/warm-up performance for both CPU and memory by avoiding costly reflection to build up a Type metadata cache mentioned [here](https://github.com/dotnet/runtime/issues/38982). The second would be throughput improvement by avoiding the initial metadata-dictionary lookup on calls to the serializer by generating ```CreateObjectFunc```, ```SerializeFunc``` and ```DeserializeFunc``` when creating its ```JsonClassInfo``` (metadata) that would be used to (de)serialize with overloaded JsonSerializer functions using a wrapper which will be mentioned in the integration phase. 

The proposed approach consist of an initialization phase where generated code will call an initialization method within the created facade class where a ```JsonClassInfo``` is created with the functions mentioned above and registered into options with the necessary ```JsonPropertyInfo```. For each call into the serializer using the generated code, the POCO would call a public overload into the ```JsonSerializer``` that also take the metadata ```JsonClassInfo``` created during the initialization method.

#### Sketch of SourceGenerated Code (for simple POCO using SerializableClass)

Class variables for code generated ```ExampleLoginClassInfo```:
```c#
private static bool _s_isInitiated;
private static JsonClassInfo _s_classInfo;

private static JsonPropertyInfo<string> _s_Property_Email;
private static JsonPropertyInfo<string> _s_Property_Password;
```

These functions would used to create a ```JsonClassInfo```:

```c#
// CreateObjectFunc
private static object CreateObjectFunc()
{
    return new ExampleLogin();
}
```

```c#
// SerializeFunc
private static void SerializeFunc(Utf8JsonWriter writer, object value, ref WriteStack writeStack, JsonSerializerOptions options)
{
    ExampleLogin obj = (ExampleLogin)value;

    _s_Property_Email.WriteValue(obj.Email, writer);
    _s_Property_Password.WriteValue(obj.Password, writer);
}
```

```c#
// DeserializeFunc
private static ExampleLogin DeserializeFunc(ref Utf8JsonReader reader, ref ReadStack readStack, JsonSerializerOptions options)
{
    bool ReadPropertyName(ref Utf8JsonReader reader)
    {
    return reader.Read() && reader.TokenType == JsonTokenType.PropertyName;
    }

    ReadOnlySpan<byte> propertyName;
    ExampleLogin obj = new ExampleLogin();

    if (!ReadPropertyName(ref reader)) goto Done;
    propertyName = reader.ValueSpan;

    if (propertyName.SequenceEqual(_s_Property_Email.NameAsUtf8Bytes))
    {
        reader.Read();
        _s_Property_Email.ReadValueAndSetMember(ref reader, ref readStack, obj);
        if (!ReadPropertyName(ref reader)) goto Done;
        propertyName = reader.ValueSpan;
    }

    if (propertyName.SequenceEqual(_s_Property_Password.NameAsUtf8Bytes))
    {
        reader.Read();
        _s_Property_Password.ReadValueAndSetMember(ref reader, ref readStack, obj);
        if (!ReadPropertyName(ref reader)) goto Done;
        propertyName = reader.ValueSpan;
    }

    reader.Read();

    Done:
        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException("Could not deserialize object");
        }

        return obj;
    }
}
```

User faced methods for (de)serialization (assuming ExampleLoginClassInfo is initialized):
```c#
public static ExampleLogin Deserialize(string json)
{
    return JsonSerializer.Deserialize<ExampleLogin>(json, this._s_classInfo);
}

public static string Serialize()
{
    return JsonSerializer.Serialize(this, this._s_classInfo);
}
```

It is also important to notice that in case there are nested types within the root type we are recursing over, a new class with name ```FoundTypeNameClassInfo``` will have to be created in order to completely serialize and deserialize.

Even if the source generation fails, we can always fallback to the slower status quo by using ```Reflection``` at runtime.

#### Validations and Tests

Validations for this phase will happen for each type where we will use Roslyn's API to verify that the code we are generating is valid C# syntax code. If validations fail we will only include in the output the generated code that do not contain errors and leave the rest for fallback methods. These validation errors should produce output to users at compile time.

There will be unit tests that checks the source code generation by checking output source code generation given multiple types that could be discovered in the Type Discovery phase.

#### Alternatives
An alternative approach involving the creation of individual JsonConverter for each type was talked about. However, we believe that the current design provides the potential perf benefits of that approach in a way that is more serviceable, scalable, and has better integration with the serializer (to utilize support for more complex features).

### Generated Source Code Integration
There are [discussions](https://gist.github.com/steveharter/d71cdfc25df53a8f60f1a3563d13cf0f) regarding integration of the approach mentioned above.

The proposed approach consists of the generator creating a context class (```JsonSerializerContext```) which takes an options instance that contains references to the generated ```JsonClassInfo```s for each type seen above. This relies on the creation of new overloads to the current serializer that take ```JsonClassInfo```s  that can be retrieved from the context. An example of the overload and usage can be seen [here](https://github.com/dotnet/runtimelab/compare/master...steveharter:ApiAdds) while examples and details of the end to end approach can be seen as follows: 

```c#
public class JsonSerializerContext : IDisposable
{
    public JsonSerializerContext();
    public JsonSerializerContext(JsonSerializerOptions options);
    public JsonSerializerOptions JsonSerializerOptions { get; };

    // Generated JsonClassInfos.
    public JsonClassInfo<ExampleLogin> ExampleLoginClassInfo { get; }
    public JsonClassInfo<ExampleExternal> ExampleExternalClassInfo { get; }
}
```

For cases where users may not have enough context to call more specific overloads proposed (such as ASP.NET) we are considering ways of looking up the type's metadata that points to the type's ```JsonClassInfo``` so part of this feature's benefits could be received.

#### Example Usage

```C#
 // (Base Case) Codegen (De)Serialization of ExampleLogin into ExampleLoginClassInfo. 
[JsonSerializable] 
public class ExampleLogin
{ 
    public string Email { get; set; } 
    public string Password { get; set; } 
}

// (Pass in Type) Codegen (De)Serialization of ExampleExternal into ExampleExternalClassInfo using ExternalClass. 
[JsonSerializable(typeof(ExternalClass))] 
public static class ExampleExternal { }

// High level usage of serialization using JsonSerializableContext.
using (var context = new MyJsonSerializerContext(options))
{
    ExampleLogin obj = context.ExampleLoginClassInfo.Deserialize(json);
    ExternalClass obj = context.ExampleExternalClassInfo.Deserialize(json);
}
```

#### Validations and Tests

Validations for this tests will be mostly burdened by Roslyn's API error handling where if something goes wrong in the first two phases, it won't include any generated code into the final compilation along with the validations mentioned in the previous phases. However, there will be end to end tests that verify the error handling, generated source code and types that were generated given source codes.

## Future Considerations

* **Versioning**: This will be needed in order to determine compatibility and to be able to detect the bugs related to the different releases of this feature.
* **Error Handling**: Currently if something goes wrong in the source generation or code generation, Roslyn's SourceGenerator default message is shown to the user. This needs to be handled to show compilation errors from the source generated code to the user to be more verbose. 
* **Linker Trimming**: Adding linker trimming test to ensure we have everything for both generated code and application code will be necessary.