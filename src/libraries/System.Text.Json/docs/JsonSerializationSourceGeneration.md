# System.Text.Json build-time generation for serializers using SourceGenerators

## Motivation

There are comprehensive [documents](https://github.com/dotnet/designs/pull/113) detailing the needs and benefits of generating JSON serializers at compile time. Some of these benefits are improved startup time, reduction in private memory usage, and the most obvious, faster runtime for serialization/deserialization. After discussing some approaches and pros/cons of some of them we decided to go ahead and implement this feature using [Roslyn Source Generators](https://github.com/dotnet/roslyn/blob/master/docs/features/source-generators.cookbook.md). This document will outline the roadmap for the initial experiment and highlight actionable items for the base prototype.

## New API Proposal

```C#
namespace System.Text.Json.Serialization
{
    /// <summary>
    /// When placed on a type, will source generate de/serialization for the specified type and it's descendants.
    /// </summary>
    /// <remarks>
    /// Must take into account that type discovery using this attribute is at compile time using Source Generators.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
    public sealed class JsonSerializableAttribute : JsonAttribute
    {
        public JsonSerializableAttribute() { }

        public JsonSerializableAttribute(Type type) { }
    }
}
```

## Example Usage

```C#
 // (Base Case) Codegen De/Serialization as extensions of SerializableClass. 
[JsonSerializable] 
public partial class SerializableClass 
{ 
    public string Email { get; set; } 
    public string Password { get; set; } 
}

// (Pass in Type) Codegen De/Serialization extending SerializableClassExtension using ExternClass. 
[JsonSerializable(typeof(ExternalClass))] 
public static partial class SerializableClassExtension { }

// Using the generated source code.
SerializableClass obj = SerializableClass.Deserialize(json);
string serializedObj = SerializableClass.Serialize(obj);

// (WIP) High level usage of serialization using contexts.
using (var context = new MyJsonSerializerContext(options))
{
    SerializableClass obj = context.SerializableClass.Deserialize(json);
}
```

## Feature Behavior
There are 3 main points in this project: type discovery, source code generation, generated source code integration (with user applications):

### Type Discovery

Type discovery can be divided into two different models, an implicit model (where the user does not have to specify which types to generate code for) and an explicit model (user specifically specifies through code which types they want us to generate code for). 

#### Implicit Model
Various implicit approaches have been discussed such as source generating all partial classes or scanning for calls into the serializer using the Roslyn tree scanning. These models can be revisited in the future as the value/feasibility of the approach becomes clearer based on user feedback. It is important to note that some downsides to such model can result in missing types to source generate or source generating types when not needed due to a bug or edge cases we didn’t consider. 

#### Proposed Explicit Model
There are two scenarios within the proposed explicit model:

1. Base case: Code generates a partial class to the attribute target class/struct.

```c#
 // (Base Case) Codegen De/Serialization as extensions of SerializableClass. 
[JsonSerializable] 
public partial class SerializableClass 
{ 
    public string Email { get; set; } 
    public string Password { get; set; } 
    public bool RememberMe { get; set; } 
}
```
2. Pass in type: Code generates a partial class to the attribute target class/struct using the passed in type. This scenario can be used if you don't want to make your serializable class partial or you don't own the serializable class.

```c#
// (Pass in Type) Codegen De/Serialization extending SerializableClassExtension using ExternClass. 
[JsonSerializable(typeof(ExternalClass))] 
public static partial class SerializableClassExtension { }
```

The proposed approach for source code generation requires JSON serializable types defined by the user to be partial since source generation does not change the user’s code and we want to extend it and to allow the serialization of non-public members for owned types.

The output of this phase would be a list of reflection-type-like model where we can iterate through the type's members in order to codegen recursively. The scope of this phase is to only find the root serializable types instead of the whole type-graph since we want to recursively codegen without storing the whole type-graph in memory. 

We believe that an explicit model using attributes would be a simple first-approach to the problem given that the source code generation needs the user to declare their type class as partial anyway. We can then use Roslyn tree to find the JsonSerializable attribute for both types the user owns and doesn’t own to source generate using Roslyn Source Generators.

### Source Code Generation
This phase consists of taking the discovered types and recursively codegenerating the serialization methods.

#### Proposed Approach
The expected code generation has been already been [tackled](https://github.com/dotnet/runtimelab/compare/master...steveharter:ApiAdds) by @steveharter focusing mainly on performance gains and extendibility to the current codebase. This approach increases performance drastically in 2 different ways. The first would be during the first-time/warm-up performance for both CPU and memory by avoiding costly reflection to build up a Type metadata cache mentioned here. The second would be throughput improvement by avoiding the initial metadata-dictionary lookup on calls to the serializer by generating ```CreateObjectFunc```, ```SerializeFunc``` and ```DeserializeFunc``` when creating its ```ClassInfo```. 

#### Sketch of SouceGenerated Code (for simple POCO using SerializableClass)
CreateObjectFunc:
```c#
private static object CreateObjectFunc()
{
    return new SerializableClass();
}
```

SerializeFunc:
```c#
private static void SerializeFunc(Utf8JsonWriter writer, object value, ref WriteStack writeStack, JsonSerializerOptions options)
{
    SerializableClass obj = (SerializableClass)value;

    _s_Property_Email.WriteValue(obj.Email, writer);
    _s_Property_Password.WriteValue(obj.Password, writer);
}
```

DeserializeFunc:
```c#
    private static SerializableClass DeserializeFunc(ref Utf8JsonReader reader, ref ReadStack readStack, JsonSerializerOptions options)
    {
        bool ReadPropertyName(ref Utf8JsonReader reader)
        {
            return reader.Read() && reader.TokenType == JsonTokenType.PropertyName;
        }

        ReadOnlySpan<byte> propertyName;
        SerializableClass obj = new SerializableClass();

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
            _s_Property_Name.ReadValueAndSetMember(ref reader, ref readStack, obj);
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

It is also important to notice that in case there are nested types within the root type we are recursing over, a new class with name ```FoundTypeNameSerializer``` will have to be created in order to completely serialize and deserialize.

Even if the source generation fails, we can always fallback to the slower status quo by using ```Reflection```.

#### Alternatives
Some alternatives such as the use of interfaces for the functions mentioned above or the creation of individual JsonConverter for each type were talked about. However, due to performance and the direction we are taking with the initial prototype, we believe these are not necessary. After user feedback we can revisit this if needed. 

### Generated Source Code Integration
There are [discussions](https://gist.github.com/steveharter/d71cdfc25df53a8f60f1a3563d13cf0f) regarding integration of the approach mentioned above.

The high level registration for the generated source code implies that the Json options class is modified by calling generated code where we use de/serialization API entry points for the extended class that auto-registers itself.

For the most part the source code generation approach mentioned above solves this problem since the serialize and deserialize functions would live within the type class and once the serializer is initialized, the serializer can be used calling ```JsonSerializer.Serialize<Type>()``` or ```SourceGeneratedType.Serialize()```. In order to continue with this approach, an extension of the type class would be needed where the users would have to explicitly declare their types as partial classes for it to be extended by the source generator while the types they don’t own would be entirely created. 

Even if this initialization isn't performed, we can always fallback to the slower status quo ```JsonSerializer``` methods.

## Future Considerations

* **Versioning**: This will be needed in order to determine compatibility and to be able to detect the bugs related to the different releases of this feature.
* **Error Handling**: Currently if something goes wrong in the source generation or code generation, Roslyn's SourceGenerator default message is shown to the user. This needs to be handled to show compilation errors from the source generated code to the user to be more verbose. 