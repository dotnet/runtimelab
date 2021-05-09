# Support for Marshalling `(ReadOnly)Span<T>`

As part of the exit criteria for the DllImportGenerator experiment, we have decided to introduce support for marshalling `System.Span<T>` and `System.ReadOnlySpan<T>` into the DllImportGenerator-generated stubs. This document describes design decisions made during the implementation of these marshallers.

## Design 1: "Intrinsic" support for `(ReadOnly)Span<T>`

In this design, the default support for `(ReadOnly)Span<T>` is emitted into the marshalling stub directly and builds on the pattern we enabled for arrays.

### Default behavior

This section describes the behavior of the default emitted-in-stub marshallers.

By default, we will marshal `Span<T>` and `ReadOnlySpan<T>` similarly to array types. When possible, we will pin the `(ReadOnly)Span<T>`'s data and pass that down. When it is not possible to do so, we will try stack allocating scratch space for efficiency or allocate native memory with `Marshal.AllocCoTaskMem` for compatibility with arrays.

To support marshalling from native to managed, we will support the same `MarshalAsAttribute` properties that arrays support today.

When a `(ReadOnly)Span<T>` is marshalled from native to managed, we will allocate a managed array and copy the data from the native memory into the array.

### Empty spans

Since `(ReadOnly)Span<T>` does not have a way to distinguish from an empty collection and a `null` collection, we will provide a dummy pointer for zero-length/`null` spans to match the behavior of zero-length arrays. We will also provide an in-source marshaller that enables marshalling an empty span as `null`.

### Additional proposed in-source marshallers

As part of this design, we would also want to include some in-box marshallers that follow the design laid out in the [Struct Marshalling design doc](./StructMarshalling.md) to support some additional scenarios:

- A marshaler that marshals an empty span as `null`.
  - This marshaller would only support empty spans as it cannot correctly represent non-empty spans of non-blittable types.
- A marshaler that marshals out a pointer to the native memory as a Span instead of copying the data into a managed array.
  - This marshaller would only support blittable spans by design.
  - This marshaler will require the user to manually release the memory. Since this will be an opt-in marshaler, this scenario is already advanced and that additional requirement should be understandable to users who use this marshaler.
  - Since there is no mechansim to provide a collection length, the question of how to provide the span's length in this case is still unresolved. One option would be to always provide a length 1 span and require the user to create a new span with the correct size, but that feels like a bad design.

### Pros/Cons of Design 1

Pros:

- This design builds on the array support that already exists, providing implementation experience and a slightly easier implementation.
- As we use the same MarshalAs attributes that already support arrays, developers can easily migrate their usage of array parameters in source-generated P/Invokes to use the span types with minimal hassle.

Cons:

- Defining custom marshalers for non-empty spans of non-blittable types generically is impossible since the marshalling rules of the element's type cannot be known.
- Custom non-default marshalling of the span element types is impossible for non-built-in types.
- Inlining the span marshalling fully into the stub increases on-disk IL size.
- This design does not enable developers to easily define custom marshalling support for their own collection types, which may be desireable.
- The MarshalAs attributes will continue to fail to work on spans used in non-source-generated DllImports, so this would be the first instance of enabling the "old" MarshalAs model on a new type in the generated DllImports, which may or may not be undesirable.
  - The existing "native type marshalling" support cannot support marshalling collections of an unknown (at marshaller authoring time) non-blittable element type and cannot specify an element count for collections during unmarshalling.

## Design 2: "Out-line" default support with extensions to Native Type Marshalling

<!--
    Idea: Extend the StructMarshalling proposal with a design for a generic collection-based marshalling pattern. That will enable outlining the Span/ReadOnlySpan/array/etc. marshallers while enabling developers to enable marshalling any collection type they want without us adding types to the generator.
-->

An alternative option to fully inlining the stub would be to extend the model described in the [Struct Marshalling design doc](./StructMarshalling.md) to have custom support for collection-like types. By extending the model to be built with generic collection types in mind, many of the cons of the first approach would be resolved.

Span marshalling would still be implemented with similar semantics as mentioned above in the Empty Spans section.Additional marshallers would still be provided as mentioned in the Additional proposed in-source marshallers section, but the `null` span marshaller would be able to be used in all cases, not just for empty spans.

### Proposed extension to the custom type marshalling design

Introduce a new attribute named `GenericCollectionMarshallerAttribute`. This attribute would have the following shape:

```csharp
namespace System.Runtime.InteropServices
{ 
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
    public sealed class GenericCollectionMarshallerAttribute : Attribute
    {
        public GenericCollectionMarshallerAttribute();
    }
}
```

The attribute would be used with a collection type like `Span<T>` as follows:

