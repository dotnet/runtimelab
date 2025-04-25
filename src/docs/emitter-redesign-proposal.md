# Emission redesign proposal

## Introduction

The purpose of this document is to outline the new emission process and the changes that need to be made to the current codebase.
The new emission process is designed to be more modular and extensible, allowing for easier addition of new features and improvements in the future. The goal is to create a clear separation of concerns between different parts of the codebase, making it easier to understand and maintain.

This document proposes splitting the emission process into following distinct steps:

1. Type pre-processing and validation
    - Decide whether a type is supported, if so how it is marshalled
    - Save the information to the TypeDatabase
This step makes sure that all the types that are going to go into the TypeDatabase are things that will be emitted (We want to avoid scenario of populating the TypeDatabase with types that are not going to be emitted).
2. Type processing
    - Process each type's members. Fill out an appropriate representation. Specifically, run a set of handlers for each member category which will populate the representation with information about how to marshal the member.
    - Prune out properties and methods which are not supported.
This step prunes the non-layout affecting properties and methods from the type in case they contain unsupported types.
3. Emission
    - Emit the code for each type and its members based on the information in the representation. This will involve writing raw strings to the output file, or simple data manipulations and string formatting.

## Type pre-processing and validation

The purpose of this step is to ensure that all the types that are going to be emitted are supported and to save the information about how they should be marshalled to the TypeDatabase.

This step can be broken down into two sub-steps:

1. Graph traversal and type annotation
2. Graph pruning and TypeDatabase population

### Graph traversal and type annotation

The input of this step is a collection of TypeDecls. Each declaration contains metadata about whether it is a ref type, whether it is frozen, etc.

We can think about this input as a directed graph:

- Nodes are `TypeDecls` / `TypeDatabase` entries
- Edges are fields and stored properties (`TypeSpecs`)

#### Graph Traversal Algorithm

The graph must be traversed in the correct order – we need to process all dependencies of a type before processing the type itself. This is achieved using a depth-first search (DFS) with cycle detection:

#### Marshalling Label Decision Rules

During graph traversal, we assign each type one of the following marshalling labels:

```csharp
public enum MarshallingLabel
{
    Struct,               // Frozen structs with only frozen struct fields
    ClassWithOpaquePayload, // Non-frozen structs and mixed-field frozen structs
    ClassWithBufferStruct,  // Frozen structs that reference ref types
    Class,                // Swift classes
    Unknown               // Types that cannot be marshalled
}
```

1. **Struct** - Used for frozen structs that reference only other frozen structs. These can be marshalled directly as C# structs with the same memory layout.

   Example:

   ```swift
   @frozen 
   struct Point {
       let x: Int
       let y: Int
   }
   ```

2. **ClassWithOpaquePayload** - Used for non-frozen structs or frozen structs that contain fields that must be projected as classes with opaque payload.

   Example:

   ```swift
   struct NonFrozenPoint {
       let x: Int
       let y: Int
   }
   ```

3. **ClassWithBufferStruct** - Used for frozen structs that reference ref types or other frozen structs that would be marshalled as classes with buffer structs.

   Example:

   ```swift
   @frozen struct PersonWrapper {
       let person: Person  // Person is a class
       let id: Int
   }
   ```

4. **Class** - Used for Swift classes, which are always reference types.

   Example:

   ```swift
   class Animal {
       var name: String
   }
   ```

5. **Unknown** - Used for types that cannot be marshalled or are not supported.

#### UNKNOWN Label Propagation Rules

The `Unknown` label propagates according to these rules:

1. If a type would be marshalled as something that requires layout (Struct, ClassWithBufferStruct) and any of its stored properties are `Unknown`, the type itself becomes `Unknown`.

2. Generic types are currently marked as `Unknown` since we don't yet support them.

Example of UNKNOWN propagation:

```swift
@frozen struct Container {
    let value: GenericType<Int>  // GenericType is Unknown
    let id: Int
}
// Container becomes Unknown because it has an Unknown field
```

The output of this step is an annotated graph where each node has a marshalling label. This information will be used in the subsequent pruning step.

### Prune the graph and save data to the TypeDatabase

The input of this step is the output of the last one – a collection of types with annotations how they should be marshalled.

We should iterate over the collection and prune everything with `UNKNOWN` label. After pruning is completed a new TypeRecord for each remaining type should be created.

TypeRecord should contain only information which is necessary for projecting "references" to a given type (e.g. when type occurs as a method argument or as a property / field type).

Minimal viable type record:

```csharp
public record TypeRecord
{
    public required CSharpTypeName CSharpTypeName { get; init; }
    public required SwiftTypeName SwiftTypeName { get; init; }
    public required MarshallingLabel Label { get; init; }
}
```

