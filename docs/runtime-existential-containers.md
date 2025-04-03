# Existential Containers

In Swift when a protocol *without associated types* or a composition of protocols is passed as an argument into or returned from a function, the value gets passed in an Existential Container. This is a value type which serves as a box for any type that adopts the protocol or protocols needed. It is a [variable length struct](https://github.com/swiftlang/swift/blob/main/docs/ABI/TypeLayout.rst#existential-container-layout) of the form:

| Index | Contents       | Size in Machine Words |
|-------|----------------|-----------------------|
|   0   | Payload        | 3                     |
|   3   | Metadata       | 1                     |
|   4   | Witness Tables | variable: 0 or more   |

The start of the box includes 3 machine words which serve as a payload for the existential container. If the payload is 3 machine words or less, it will be copied into the existential container. If the payload is larger than 3 machine words, a copy will be allocated from the heap and the pointer to that copy will live in the payload.

After the payload is the type metadata of the entity in the payload.

Finally, there is an inline list of pointers to protocol witness tables. This list can be 0 elements in length as in the case of the special `Any` protocol or it can be n elements in length as in the case of a protocol composition type (`P1 & P2 & ... Pn`). In the case of an existential container with more than 1 protocol witness table, they are ordered alphabetically by the protocol name (note, this is from the source code - Apple has not documented this).

Because of the memory issues, existential containers need to be handled with attention paid to reference counting, but this can be managed by avoiding exposing the existential containers to the public API. This is straight-forward since in order to present protocols back and forth to each language will require dual proxies. This is detailed [here](binding-protocols.md).

To start, we want to be able to operate on existential containers in the abstract. Therefore all existential containers should implement a common interface:

```csharp
public interface IExistentialContainer {
    // These 3 elements hold the payload of the existential type
    IntPtr Payload0 { get; set; }
    IntPtr Payload1 { get; set; }
    IntPtr Payload2 { get; set; }
    // This is the type metadata of the object in the payload. The type metadata will determine
    // whether or not the payload contains the actual object or a heap-allocated copy. This is done by
    // looking at the value witness table, if it's a value type, which has a field for the type's size
    TypeMetadata ObjectMetadata { get; set; }
    // an indexer into each of the value witness tables
    SwiftHandle this [int index] { get; set; } // this[] and Count could just as easily be IEnumerable<SwiftHandle>
    // the number of value witness tables
    int Count { get; }
    // the size of this in bytes
    int SizeOf { get; }
    // Copy the existential container into memory, returns memory
    IntPtr CopyTo (IntPtr memory);
    // Copy the contents of this into the supplied container. Throws if container doesn't match the size of this
    void CopyTo <T>(ref T container) where T : IExistentialContainer;
}
```

From there, we need as many concrete implementations of `IExistentialContainer` as we see fit. By far the most common existential container is the variety with a single protocol witness table in it.

Given this, I created 9 structs representing existential containers with 0 to 8 inclusive witness table slots.
Since most of the code is boiler plate, I made static internal utility functions to copy existential containers to and from raw memory and to each other.

I also created methods to unbox payloads from existential containers and to box into a 0 witness table container i.e. `Any`.

There is also a second flavor of existential container which is a special case reserved for types constrained to `AnyObject` which has the following layout:

| Index | Contents       | Size in Machine Words |
|-------|----------------|-----------------------|
|   0   | Payload        | 1                     |
|   1   | Witness Tables | variable: 0 or more   |

This is noted in [Apple's documentation](https://github.com/swiftlang/swift/blob/main/docs/ABI/TypeLayout.rst#class-existential-containers). I have never encountered this in Apple's code nor in the wild. This is mostly due to Apple encouraging Swift to be a value type based language.

Although this would not be a priority for us, the type could be represented like this:

```csharp
public interface IClassExistentialContainer {
    SwiftHandle Object { get; set; }
    SwiftHandle this [int index] { get; set; } // this and count could just as easily by IEnumerable<SwiftHandle>
    int Count { get; }
    int Sizeof { get; }
    IntPtr CopyTo (IntPtr memory);
    void CopyTo <T>(ref T container) where T : IExistentialContainer;
}
```
