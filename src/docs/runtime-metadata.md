# Swift Type Metadata

## Background

Swift has a struct which is implicit in the language. It is used for several tasks:
1 - Determining the classification of the type that is represented (struct, enum, class, tuple, etc)
2 - Getting access to the type descriptor associated with the type
3 - Getting access to generic types that specialize the type as well [protocols with associated types](bindings-pats.md)
4 - Labeling the contents of existential containers representing [protocols](binding-protocols.md)
5 - Specifying the types of generic parameters to functions
6 - Providing information used to allocate types

In our case, we will use this type for several things in interoperation with Swift from C#:

1 - Generating existential containers
2 - Specializing generic types
3 - Specifying the types of generic parameters in pinvokes into Swift
4 - Mapping a C# type to an equivalent Swift type and vice versa
5 - Getting access to [nominal type descriptors](runtime-nominal-type-descriptor.md) to determine size, stride, etc.

Swift type metadata is always a singleton for every unique type in Swift. The Swift runtime enforces this through accessors for getting at the type metadata and it will cache the values. In usage, the type metadata is always passed by reference but they should be considered read-only.

There are two ways to get the type metadata for a Swift type:
1 - by accessing the type metadata directly by symbolic name (only applies to non-generic nominal types)
2 - using an accessor

Within accessors, there are two classes of accessor:
1 - compiler-generated accessors (for all nominal types)
2 - runtime-provided accessors (for non-nominal types - tuples, closures, etc.)

Generally speaking, it's probably best for us to use the accessors and if we choose, to cache those values as static fields in bound types.

The signature of a type metadata accessor is a request followed by n TypeMetadata objects (one for each generic type). Those are then followed by protocol witness tables (one for each protocol conformace). The witness tables are ordered first by the generic type they correspond to, second by lexicographical order.

```csharp
public static TypeMetadata Accessor(TypeMetadataRequest request, [ TypeMetadata specialization0, TypeMetadata specialization1, ..., NativeHandle pwt0, NativeHandle pwt1, ...])
{

}
```

Also, when total number of generic types and protocol conformances exceeds three, the signature changes to contain a request followed by a pointer to a buffer containing the said handles.

```csharp
public static TypeMetadata Accessor(TypeMetadataRequest request, IntPtr parameters)
{

}
```

So for example for a sample type:

```swift
public struct Foo<T0: P2 & P0, T1, T2: P1> {...}
```

```csharp
struct MetadataParameters
{
    TypedMetadata T0;
    TypedMetadata T1;
    TypedMetadata T2;
    NativeHandle P0;
    NativeHandle P2;
    NativeHandle P1;
}
```