```csharp
[NativeTypeMarshalling(typeof(DefaultSpanMarshaler<>))]
public ref struct Span<T>
{
  ...
}

[GenericCollectionMarshaller]
public ref struct DefaultSpanMarshaler<T>
{

}
```

The `GenericCollectionMarshallerAttribute` attribute is applied to a generic marshaler type with the "collection marshaller" shape described below. Since generic parameters cannot be used in attributes, open generic types will be permitted in the `NativeTypeMarshallingAttribute` constructor as long as they have the same arity as the type the attribute is applied to and generic parameters provided to the applied-to type can also be used to construct the type passed as a parameter.

#### Generic collection marshaller shape

A generic collection marshaller would be required to have the following shape, in addition to the requirements for marshaler types used with the `NativeTypeMarshalllingAttribute`, excluding the constructors.

```csharp
[GenericCollectionMarshaller]
public struct GenericCollectionMarshallerImpl<T, U, V,...>
{
    // these constructors are required if marshalling from managed to native is supported.
    public GenericCollectionMarshallerImpl(GenericCollection<T, U, V, ...> collection, int nativeSizeOfElements);
    public GenericCollectionMarshallerImpl(GenericCollection<T, U, V, ...> collection, Span<byte> stackSpace, int nativeSizeOfElements); // optional
    
    public const int StackBufferSize = /* */; // required if the span-based constructor is supplied.

    // These method is required if marshalling from managed to native is supported.
    public TCollectionElement GetManagedValueAtIndex(int i);
    // This method is required if marshalling from native to managed is supported.
    public void SetManagedValueAtIndex(int i, TCollectionElement value);

    // The getter is required if marshalling from managed to native is supported.
    // The setter is required if marshalling from native to managed is supported.
    public int Count { get; set; }

    // The requirements on the Value property are the same as when used with `NativeTypeMarshallingAttribute`.
    // The property is required with the generic collection marshalling.
    public TNative Value { get; set; }

    public ref byte GetOffsetForNativeValueAtIndex(int index);
}
```

The constructors now require an additional `int` parameter specifying the native size of the collection elements, represented as `TCollectionElement` above. As the elements may be marshalled to types with different native sizes than managed, this enables the author of the generic collection marshaller to not need to know how to marshal the elements of the collection, just the collection structure itself.

The `GetManagedValueAtIndex` method and `Count` getter are used in the process of marshalling from managed to native. The generated code will iterate through `Count` elements (retrieved through `GetManagedValueAtIndex`) and assign their marshalled result to the address represented by `GetOffsetForNativeValueAtIndex` called with the same index. Then either the `Value` property getter will be called or the marshaller's `GetPinnableReference` method will be called, depending on if pinning is supported in the current scenario.

The `SetManagedValueAtIndex` method and the `Count` setter are used in the process of marshalling from native to managed. The `Count` property will be set to the number of elements that the native collection contains, and the `Value` property will be assigned the result value from native code. Then the stub will iterate through the native collection `Count` times, calling `GetOffsetForNativeValueAtIndex` to get the offset of the native value and calling `SetManagedValueAtIndex` to set the unmarshalled managed value at that index.

This design enables marshalling generic collections without requiring the owner of the collection type to know how to marshal every element in the collection.

#### Providing additional data for collection marshalling

As part of collection marshalling, there needs to be a mechanism for the user to tell the stub code generator how many elements are in the native collection when unmarshalling. For parity with the previous system, there also needs to be a mechanism to describe how to marshal the elements of the collection. This proposal adds the following members to the `MarshalUsingAttribute` attribute to enable this and other features:

```diff

- [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.Field)]
+ [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.ReturnValue | AttributeTargets.Field, AllowMultiple=true)]
public class MarshalUsingAttribute : Attribute
{
+    public MarshalUsingAttribute() {}
     public MarshalUsingAttribute(Type nativeType) {}
+    public string CountParameterName { get; set; }
+    public int ConstantElementCount { get; set; }
+    public int ElementIndirectionLevel { get; set; }
+    public const string ReturnsCountValue = "return-value";
}
```

The `MarshalUsingAttribute` will now provide a `CountParameterName` property that will point to a parameter whose value will hold the number of native collection elements, or to the return value if the value of `CountParameterName` is `ReturnsCountValue`. The `ConstantElementCount` property allows users to provide a constant collection length.

> Open Question:
> Should combining `CountParameterName` and `ConstantElementCount` in the same attribute be allowed?
> With the `MarshalAs` marshalling, `SizeParamIndex` and `SizeConst` can be combined and the resulting size will be `paramValue(SizeParamIndex) + SizeConst`.

