# Binding generics

This document explores projections of Swift generics into C#.

1. [Generic functions](#generic-functions)
2. Generic types (TBD)
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
    TypeMetadata.TryGetTypeMetadata<T>(out var metadata);
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
    TypeMetadata.TryGetTypeMetadata<T>(out var metadata);
    nuint payloadSize = /* Extract size from metadata */;
    NativeHandle payload = new NativeHandle(NativeMemory.Alloc(payloadSize));

    try 
    {
        SwiftIndirectResult result = new SwiftIndirectResult(payload);
        
        NativeHandle dataPayload = Runtime.GetPayload(ref data);
        PInvoke_ReturnData(result, dataPayload, metadata!.Value);
        return Runtime.FromPayload<T>(payload);
    }
    finally
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
        _payload = new NativeHandle(NativeMemory.Alloc(PayloadSize));
        NativeMemory.Copy(payload, _payload, PayloadSize);
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
        return *(FrozenStruct*)payload;
    }
}
```

### API comments

The proposed API will only work with types known at compile time. In some cases it might be desirable that we are able to create the type in runtime (there is an example of this happening when using [PATs](runtime-nominal-type-descriptor.md)). We will have to consider those cases and investigate potential solutions - keeping in mind that the code should work with NativeAOT.

**Future considerations:**

The InitializeWithCopy should be used if we need to return a type to a swift caller, for example a closure, virtual method - anything done from a reverse invoke.

For heap allocated types (classes and actors), the pointer is not a buffer but is is and actual instance pointer. For getting that into C#, we need to have a global registry that has a map of NativeHandle -> GCHandle where the GCHandle is taken from the actual C# instance of the type. For a type that doesn't exist in the map, we use the C# type (if we have it) or use the TypeMetadata to identify the C# type and run a flavor of constructor that takes a NativeHandle to initialize the type. As part of the memory management, we take both a strong and weak reference to the Swift handle. If the C# type is disposed, we inform the registry and it removes it from the cache as well as dropping the references. In BTfS this is in the class SwiftObjectRegistry. The strong reference is taken in the object and the weak is taken in the registry.