The request is the state of the resulting type metadata object. This is documented [here](https://github.com/swiftlang/swift/blob/9fed7159e9419e27f081364f7501f6fc7592f8c2/include/swift/ABI/MetadataValues.h#L2392), but for our needs, I don't anticipate any use but `Complete` which is 0.

## Implementation

```csharp
namespace Swift.Runtime;

[Flags]
public enum TypeMetadataFlags {
    MetadataKindIsNonType = 0x400,
    MetadataKindIsNonHeap = 0x200,
    MetadataKindIsRuntimePRivate = 0x100,
}

[Flags]
public enum TypeMetadataRequest {
    Complete = 0,
    NonTransitiveComplete = 1,
    LayoutComplete = 0x3f,
    Abstract = 0xff,
    IsNotBlocking = 0x100,
}

public enum TypeMetadataKind {
    None = 0,
    Struct = 0 | MetadataFlags.MetadataKindIsNonHeap,
    Enum = 1 | MetadataFlags.MetadataKindIsNonHeap,
    Optional = 2 | MetadataFlags.MetadataKindIsNonHeap,
    ForeignClass = 3 | MetadataFlags.MetadataKindIsNonHeap,
    Opaque = 0 | MetadataFlags.MetadataKindIsRuntimePrivate | MetadataFlags.MetadataKindIsNonHeap,
    Tuple = 1 | MetadataFlags.MetadataKindIsRuntimePrivate | MetadataFlags.MetadataKindIsNonHeap,
    Function = 2 | MetadataFlags.MetadataKindIsRuntimePrivate | MetadataFlags.MetadataKindIsNonHeap,
    Protocol = 3 | MetadataFlags.MetadataKindIsRuntimePrivate | MetadataFlags.MetadataKindIsNonHeap,
    Metatype = 4 | MetadataFlags.MetadataKindIsRuntimePrivate | MetadataFlags.MetadataKindIsNonHeap,
    ObjCClassWrapper = 5 | MetadataFlags.MetadataKindIsRuntimePrivate | MetadataFlags.MetadataKindIsNonHeap,
    ExistentialMetatype = 6 | MetadataFlags.MetadataKindIsRuntimePrivate | MetadataFlags.MetadataKindIsNonHeap,
    HeapLocalVariable = 0 | MetadataFlags.MetadataKindIsNonType,
    HeapGenericLocalVariable = 0 | MetadataFlags.MetadataKindIsNonType | MetadataFlags.MetadataKindIsRuntimePrivate,
    ErrorObject = 1 | MetadataFlags.MetadataKindIsNonType | MetadataFlags.MetadataKindIsRuntimePrivate,
    Class = 0x800 // not really, but it's reasonable under the circumstances
                // This number looks arbitrary, but the swift source says that they're never going to
                // go over 0x7ff for predefined/enumerated metadata kind values
}

public record struct TypeMetadata
{
    const nint kMaxDiscriminator = 0x7ff;
    NativeHandle handle;

    internal TypeMetadata (NativeHandle handle) // internal to help prevent bogus handles
    {
        this.handle = handle;
    }

    public NativeHandle Handle {
        get { return handle; }
    }

    public TypeMetatypeKind {
        get { return TypeMetadataKindFromHandle (handle); }
    }

    public static bool TryGetTypeMetadata (Type type, [NotNullWhen (true)] out TypeMetadata? result)
    {
        // implementation tbd
    }

    public static bool TryGetTypeMetadata<T> (T t, [NotNullWhen (true)] out TypeMetadata? result)
    {
        return TryGetTypeMetadata (typeof (t), out result);
    }

    static TypeMetadataKind TypeMetadataKindFromHandle (NativeHandle h)
    {
        nint val = ReadNativeInt (h.Handle);
        if (val == 0)
            return TypeMetadataKind.None;
        if (val > kMaxDiscriminator)
            return TypeMetadataKind.Class;
        return (TypeMetadataKind)val;
    }
}
```

While not a complete implementation, this makes the core clear: this should be an immutable struct that can easily by passed by value to pinvokes and through reverse pinvokes.

In addition, there should be the following **public** methods at a minimum:

```csharp
public bool TryGetNominalTypeDescriptor ([NotNullWhen (true)] out NominalTypeDescriptor? result) { }
public string TypeName { get; } // uses the nominal type descriptor
// NB - while Swift's Optional<T> is declarad as a generic enum, its implementation is a special case
// since it is very common.
public bool IsGeneric { get; } // uses nominal type descriptor
public int GenericArgumentCount { get; }
```

In addition, we are likely to need **internal** accessors for:
- the class superclass `TypeMetadata`
- the number of elements in a tuple
- the `TypeMetadata` of each tuple element
- the number of parameters in a closure
- the `TypeMetadata` of each parameter
- the calling conventions of a closure
- whether or not the closure has a return value
- the `TypeMetadata` of the return value
- whether or not parameters have flags (this determines if a parameter is `inout`, variadic or autoclosure)

In addition, while we are not likely to need but is present:
- Protocol information (flags, number of witness tables, protocol descriptors for each, whether a protocol is ObjC)

## Mapping C# Type to TypeMetadata and Vice Versa

There should be a threadsafe 1 to 1 bi-map of Type <-> TypeMetadata to serve as cache.

For the Type -> TypeMetadata half, there should be a strategy mechanism that handles the following cases:
- primitive value types such as int, uint, short, ushort etc. NB: IntPtr is a synonym for nint and UIntPtr is a synonym for nuint
- bound Swift types should each have a static method for getting the type metadata using the metadata accessor which should be inspectable via reflection or interface compliance
- for tuples, there is a Swift library routine, `swift_getTupleTypeMetadata`
- for closures, there is a Swift library routine, `swift_getFunctionTypeMetadata`
- for existential containers, there is a Swift library routine, `swift_getExistentialTypeMetadata`
- for types with generics, there is a Swift library routine, `swift_getGenericMetadata` and `swift_getOpaqueTypeMetadata`

For the TypeMetadata -> Type half, this is a little more problematic. For nominal types, we can use the type name for looking up the matching C# type in a runtime type database, but it is entirely possible to encounter types that have no exposure in C# through binding. Under those circumstances, we may just be stuck with an approximation. This means that if we're presented with a class type that we've not seen, we would have to report it as either a proxy type if it's presented as implementing a particular protocol or as a broad placeholder with no real bindings to the original type. Under these circumstances, since the type mapping is no longer 1 to 1, these fallback cases should never go in the main cache, but could/should live in a secondary cache.

In terms of finding out if a C# type is capable of reporting Swift type metadata, I did benchmarking on several mechanisms and found that using static abstract interfaces is the most efficient predicate for an interface.

Given that, we can write something like this:
```csharp
// NB - the name in particular is not important just that
// this this interface must be implemented by all nominal types that are projected from Swift to C#
// So this could just as easily be named ISwiftNominalType
//
public interface ISwiftTyped {
    static abstract TypeMetadata GetMetadata();
}

// ...


// getter
internal static bool TryGetSwiftTypedMetadata (Type type, [NotNullWhen (true)] out TypeMetadata? result)
{
    if (typeof (ISwiftTyped).IsAssignableFrom (type)) {
        // assuming explicit interface implementation
        var mi = tt.GetMethod ("Swift.Runtime.ISwiftTyped.GetTypeMetadata", BindingFlags.NonPublic | BindingFlags.Static);
        result = (TypeMetadata)mi.Invoke (null, null);
        return true;
    } else {
        result = null;
        return false;
    }
}

//
public static bool TryGetTypeMetadata (Type type, [NotNullWhen (true)] out TypeMetadata? result)
{
    // look up in cache...
    // ...
    // other cases TBD
    // ...

    if (TryGetSwiftTypedMetadata (type, out result)) {
        // add result to cache
        // ...
        return true;
    }

    // ...
    return false;
}
```

In terms of typical implementations of the `ISwiftTyped` interface, we would have something like this:

```csharp
public class SomeBoundType : ISwiftTyped {
    static TypeMetadata? _metadata;
    static TypeMetadata ISwiftTyped.GetMetadata()
    {
        if (_metadata is null) {
            _metadata = SomeBoundTypePinvokes.MetadataAccessor (TypeMetadataRequest.Complete);
        }
        return _metadata;
    }
}

internal static class SomeBoundTypePinvokes {
    [DllImport(...)]
    public static TypeMetadata MetadataAccessor (MetadataRequest request);
}
```

Note that for generic types, the MetadataAcessor with have a `TypeMetadata` argument for each generic parameter.
An example of that might be:
```csharp
public class SomeBoundTypeGeneric<T> : ISwiftTyped {
    static TypeMetadata? _metadata;
    static TypeMetadata ISwiftTyped.GetMetadata()
    {
        if (_metadata is null) {
            if (TypeMetadata.TryGetTypeMetadata (typeof(T), out var tSwift)) {
                _metadata = SomeBoundTypeGenericPinvokes.MetadataAccessor (MetadataRequest.Complete, tSwift);
            } else {
                // exception TBD
                throw new Exception ($"Unable to get Swift TypeMetadata for C# type {T.Name}");
            }
        }
        return _metadata;
    }
}

internal static class SomeBoundTypeGenericPinvokes {
    [DllImport(...)]
    public static TypeMetadata MetadataAccessor (MetadataRequest request, TypeMetadata t);
}
```