`MarshallingLabel` is the result of the previous step. It can be replaced by raw information about the type which allow to reconstruct the label later on (frozenness, requiresMemoryManagement). However the key thought behind this label is that things which share the same label should be marshalled the same - both declarations and references.

## Type processing

The purpose of this step is to process each type and its members, building a representation of how they should be marshalled.

The input of this step is a pruned collection of TypeDecls and a TypeDatabase containing types from the current module.

The simplest representation of a type is a `TypeRepresentation` which contains information about the type itself and its members.

```csharp
public record struct TypeRepresentation
{
    public required CSharpTypeName CSharpTypeName { get; init; }
    public required MarshallingLabel Label { get; init; }
    public required List<string> Methods { get; init; } 
    // We will need to have a separate representation for methods. This can either exist us a separate collection, or some external builder will create the method and append a serialized version of it to the representation.
    // public required List<MethodRepresentation> Methods { get; init; } 
    public required List<string> Properties { get; init; }
    public required List<string> Fields { get; init; }
}
```

Since dependant on the `MarshallingLabel` the representation of a type will contain some default code (e.g. Dispose method for things projected as classes) it might be beneficial to either have a separate representation for each label `PlainFrozenStruct` etc. or have a factory which will fill out the representation based on the label with the default code.

Also - what will be visible when dealing with methods - each `TypeRepresentation` should provide some code snippets which can be used to emit code for the methods. This includes creating appropriate `SwiftSelf` or assigning result of some method to the internal payload. Again, this can be done by implementing an interface in case of multiple representations or having some external method with a switch statement.

```csharp
public interface ISwiftStruct
{
    string ProvidePayloadAssignment();
    string ProvideSwiftSelfCreationCode();
}
```

The output of this step is a collection of `TypeRepresentation` objects. Each representation should contain information about the type itself and its members.

### Interfaces

Each of the projected type can implement some number of interfaces (at minimum every projected type will implement `ISwiftObject`). In order to keep the code for each interface handling separate we can create a set of handlers which will populate the representation with appropriate code pieces.

```csharp
public interface ITypeRepresentationHandler
{
    public bool CanHandle(TypeMarshallingInfo typeMarshallingInfo);
    void Handle(TypeMarshallingInfo typeMarshallingInfo);
}
```

where `TypeMarshallingInfo` is a simple struct which contains `TypeDecl` and `TypeRepresentation`. So for example for `ISwiftObject` `CanHandle()` would always return true and for `IEquatable` it would return true only if `TypeDecl` conforms to `Equatable` protocol on the Swift side.

Then the `Handle()` method would construct and append the code to the representation.

### Methods

Methods are by far the most complex part of the emission process. The complexity comes from the fact that we need to exactly match the Swift calling convention.

The proposal is to split the method processing into a set of independent handlers which will build up the method representation. Each handler will be responsible for a specific part of the method processing.

**Representation:**
Method can be split into a set of "blocks" which should be emitted in a specific order. Each block can be represented by a separate struct which will contain information about how to emit it.

1. Signature - This is the method signature. It contains information about the method name, return type, and arguments.
2. PInvokeRepresentation - PInvoke signature. Contains information about the arguments and return type.
3. MarshallingPhase - This phase is responsible for transforming the signatures arguments and return types into the correct format for the pInvoke call. This includes marshalling the arguments and return type, as well as creating any necessary SwiftSelf or SwiftError objects.
4. PInvokeCallPhase - This phase is responsible for making the pinvoke call. It contains information about how to make the call and what to do with the result.
5. ResultPostProcessingStep - This phase is responsible for processing the result of the pinvoke call. It contains information about how to handle the result, including any necessary error handling or post-processing - can be further split.
6. FieldsAssignmentStep - This phase is responsible for assigning the result of the pInvoke call to the fields of the type. It contains information about how to assign the result to the fields.
7. ReturnStep - This phase is responsible for returning the result of the method. It contains information about how to return the result, including any necessary error handling or post-processing.
8. CleanupStep - This phase is responsible for cleaning up any resources used by the method. It contains information about how to clean up the resources, including any necessary error handling or post-processing.

```csharp
public record struct MethodRepresentation
{
    public SignatureRepresentation Signature { get; set; }
    public PInvokeRepresentation PInvokeRepresentation { get; set; }
    public MarshallingPhase MarshallingPhase { get; set; }
    public PInvokeCallPhase PInvokeCallPhase { get; set; }
    public ResultPostProcessingStep ResultPostProcessingStep { get; set; }
    public FieldsAssignmentStep FieldsAssignmentStep { get; set; }
    public ReturnStep ReturnStep { get; set; }
    public CleanupStep CleanupStep { get; set; }
}
```

Handlers should implement a common interface:

```csharp
public interface IMethodHandler
{
    public ITypeDatabase TypeDatabase { get; init; }
    bool CanHandle(MethodDecl methodDecl);
    void Handle(MarshallingContext marshallingContext);
}
```

