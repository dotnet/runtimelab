# Binding Type Database

In binding a Swift module, there will be nominal type references presented to the binder that have been bound previously. The binder will need to perform a few important operations on these types:

1. Determine the C# type that should be used in the public-facing API and what namespaces need to be referenced
2. Determine the size, stride, and alignment of value types that contain the type reference, if known at binding time.

After binding the module, the binder will have new type database entries to write.

Therefore, it makes sense to aggregate a collection of type database files for each module. The binding tool can either read them all in at the start of the world or read them in lazily for each module reference.

The file format for the type database files is not vitally important beyond the fact that it should be easily serializable. Either XML or JSON would be fine.

Here are the elements that are likely to be needed in a type database entry:

- Swift module name
- Swift type name (may be a type path for inner types)
- Size, stride, and alignment (if knowable)
- C# namespace
- C# type name (may be a type path for inner types)
- Swift entity type (struct, enum, class, actor, protocol)
- Whether or not the type is blittable
- Whether or not the type is frozen

(idea - maybe also include an optional C# pinvoke type)

At binding time, a two-stage process will be necessary for new types introduced by the module being bound. This is because types may reference other types in the module before they have been defined. For example, this is valid Swift:

```swift
public struct B {
    public var a: A // not yet defined
    public init (theA: A) {
        a = theA
    }
}

public struct A {
    public var b: Int = 7
}
```

The first stage is type introduction. During this stage, the type database will be populated with a "light" version of the information available. The information that is available at this point is limited to the type names and the entity types.

The second stage is type binding, wherein the actual C# type is generated. At this stage, it will be necessary to have the full information, in addition to other information about the types. As you see in the example above, it will be to either build a dependency graph to inform the binding order or to make the binding process recursive. The downside to making the binding process recursive is that it makes it more challenging to bind in parallel.

It might be useful in the binding process to have two data structures: one for the type database entry proper and one for the types that are being bound. The reason for this is that it will be useful for binding to have information about the type that wouldn't be needed in the type database. For example, at binding time, we will need information as to whether the type is frozen, OS availability, variety of enum, and so on.

This can be accomplished in a number of ways, either by have the module defined type information kept entirely separate, subclassing or interfacing the type database entry to include extra information, etc.

The type database class should have the ability to:

- Read and merge type database entries from files and/or streams
- Add individual entries in the introduction phase of binding
- Retrieve entries using full Swift type name as the key
- Write the type database to an output file

In terms of organization, we are likely going to want to have the type database files stored in a hierarchy organized by:

- Target platform
- Target SDK or OS version (which?)
- Framework or module name
