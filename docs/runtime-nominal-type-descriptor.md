# Swift Nominal Type Descriptor

In Swift, descriptors are implicit data types that are separate from type metadata to provide more information about the type.
Nominal type descriptors in Swift provide information about elements that are common to most nominal types: struct, class and enum. Protocols are **not** included, even though they are technically nominal types. Protocols with associated types are also **not** included as [has been discussed before](binding-pats.md) PATs are not actual types.

Nominal type descriptors contain the following information that we will need at runtime:

- the name of the type and through chaining of descriptors, the full type name.
- the generic type information

The first is useful for runtime debugging.
The second is vital for runtime type mapping. Consider the following Swift code:

```swift
public protocol SomeProtocol {
    var name { get; }
}

public class Foo<T> : SomeProtocol {
    private var value: T
    public init (with: T) {
        value = with
    }
    public var name { get { return "Pat" } }
    public func printValue()
    {
        print ("\(value)")
    }
}

public func getAThing() -> SomeProtocol
{
    return Foo<Int> (with: 17)
}
```

In C# when we call `getAThing` we will get an existential container representing the protocol. We would normally build a proxy for the protocol which encapsulates the existential container and forwards the methods. We can and should be able to do better than this by looking at the type metadata inside the existential container. We will find the type metadata for `Foo<T>`, but we need the nominal type descriptor to be able to get the bound types. From that information we can instead runtime bind this to the C# binding for `Foo<T>` with the correct bound generic type and then in C# we could do this:

```csharp
ISomeProtocol p = GetAThing();
if (p is Foo<nint> fooInst) {
    fooInst.printValue()
} else {
    // ...
}
```

which enables us to correctly cast the return value to its actual type, something we can't do otherwise.

In terms of representation, we can use something like this:

```csharp
public struct NominalTypeDescriptor {
    SwiftHandle handle;

    // this should only get called by TypeMetadata
    internal NominalTypeDescriptor(SwiftHandle handle)
    {
        this.handle = handle;
    }
    // since the handle is a singleton, we could have a static cache for
    // both the Name and FullName if we wanted.
    public string Name { get { /* ... */ } }
    public string FullName { get { /* ... */ } }

    public bool IsGeneric { get { /* ... */ } }
    public IEnumerable<TypeMetadata> GenericParameters { get { } }
}
```