In the simplest scenario 1:1 mapping between MethodDecl and MethodRepresentation the `MarshallingContext` would contain the `MethodDecl` and the `MethodRepresentation`.

```csharp
public record struct MarshallingContext
{
    public MethodDecl MethodDecl { get; set; }
    public ITypeRepresentation ContainingType { get; set; }
    public MethodRepresentation MethodRepresentation { get; set; }
}
```

In reality however, the mapping will not be 1:1 e.g. `Async`. In that case we can either extend the `MethodRepresentation` to contain the information about additional methods or pass around a Dictionary of `MethodRepresentation` \ `SwiftMethodRepresentation` objects. The latter is probably better since it will allow us to keep the handlers separate and not have to worry about the additional methods.

Handlers can be split into groups:

1. Group 1 - Handlers which inspect MethodDecl metadata:
    - ConstructorHandler
    - ReturnableMethodHandler (Static, Instance)
        - StaticMethodHandler
        - InstanceMethodHandler
    - SwiftErrorHandler
    - GenericParameterHandler
    - AsyncMethodHandler (TODO)

2. Group 2 - Handlers which inspect the return type:
    - IndirectResultHandler
    - BoundGenericResultHandler
    - DirectResultHandler
    - VoidResultHandler

3. Group 3 - Handlers which inspect the arguments:
    - NonFrozenArgumentHandler
    - GenericArgumentHandler
    - BoundGenericArgumentHandler

#### Group 1 - MethodDecl Handlers

1. ConstructorHandler
    - Modifies signature
    - Populates field assignment code. This should call the `ProvidePayloadAssignment()` method on the type representation.
    - Sets a flag to skip pInvoke result postprocessing - we need to assign a raw payload to the instance.

2. ReturnableMethodHandler (Static, Instance) - Creates a step which takes the return value of the pInvoke and returns it to the caller
    - StaticMethodHandler - Adds static keyword to the method signature
    - InstanceMethodHandler - Adds `SwiftSelf` to the pInvoke signature, adds SwiftSelf creation to the marshalling phase (this should call the `ProvideSwiftSelfCreationCode()` method on the type representation) and pushes an appropriate argument to the pInvoke signature and call.

3. SwiftErrorHandler - Adds `SwiftError` to the pInvoke signature and call. Pushes appropriate check to the result postprocessing step.

4. GenericParameterHandler - Adds generic parameters to the method signature and pInvoke signature. Into marshalling phase it adds obtaining the metadata pointers and PWT pointers. Updates the pInvoke signature and the pInvoke call.

5. AsyncMethodHandler - TODO

#### Group 2 - ReturnType Handlers

1. IndirectResultHandler - Creates an indirect result in marshalling phase. Changes the pInvoke signature to return a void. Pushes `IndirectResult` argument to the pInvoke call. Updates the result postprocessing step to marshal the result from Swift.

2. BoundGenericResultHandler - Changes the pInvoke signature to return a buffer struct. Adds postprocessing step to create the bound generic struct from the buffer.

3. DirectResultHandler - Sets the pInvoke signature return type to the original direct result.

4. VoidResultHandler - Sets the pInvoke signature return type to void. Updates the pInvoke call to not return anything. (This can be merged with the DirectResultHandler and delegated to the emitter).

#### Group 3 - Argument Handlers

1. NonFrozenArgumentHandler - Updates the marshalling phase to create a handle for the argument. Pushes the handle to the pInvoke signature and call.

2. GenericArgumentHandler - Updates the marshalling phase to create a handle for the argument. Pushes the handle to the pInvoke signature and call.

3. BoundGenericArgumentHandler - Updates the marshalling phase to cast the argument to a buffer strut. Pushes the buffer struct to the pInvoke signature and call.

### Properties and fields

A property has to be marshalled as up to three different things:

- A field (stored properties on frozen structs)
- A PInvoke
- A property

A simple handler can be created which will do the necessary work and update the necessary bits of the representation.

**NOTE**: Each of the handlers will need to decide whether a given method or property is supported or not. This can be done by checking the TypeDatabase for the appropriate type. This can be done by adding a separate flag to the MethodRepresentation or by having some handler validate the arguments / properties types beforehand.

## Emission

This step is responsible for converting the representation of a type and its members into actual code. This is where the final output is generated.

Each representation might extend an interface which will provide a method for emitting the code.

```csharp
public interface ICSharpWritable
{
    void Emit(CSharpWriter writer);
}
```

A matter for discussion is whether we want to serialize the representation to a string and then emit it, or whether we want to emit the code directly from the representation. Sometimes it is easier to just append a string to the the representation rather than build up a whole representation - this is particularly true when handling interface implementation methods. One way would be to support both.
