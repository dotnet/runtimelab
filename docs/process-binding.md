# The Process of Binding

In theory, the process of binding a Swift binary into C# should be as simple as:

1. Read the type database from shipped resources as well as from any user-supplied files
2. Generate and consume the abi.json file, populating the type database
3. Extract and demangle the symbols from the binary
4. Iterate over every type and function and generate C# and/or supporting Swift code
5. Write the a type database file for every new entity
6. Optionally compile generated code

Unfortunately, it is not so simple. There are complications that need to be considered because of special cases in the Swift language as well as special cases or limitations in C#.

For example

- `open` Swift classes need to be handled differently from `public` swift classes.
- Structs and enums that are `@frozen` are passed differently from structs that are not.
- Structs and enums that contain or may contain non-blittable members need to be defined differently.
- Enums come in different forms that lend themselves to different representations in C#.
- C# doesn't have methods or properties on enums so those need to be put into extension methods.
- Any protocol implementation in C# will need proxy types in both C# and Swift
- P/invokes and `[UnmanagedCallersOnly]` entrypoints (reverse p/invokes) need to be defined in a separate class from the bound type they support if that bound type is generic.
- Mutating methods on value types need to be handled differently from non-mutating methods.
- C# does not support methods that are homonyms.

All of this makes the process of generating code challenging.

The complexities fall into several broad categories:

- Type and member naming
- Multiple types being defined in multiple languages concurrently
- Marshaling handled differently based on the type of the parameter and the type of the function
- Implicit arguments
- Versioning based on the `@available` attribute.

Because of these complexities, I think we should adopt a strategy and factory pattern for handlers at various levels.

The general pattern would work like this:

1. Start with a Swift language entity
2. Aggregate information about that entity
3. Select a factory to create a handler for that entity
4. The handler will generate a context object for handling that entity
5. Execute a series of steps through the handler that will do work appropriate for each step
6. Aggregate the result and generate as needed

Handlers will contain factories for handling sub steps, if needed.

## Argument for Non-Linear Code Generation

I strongly recommend using code-generation tools that can work in a non-linear fashion. The Dynamo framework from Binding Tools for Swift is an excellent candidate as it can handle both C# and Swift and generates non-linearly. In addition, it's not a stretch to have multithreaded binding on type boundaries that are defined at the same level.

Dynamo works by building functional language components out of small objects and aggregating them into what is effectively an abstract syntax tree. Because of this approach, it is possible to do things with the tree that for a string-based system are impractical. Some examples of this include:

- Maintaining import/using statements on as as-needed basis
- Being able to easily insert fixed blocks
- Modifying a declaration late to be unsafe
- Being able to write several types that logically belong in one file in parallel for example, for protocols with associated types and virtual classes, we can write methods and their corresponding p/invoke and their corresponding reverse p/invoke "receiver" at the same time
- Being able to insert variable declarations at any point in the process
- Being able to attach attributes to any entity at any point in the process

All of these things are necessary in the strategy/factory/handler approach.  To do these things with a string-based generator involves buffering of strings with only as much structure and honoring of the syntax as you add into it, and by that time you're halfway to re-creating Dynamo. On top of this, with Dynamo it's actually very hard to generate syntactically incorrect code.

Consider the case of a protocol proxy implementation as outlined [here](binding-protocols.md), with this Swift code:

```swift
public protocol Colorful {
    func getColor (element: ClothingItem) -> Color
}
```

This would require:

- An interface definition in C#
- A simulated vtable in Swift
- A parallel vtable in C#
- A function to set the Swift vtable
- A p/invoke in C# to call that function
- A method to initialize the C# vtable and call the pinvoke
- A two-way proxy class in C# for adapting any C# implementation of IColorful to Swift
- An extension method for each member in the protocol in Swift
- A method in the proxy class to call either the C# or the Swift implementation of IColorful
- A reverse pinvoke in C# that would dispatch to the C# proxy method

It is far better to write these all in parallel since they are all tightly-coupled: writing the code for the `ClothingItem` argument can be done for the C# interface method, the Swift vtable function, the C# vtable function, the Swift extension method, and the proxy method all at the same time.

Suppose, for example, we decided later to not use Swift's implementation of `String` in public facing bindings but instead present it as C# `string`. It would be trivial to make that change in a non-linear code generator because of the locality of the code.

## Example of Factory Handler Pattern

```Swift
public func generateAClass(String name) -> SomeClass { }
```

The process would look at this and identify this as a top-level function and will select a handler factory for it.
The handler will create a context for the object which would include a class for the top-level object to live in (C# doesn't have top-level functions) and a class to hold top-level pinvokes and a function generation context which would include a place to place function argument declarations, generic declarations, function argument pre-marshaling code, pinvoke argument declarations, pinvoke argument expressions, post-marshaling code, return type declaration, and a return expression.

The handler will execute a step to name the function and the associated pinvoke, including the entry point and library.
Then for each argument, it will gather the necessary information and from the function handler get a factory to build an argument handler for type `String`. This will in turn name the argument, generate the C# type and add it to the C# argument declaration. It will define the argument type for the pinvoke and add it to the C# pinvoke argument list. If needed, it will generate premarshal code and add it to the premarshal list and post marshal code, and finally an expression for calling the pinvoke.

A similar process will be done for handling the return type and value. In this case, the pinvoke return type will be a `SwiftHandle` and it will be used in conjunction with a registry to either retrieve an already existing C# object that is bound to that handle or it will build one through a factory.

After all this is done, the function handler will finish up by aggregating all the information, writing the C# method and writing the C# pinvoke.

By doing this, all the special cases get isolated in their own code and state related to the code, if any, can be kept in the handler's context object.

For example, if there is a pre-marshal set up and a post marshal teardown, that code can be written in the same step.

## Handler Factory Selection

A handler factory for a given entity will be given a blob of information about the prospective entity. It will have a try-get predicate to answer whether or not it can handle that entity and a method to build a handler.

A generalized handler might look like this:

```csharp
public interface Handler<EntityInformation, ParentContext> {
    void Begin (ParentContext parentContext, EntityInformation info);
    void Process ();
    void End ();
}
```

A factory might look like this:

```csharp
public interface Factory<EntityInformation, ParentContext> {
    bool TryGetHandler (EntityInformation info, [NotNullWhen(true)] out Handler<EntityInformation, ParentContext>? handler);
}
```

Finally, since Swift allows inner types, all of these steps should be naturally recursive.
It should also be noted that all entities at top level can be processed in parallel trivially. Inner types can also be processed in parallel at the scope at which they're declared, but some care needs to be taken to ensure that two inner types at the same scope don't step on each other in writing to the non-linear representation or have issues when creating p/invokes. This could be done with appropriate locks or using `Concurrent{Dictionary/List}` for the representational types.