To support supplying information about collection element counts, a parameterless constructor is added to the `MarshalUsingAttribute` type. The default constructor specifies that the code generator should use the information in the attribute but use the default marshalling rules for the type.

The `ElementIndirectionLevel` property is added to support supplying marshalling info for element types in a collection. For example, if the user is passing a `List<List<Foo>>` from managed to native code, they could provide the following attributes to specify marshalling rules for the outer and inner lists and `Foo` separately:

```csharp
private static partial void Bar([MarshalUsing(CountParameterName = nameof(count)), MarshalUsing(ConstantElementCount = 10, ElementIndirectionLevel = 1), MarshalUsing(typeof(FooMarshaler), ElementIndirectionLevel = 2)] List<List<Foo>> foos, int count);
```

Multiple `MarshalUsing` attributes can only be supplied on the same parameter or return value if the `ElementIndirectionLevel` property is set to distinct values.

Alternatively, the `MarshalUsingAttribute` could provide a `Type ElementNativeType { get; set; }` property instead of an `ElementIndrectionLevel` property and support specifying the native type of the element of the collection this way. However, this design would block support for marshalling collections of collections.

#### Example: Using generic collection marshalling for spans

This design could be used to provide a default marshaller for spans and arrays. Below is an example simple marshaller for `Span<T>`. This design does not include all possible optimizations, such as stack allocation, for simpilicity of the example.

```csharp
[GenericCollectionMarshaller]
public ref struct SpanMarshaler<T>
{
    private Span<T> managedCollection;

    private int nativeElementSize;

    public SpanMarshaler(Span<T> collection, int nativeSizeOfElements)
    {
       managedCollection = collection;
       Value = Marshal.AllocCoTaskMem(collection.Length * nativeSizeOfElements);
       nativeElementSize = nativeSizeOfElements;
    }

    public T GetManagedValueAtIndex(int i) => managedCollection[i];
    public void SetManagedValueAtIndex(int i, T value) => managedCollection[i] = value;

    public int Count
    {
       get => managedCollection.Length;
       set
       {
           managedCollection = new T[value];
       }
    }

    public IntPtr Value { get; set; }

    public unsafe ref byte GetOffsetForNativeValueAtIndex(int index) => ref *(byte*)(Value + index * nativeElementSize);

    public Span<T> ToManaged() => managedCollection;

    public void FreeNative()
    {
      if (Value != IntPtr.Zero)
      {
          Marshal.FreeCoTaskMem(Value);
      }
    }
}
```

This design could also be applied to support the built-in array marshalling if it is desired to move that marshalling out of the stub and into shared code.

#### Possible extension to the above model: Optimized support for sequential collections of blittable types

If both the managed and native representation of a collection is sequential and the contents are blittable, additional optimizations, such as optimized block copying, can be emitted. however the above design does not support the necessary APIs to enable these optimizations. This section proposes that the following members be added to the design above. A `IsSequentialCollection` boolean property should be added to the `GenericCollectionMarshaller` attribute to specify that the collection is sequential in both managed and native representations. Then an additional method should be added to the generic collection model:

```csharp
public ref TCollectionElement GetOffsetForManagedValueAtIndex(int index);
```

This function would be required when `IsSequentialCollection` is `true` and would replace the `Get/SetManagedValueAtIndex` methods. When the elements of the collection are blittable, the marshaller will emit a block copy of the range `MemoryMarshal.CreateSpan(GetOffsetForManagedValueAtIndex(0), Count)` to the destination `MemoryMarshal.CreateSpan(MemoryMarshal.Cast<byte, TCollectionElement>(GetOffsetForNativeValueAtIndex(0), Count))`.

When `TCollectionElement` is not blittable, the marshaller will iterate from `0..Count-1` and marshal the elements individually, as specified in the process above when the `Get/SetManagedValueAtIndex` methods are present.

This would enable similar performance metrics as the current support for arrays as well as Design 1's support for the span types when the element type is blittable.

### Pros/Cons of Design 2

Pros:

- Collection type owners do not need to know how to marshal the elements of the collection.
- Custom non-default marshalling of collections of non-blittable types supported with the same code as blittable types.
- Sharing code for marshalling a given collection type reduces IL size on disk.
- Developers can easily enable marshalling their own collection types without needing to modify the source generator.
- Makes no assumptions about native collection layout, so collections like linked lists can be easily supported.

Cons:

- Introduces more attribute types into the BCL.
- Introduces more complexity in the marshalling type model.
  - It may be worth describing the required members (other than constructors) in interfaces just to simplify the mental load of which members are required for which scenarios.
    - A set of interfaces (one for managed-to-native members, one for native-to-managed members, and one for the sequential-specific members) could replace the `GenericCollectionMarshaller` attribute.
