# Binding generics

This document explores projections of Swift generics into C#.

1. [Generic functions](#generic-functions)
2. [Generic types](#generic-types)
3. Generics with protocol constraints (TBD)
4. Generics with PAT constraints (TBD)
5. Existential container for a protocol as a generic type (TBD)

## Generic functions

When a generic type is used in Swift, its memory layout isn't known at compile time. To manage this, the method parameter whose type is generic represented by an opaque pointer. To handle this:

- **Opaque Pointers**: Swift represents generic parameters as opaque pointers (`%swift.opaque`), abstracting the actual memory layout.
- **Type Metadata**: Swift includes a type metadata pointer (`%T`) as an implicit argument at the end of a function's signature. This metadata provides the runtime information needed to manage the generic type.
- **Indirect Return**: For returning generic types, Swift uses an [indirect return result](https://github.com/dotnet/runtime/issues/100543), where the caller provides a memory location for the return value.

Consider the following simple swift function:

```swift
func returnData<T>(data: T) -> T {
    return data
}
```

When compiled into LLVM-IR the above function has the following LLVM-IR signature:

```llvm
@"output.returnData<A>(data: A) -> A"(ptr noalias nocapture sret(%swift.opaque) %0, ptr noalias nocapture %1, ptr %T)
```

- `%0`: Indicates an **indirect return result**, where the caller allocates memory for the return value.
- `%1`: The `data` parameter, passed as a pointer.
- `%T`: The **type metadata** for `T`.

### Projecting into C\#

A sample projection into C# assuming that the type argument is projected as struct could look like this:

```csharp
[DllImport(Path, EntryPoint = "...")]
[UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
public static extern void PInvoke_ReturnData(SwiftIndirectResult result, NativeHandle data, TypeMetadata metadata);

public static unsafe T ReturnData<T>(T data) where T : unmanaged, ISwiftObject
{
    var metadata = GetTypeMetadataOrThrow<T>();
    nuint payloadSize = /* Extract size from metadata */;

    var payload = NativeMemory.Alloc(payloadSize);

    try 
    {
        var result = new SwiftIndirectResult(payload);
        PInvoke_ReturnData(result, &data, metadata);
        return *(T*)payload;
    }
    finally
    {
        NativeMemory.Free(payload);
    }
}
```

However, the projection has to support type arguments agnostic of whether they are projected as structs (frozen structs) or classes (non-frozen structs). It also has to support types which do not implement `ISwiftObject` like primitive number types. We cannot then use the constraints and will need to introduce abstractions for getting a native handle of an arbitrary Swift type and constructing a Swift type from such a handle.

Those could look something like

```csharp
public NativeHandle GetPayload();
public static abstract object FromPayload(NativeHandle payload);
```

Using these abstractions, the projection becomes:

```csharp
[DllImport(Path, EntryPoint = "...")]
[UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
public static extern void PInvoke_ReturnData(SwiftIndirectResult result, NativeHandle data, TypeMetadata metadata);

public static unsafe T ReturnData<T>(T data)
{
    var metadata = GetTypeMetadataOrThrow<T>();
    nuint payloadSize = /* Extract size from metadata */;
    NativeHandle payload = new NativeHandle(NativeMemory.Alloc(payloadSize));

    try 
    {
        SwiftIndirectResult result = new SwiftIndirectResult(payload);
        
        NativeHandle dataPayload = Runtime.GetPayload(ref data);
        PInvoke_ReturnData(result, dataPayload, metadata!.Value);
        return Runtime.FromPayload<T>(payload); // Transfers ownership of the payload
    }
    catch
    {
        NativeMemory.Free(payload);
    }
}
```

**Memory Management Considerations:**

- **Structs**: The CLR copies the data from unmanaged to managed memory. We must free the unmanaged memory to avoid leaks.
- **Classes**: In this example we expect that calling `ConstructFromNativeHandle` will copy the necessary data so that the function can free all the resources it allocated.

### New API implementation

```csharp
public interface ISwiftObject // Could be also INominalType, naming
{
    public static abstract TypeMetadata Metadata { get; }

    public NativeHandle GetPayload();

    public static abstract object FromPayload(NativeHandle payload);
}
```

The implementation of those new methods could look like follows:

```csharp
public static bool TryGetTypeMetadata<T>([NotNullWhen(true)] out TypeMetadata? result)
{
    if (typeof(ISwiftObject).IsAssignableFrom(typeof(T)))
    {
        var helperType = typeof(Helper<>).MakeGenericType(typeof(T));
        result = (TypeMetadata)helperType.GetMethod("GetTypeMetadata")!.Invoke(null, null);
        return true;
    }
    else
    {
        result = null;
        return false;
    }
}

public static TypeMetadata GetTypeMetadataOrThrow<T>()
{
    if (TryGetTypeMetadata<T>(out var result))
    {
        return result!;
    }

    throw new InvalidOperationException($"Could not obtain TypeMetadata for {typeof(T)}.");
}

public static NativeHandle GetPayload<T>(ref T type)
{
    // Logic for other types

    if (typeof(ISwiftObject).IsAssignableFrom(typeof(T)))
    {
        return ((ISwiftObject)type).GetPayload();
    }
    else
    {
        throw new NotSupportedException($"Type {typeof(T)} is not supported.");
    }
}

public static T FromPayload<T>(NativeHandle payload)
{   
    // Logic for other types

    if (typeof(ISwiftObject).IsAssignableFrom(typeof(T)))
    {
        var helperType = typeof(Helper<>).MakeGenericType(typeof(T));
        return (T)helperType.GetMethod("FromPayload")!.Invoke(null, new object[] { (NativeHandle)payload })!;
    }
    else
    {
        throw new NotSupportedException($"Type {typeof(T)} is not supported.");
    }
}
```

In order to preserve static interface methods during ILLink / NativeAOT we will use a Helper class

```csharp
public static class Helper<T> where T : ISwiftObject // We should have a better name
{
    public static TypeMetadata GetTypeMetadata()
    {
        return T.Metadata;
    }

    public static T FromPayload(NativeHandle payload)
    {
        return (T)T.FromPayload(payload);
    }
}
```

```csharp
class NonFrozenStruct : ISwiftObject
{
    private static nuint PayloadSize = /* Extract size from metadata */;

    private NativeHandle _payload;

    // Other members

    public static TypeMetadata Metadata => PInvokeMetadataForNonFrozenStruct();

    public NativeHandle GetPayload() => _payload;

    public static object FromPayload(NativeHandle payload)
    {
        return new NonFrozenStruct(payload);
    }

    private unsafe NonFrozenStruct(NativeHandle payload) 
    { 
        _payload = payload;
    }
}

[StructLayout(LayoutKind.Sequential, Size = /* Extract size */)]
struct FrozenStruct : ISwiftObject
{
    // Other members

    public static TypeMetadata Metadata => PInvokeMetadataForFrozenStruct();

    public unsafe NativeHandle GetPayload()
    {
        return new NativeHandle(Unsafe.AsPointer(ref this));
    }

    public static unsafe object FromPayload(NativeHandle payload)
    {
        var struct =  *(FrozenStruct*)payload;
        NativeMemory.Free(payload);
        return struct;
    }
}
```

### API comments

The proposed API will only work with types known at compile time. In some cases it might be desirable that we are able to create the type in runtime (there is an example of this happening when using [PATs](runtime-nominal-type-descriptor.md)). We will have to consider those cases and investigate potential solutions - keeping in mind that the code should work with NativeAOT.

**Future considerations:**

The InitializeWithCopy should be used if we need to return a type to a swift caller, for example a closure, virtual method - anything done from a reverse invoke.

For heap allocated types (classes and actors), the pointer is not a buffer but is is and actual instance pointer. For getting that into C#, we need to have a global registry that has a map of NativeHandle -> GCHandle where the GCHandle is taken from the actual C# instance of the type. For a type that doesn't exist in the map, we use the C# type (if we have it) or use the TypeMetadata to identify the C# type and run a flavor of constructor that takes a NativeHandle to initialize the type. As part of the memory management, we take both a strong and weak reference to the Swift handle. If the C# type is disposed, we inform the registry and it removes it from the cache as well as dropping the references. In BTfS this is in the class SwiftObjectRegistry. The strong reference is taken in the object and the weak is taken in the registry.

## Generic Types

This section describes how we might approach projecting generic swift types into C#.

### Generic structs

As the memory layout is not know at compile time the struct has to be projected as classes. **NOTE**: This holds true for both frozen and non-frozen generic structs, this will be explained in more detail later on.

Lets consider this simple example:

```swift
public struct Pair<T, U> { 
    public var first: T 
    public var second: U
 
    public init(first: T, second: U) { 
        self.first = first 
        self.second = second 
    }

    public mutating func changeFirst(newFirst : T) {
        self.first = newFirst
    }
}
```

The init method will behave just like a regular generic function returning generic type (see `ReturnData`), that means that we need to pass `SwiftIndirectResult`, pointer to data and metadata pointer.

The `changeFirst` method will require additional handling. Upon inspection we can see that when calling instance methods on generic types Swift expects to be handled type metadata for the struct (in addition to `newFirst` pointer and `SwiftSelf`).

Accounting for those two the projection could look something like:

```csharp
class Pair<T, U> : ISwiftObject
{

    private static nuint PayloadSize =  /* Extract size */;
    private NativeHandle _payload;

    public unsafe Pair(T first, U second)
    {
        _payload = new NativeHandle(NativeMemory.Alloc(PayloadSize));

        SwiftIndirectResult swiftIndirectResult = new SwiftIndirectResult(_payload);

        var firstMetadata = GetTypeMetadataOrThrow<T>();
        var secondMetadata = GetTypeMetadataOrThrow<U>();

        var nativeHandleFirst = Runtime.GetPayload(ref first);
        var nativeHandleSecond = Runtime.GetPayload(ref second);

        PairPInvokes.Pair(swiftIndirectResult, nativeHandleFirst, nativeHandleSecond, firstMetadata!.Value, secondMetadata!.Value);
    }

    public static TypeMetadata Metadata
    {
        /* This should be cached */
        get
        {
            var firstMetadata = GetTypeMetadataOrThrow<T>();
            var secondMetadata = GetTypeMetadataOrThrow<U>();

            return PairPInvokes.PInvokeMetadata(TypeMetadataRequest.Complete, firstMetadata!.Value, secondMetadata!.Value);
        }
    }

    public static object FromPayload(NativeHandle payload)
    {
        return new Pair<T, U>(payload);
    }

    public NativeHandle GetPayload() => _payload;

    public unsafe void ChangeFirst(T newFirst)
    {
        SwiftSelf self = new SwiftSelf(_payload);
        PairPInvokes.ChangeFirst(Runtime.GetPayload(ref newFirst), Metadata, self);
    }

    private unsafe Pair(NativeHandle payload)
    {
        _payload = payload;
    }
}
```

#### Frozen and non-frozen generic structs

The frozen keyword can be applied to a generic struct just like it can be applied to a regular struct. However because the generic parameters type (and hence size and layout) is unknown at the compile time, so is the size and the layout of the generic struct. Specifically, the generic parameter can be a frozen or a non-frozen struct.

If we consider the four cases:

1. Non-frozen struct and at least one non-frozen generic parameter
2. Non-frozen struct and (recursively) frozen generic parameters
3. Frozen struct and at least one non-frozen generic parameter
4. Frozen struct and (recursively) frozen generic parameters

The cases one to three are simple from the projection perspective. The instantiated generic will be passed using indirection (as a non-frozen struct would in previous examples).

The case four is problematic, as instances of a closed generic can be passed using swift lowering algorithm. Lets look into this more detail:

```swift
@frozen
public struct Pair<T, U> {...}

@frozen
public struct FrozenStruct {
    let a: Double
    let b : Double
}

public struct NonFrozenStruct {
    let a: Double
    let b : Double
}

// Case 3
public func withAtLeastOneNonFrozenParameter(data: Pair<FrozenStruct, NonFrozenStruct>) -> Pair<FrozenStruct, NonFrozenStruct> {
    return data;

// Case 4
public func withTwoFrozenParameters(data: Pair<FrozenStruct, FrozenStruct>) -> Pair<FrozenStruct, FrozenStruct> {
    return data;
}
}
```

The corresponding llvm-ir signatures of the above two functions are as follows (cases one and two would generate a signature very similar to the signature of case three).

```llvm
// Case 3
void @"output.withAtLeastOneNonFrozenParameter(data: output.Pair<output.FrozenStruct, output.NonFrozenStruct>) -> output.Pair<output.FrozenStruct, output.NonFrozenStruct>"(ptr noalias nocapture sret(%T6output13PairVyAA0D6StructVAA0cdE0VG) %0, ptr noalias nocapture dereferenceable(32) %1)

// Case 4
{ double, double, double, double } @"output.withTwoFrozenParameters(data: output.Pair<output.FrozenStruct, output.FrozenStruct>) -> output.Pair<output.FrozenStruct, output.FrozenStruct>"(double %0, double %1, double %2, double %3)
```

Case four passes the Pair using lowering algorithm.

From the user perspective we would like to have a function with the following signature:

```csharp
    public static Pair<FrozenStruct, FrozenStruct> WithTwoFrozenParameters(Pair<FrozenStruct, FrozenStruct> pair)
```

Which would then ideally call the following PInvoke.

```csharp
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static extern Pair<FrozenStruct, FrozenStruct> WithTwoFrozenParameters(Pair<FrozenStruct, FrozenStruct> pair);
```

This however would not work as `Pair<FrozenStruct, FrozenStruct>` is a class. Runtime would try to pass the value by reference (it does not even know the layout to begin with). We could solve this problem on the projection layer.

We could generate a helper struct per instance of frozen closed generic type. This could look something like:

```csharp
[StructLayout(LayoutKind.Sequential, Size = 16)]
public struct Pair_FrozenStruct_FrozenStruct
{
    public FrozenStruct first;
    public FrozenStruct second;
}
```

Then the PInvoke would use this helper struct instead of the generic in its signature.

```csharp
    [UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
    public static extern Pair_FrozenStruct_FrozenStruct WithTwoFrozenParameters(Pair_FrozenStruct_FrozenStruct pair);
```

And the user would be exposed the following wrapper:

```csharp
public static unsafe Pair<FrozenStruct, FrozenStruct> WithTwoFrozenParameters(Pair<FrozenStruct, FrozenStruct> pair)
{
    Pair_FrozenStruct_FrozenStruct structPair = *(Pair_FrozenStruct_FrozenStruct*)pair.GetPayload(); /* This would have to be revised for memory safety*/

    Pair_FrozenStruct_FrozenStruct newPair = WithTwoFrozenParameters(struct_pair);
    return new Pair<FrozenStruct, FrozenStruct>(newPair.first, newPair.second);
}
```

We could also consider solving this on the runtime level.

### Generic classes

TBD
