# Binding Tuples

Tuples in Swift are conceptually anonymous structs. Apple implies that in the documentation by saying that tuples follow the same packing rules as structs when they are stored in memory. However, this is absolutely not the case when tuples are being passed as arguments. For whatever reason, when passed as arguments tuples are flattened entirely to individual elements that are not tuples. In other words, the following two functions are identical from an ABI perspective:
```swift
public func foo(T: (a: Int, b: Bool, (c: SomeStruct, d: SomeClass))) { // func of 1 argument
    // ...
}

public func foo(a: Int, b: Bool, c: SomeStruct, d: SomeClass) { // func of 4 arguments
    // ...
}
```
It's unclear why Apple would do this except that it might be a vestige of when Swift had the ability to have multiple
argument lists and could do partial function application. It also might be because it enables very low-friction usage of tuples in that when used outside of the context of nominal types, tuples don't actually need to exist. They can be implemented as an artifical grouping of scoped variable names.

## Language Parity Mistmatch

Since Tuples became available in C# they have had two varieties: `System.Tuple` and `System.ValueTuple`. The latter is what has been adopted into C# as a first class citizen in the language and since it matches closely to Swift tuples, we should follow that.

One potential problem we need to investigate is that the C# `ValueTuple` is generic and the size of the struct can't be put into a `StructLayout` attribute. Furthermore, we need to know if we need to change `ValueTuple` to have `[StructLayout(LayoutKind.Sequential)]` added to the types. If not, it might be necessary to create our own type that can easily be converted back and forth to `ValueTuple`.

## ABI Mismatches

Because of the nature of function calls having different calling conventions for tuple arguments in Swift, we should flatten any tuple type from swift and expand it to different arguments for the corresponding pinvoke:

```swift
// Swift function
public func sumTuple(x: (a: Int, b: Int, c: Int)) -> Int {
    return x.a + x.b + x.c
}
```
```csharp
// csharp code
public nint SumTuple (x: (Int, Int, Int))
{
    return _sumTuple(x.Item1, x.Item2, x.Item3)
}

[UnmanagedCallConv(CallConvs = new Type[] { typeof(CallConvSwift) })]
[DllImport(/* ... */)]
static extern nint _sumTuple(xa: nint, xb: nint, xc: nint);
```
Of course care needs to be taken when doing this because in writing the pinvoke, we're changing the signature of the function which may create a pinvoke that conflicts in another existing pinvoke.

In returning tuples Swift follows the pattern for structs in that the return value is packed into up to 4 registers before requiring the caller to pass in a pointer to memory. The Swift runtime has an entrypoint which given a pointer to an array of type metadata pointers and a count will return the type metadata for that tuple. The actual arguments are more complicated than that, but nothing that we need to worry about. What is important to keep in mind is that we should **never** try to synthesize Swift type metadata for tuples (or for any other types, really). The Swift runtime has expectations that type metadata are singletons and will do comparisons on pointers for equiality and Swift caches type metadata objects. Therefore we should ensure that any type metadata that need at runtime should be gotten from Swift.

This accessor appears to be thread safe.

## Runtime Mismatches
Tuples that contain heap allocated types will create issues due to reference counting issues. This is the same as with Structs.

For calling generic functions that are being passed a tuple from C#, we will need to pass in the appropriate type metadata. Fortunately, this is relatively straightforward. 

## Idiomatic Differences
There are no idiomatic differences that we will need to worry about.

## Accessibility
Making it easy to copy tuples and manage the lifetime of reference counted types will be our biggest issue in terms of accessibility.

Copying tuples should be done using the [value witness table](binding-value-witness-table.md) taken from the tuple's type metadata.