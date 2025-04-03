# Binding generics

This document explores projections of Swift generics into C#.

1. [Generic functions](#generic-functions)
2. [Generic types](#generic-types)
3. [Generics with protocol constraints](#protocol-constraints)
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
public static extern void PInvoke_ReturnData(SwiftIndirectResult result, SwiftHandle data, TypeMetadata metadata);

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
public SwiftHandle GetPayload();
public static abstract object FromPayload(SwiftHandle payload);
```

Using these abstractions, the projection becomes:

```csharp
[DllImport(Path, EntryPoint = "...")]
[UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
public static extern void PInvoke_ReturnData(SwiftIndirectResult result, SwiftHandle data, TypeMetadata metadata);

public static unsafe T ReturnData<T>(T data)
{
    var metadata = GetTypeMetadataOrThrow<T>();
    nuint payloadSize = /* Extract size from metadata */;
    SwiftHandle payload = new SwiftHandle(NativeMemory.Alloc(payloadSize));

    try 
    {
        SwiftIndirectResult result = new SwiftIndirectResult(payload);
        
        SwiftHandle dataPayload = Runtime.GetPayload(ref data);
        PInvoke_ReturnData(result, dataPayload, metadata);
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
- **Classes**: In this example we expect that calling `ConstructFromSwiftHandle` will copy the necessary data so that the function can free all the resources it allocated.

### New API implementation

```csharp
public interface ISwiftObject // Could be also INominalType, naming
{
    public static abstract TypeMetadata Metadata { get; }

    public SwiftHandle GetPayload();

    public static abstract object FromPayload(SwiftHandle payload);
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

public static SwiftHandle GetPayload<T>(ref T type)
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

public static T FromPayload<T>(SwiftHandle payload)
{   
    // Logic for other types

    if (typeof(ISwiftObject).IsAssignableFrom(typeof(T)))
    {
        var helperType = typeof(Helper<>).MakeGenericType(typeof(T));
        return (T)helperType.GetMethod("FromPayload")!.Invoke(null, new object[] { (SwiftHandle)payload })!;
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

    public static T FromPayload(SwiftHandle payload)
    {
        return (T)T.FromPayload(payload);
    }
}
```

```csharp
class NonFrozenStruct : ISwiftObject
{
    private static nuint PayloadSize = /* Extract size from metadata */;

    private SwiftHandle _payload;

    // Other members

    public static TypeMetadata Metadata => PInvokeMetadataForNonFrozenStruct();

    public SwiftHandle GetPayload() => _payload;

    public static object FromPayload(SwiftHandle payload)
    {
        return new NonFrozenStruct(payload);
    }

    private unsafe NonFrozenStruct(SwiftHandle payload) 
    { 
        _payload = payload;
    }
}

[StructLayout(LayoutKind.Sequential, Size = /* Extract size */)]
struct FrozenStruct : ISwiftObject
{
    // Other members

    public static TypeMetadata Metadata => PInvokeMetadataForFrozenStruct();

    public unsafe SwiftHandle GetPayload()
    {
        return new SwiftHandle(Unsafe.AsPointer(ref this));
    }

    public static unsafe object FromPayload(SwiftHandle payload)
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

For heap allocated types (classes and actors), the pointer is not a buffer but is is and actual instance pointer. For getting that into C#, we need to have a global registry that has a map of SwiftHandle -> GCHandle where the GCHandle is taken from the actual C# instance of the type. For a type that doesn't exist in the map, we use the C# type (if we have it) or use the TypeMetadata to identify the C# type and run a flavor of constructor that takes a SwiftHandle to initialize the type. As part of the memory management, we take both a strong and weak reference to the Swift handle. If the C# type is disposed, we inform the registry and it removes it from the cache as well as dropping the references. In BTfS this is in the class SwiftObjectRegistry. The strong reference is taken in the object and the weak is taken in the registry.

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
    private SwiftHandle _payload;

    public unsafe Pair(T first, U second)
    {
        _payload = new SwiftHandle(NativeMemory.Alloc(PayloadSize));

        SwiftIndirectResult swiftIndirectResult = new SwiftIndirectResult(_payload);

        var firstMetadata = GetTypeMetadataOrThrow<T>();
        var secondMetadata = GetTypeMetadataOrThrow<U>();

        var SwiftHandleFirst = Runtime.GetPayload(ref first);
        var SwiftHandleSecond = Runtime.GetPayload(ref second);

        PairPInvokes.Pair(swiftIndirectResult, SwiftHandleFirst, SwiftHandleSecond, firstMetadata, secondMetadata);
    }

    public static TypeMetadata Metadata
    {
        /* This should be cached */
        get
        {
            var firstMetadata = GetTypeMetadataOrThrow<T>();
            var secondMetadata = GetTypeMetadataOrThrow<U>();

            return PairPInvokes.PInvokeMetadata(TypeMetadataRequest.Complete, firstMetadata, secondMetadata);
        }
    }

    public static object FromPayload(SwiftHandle payload)
    {
        return new Pair<T, U>(payload);
    }

    public SwiftHandle GetPayload() => _payload;

    public unsafe void ChangeFirst(T newFirst)
    {
        SwiftSelf self = new SwiftSelf(_payload);
        PairPInvokes.ChangeFirst(Runtime.GetPayload(ref newFirst), Metadata, self);
    }

    private unsafe Pair(SwiftHandle payload)
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

## Protocol constraints

This section describes how to project protocol constraints on generic type parameters from Swift to C\#. The goal here is not to describe full projections of protocols; for that, please refer to the [binding protocols doc](binding-protcols.md).

A Swift protocol can be mapped to C\# as an interface. Then Swift generic parameters with protocol constraint can be mapped into C\# as generic parameters with interface constraint. This should work for both functions and types.

### Protocol constraints on functions

Consider the following simple example in Swift:

```swift
public protocol Printable {
 func printMe()
}

public func printPrintable<T: Printable>(data: T) {
 data.printMe()
}

// Equivalent to: public func printPrintable<T>(data: T) where T : Printable
```

When a function is declared with a generic argument, an extra implicit argument is added to the end representing the type metadata of the generic argument (see above). If that generic argument is constrained by one or more protocols, the type metadata is followed by a pointer to the protocol witness table for each protocol. In the case of multiple protocols, e.g. T: P3 & P1 & P2, the protocol witness tables are ordered lexically by fully qualified name of the protocol. For detailed description of Metadata accessor refer to [runtime metadata doc](runtime-metadata.md).

This can be projected into C\# as follows:

```csharp
[SwiftProtocol("ModuleA", "Printable")] // New attribute which encapsulates information necessary to extract Protocol Conformance Descriptor
public interface IPrintableProtocol
{
    public void PrintMe();

}

public static void PrintPrintable<T>(T data) where T : ISwiftObject, IPrintableProtocol
{
    var metadata = TypeMetadata.GetTypeMetadataOrThrow<T>();
    var protocolWitnessTable = ProtocolWitnessTable.GetProtocolWitnessTableOrThrow<T, IPrintableProtocol>();
    PrintPrintable(data.GetPayload(), metadata, protocolWitnessTable);
}

[DllImport(Path, EntryPoint = "...")]
[UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
private static extern void PInvokePrintPrintable(SwiftHandle data, TypeMetadata metadata, ProtocolWitnessTable protocolWitnessTable);
```

This utilizes a new type `ProtocolWitnessTable` whose implementation might look something like:

```csharp
public record struct ProtocolWitnessTable
{
    SwiftHandle handle;

    internal ProtocolWitnessTable(SwiftHandle handle)
    {
        this.handle = handle;
    }

    public SwiftHandle Handle
    {
        get { return handle; }
    }

    public static ProtocolWitnessTable GetProtocolWitnessTable<T, U>() where T : ISwiftObject
    {
        var metadata = TypeMetadata.GetTypeMetadataOrThrow<T>();
        var conformanceDescriptor = T.GetProtocolConformanceDescriptor<U>();
        return new ProtocolWitnessTable(Runtime.GetProtocolWitnessTable(conformanceDescriptor, metadata));
    }
}
```

This would require adding a new method to `ISwiftObject`.

```csharp
public interface ISwiftObject
    public static abstract SwiftHandle GetProtocolConformanceDescriptor<T>();
```

**NOTE:** Here an assumption is made -- we will only need PWTs of things which implement the `ISwiftObject` interface.

Then a type implementing the interface could look like:

```csharp
[StructLayout(LayoutKind.Sequential, Size = /* Extract size */)]
struct FrozenStruct : ISwiftObject, IPrintableProtocol
{
    // Other members

    public static SwiftHandle GetProtocolConformanceDescriptor<T>()
    {
        TypeInfo typeInfo = typeof(T).GetTypeInfo();

        var protocolAttribute = typeInfo.GetCustomAttribute<SwiftProtocolAttribute>();
        if (protocolAttribute == null)
        {
            throw new InvalidOperationException($"Type {typeof(T).Name} does not have a SwiftProtocol attribute");
        }

        // Use information from protocolAttribute to correctly extract the conformance descriptor
        return GetProtocolConformanceDescriptor(...);
    }

    public void PrintMe()
    {
        var self = new SwiftSelf<FrozenStruct>(this);
        PInvokePrintMe(self);
    }
}
```

### Protocol constraints on types

Adding constraints on a type's generic parameters is also straightforward, with small adjustments necessary to the initializer and metadata accessor functions. Let's revisit the `Pair` example:

```swift
public struct Pair<T: Printable, U: Printable> { 
    public var first: T 
    public var second: U
 
    public init(first: T, second: U) { 
        self.first = first 
        self.second = second 
    }

    public func printMe() {
        self.first.printMe()
        self.second.printMe()
    }
}
```

The corresponding C\# projection would be:

```csharp
class Pair<T, U> : ISwiftObject
    where T : ISwiftObject, IPrintableProtocol
    where U : ISwiftObject, IPrintableProtocol
{

    private static nuint PayloadSize =  /* Extract size */;
    private SwiftHandle _payload;

    public unsafe Pair(T first, U second)
    {
        _payload = new SwiftHandle(NativeMemory.Alloc(PayloadSize));

        SwiftIndirectResult swiftIndirectResult = new SwiftIndirectResult(_payload);

        var firstMetadata = GetTypeMetadataOrThrow<T>();
        var secondMetadata = GetTypeMetadataOrThrow<U>();

        var SwiftHandleFirst = Runtime.GetPayload(ref first);
        var SwiftHandleSecond = Runtime.GetPayload(ref second);

        var printableProtocolWitnessTableT = ProtocolWitnessTable.GetProtocolWitnessTable<T, IPrintableProtocol>();
        var printableProtocolWitnessTableU = ProtocolWitnessTable.GetProtocolWitnessTable<U, IPrintableProtocol>();

        PairPInvokes.Pair(swiftIndirectResult, SwiftHandleFirst, SwiftHandleSecond, firstMetadata, secondMetadata, printableProtocolWitnessTableT, printableProtocolWitnessTableU);
    }

    public static TypeMetadata Metadata
    {
        /* This should be cached */
        get
        {
            var firstMetadata = GetTypeMetadataOrThrow<T>();
            var secondMetadata = GetTypeMetadataOrThrow<U>();

            var printableProtocolWitnessTableT = ProtocolWitnessTable.GetProtocolWitnessTable<T, IPrintableProtocol>();
            var printableProtocolWitnessTableU = ProtocolWitnessTable.GetProtocolWitnessTable<U, IPrintableProtocol>();

            // Might be handled differently due to the metadata lowering https://github.com/dotnet/runtimelab/pull/2810

            return PairPInvokes.PInvokeMetadata(TypeMetadataRequest.Complete, firstMetadata, secondMetadata, printableProtocolWitnessTableT, printableProtocolWitnessTableU);
        }
    }

    // Other members
}
```

### Supporting generic constraints on types present in both C\# and Swift

When projecting a Swift type into C\#, we can project all of the protocols it conforms to as interfaces and make the C\# equivalent implement those interfaces. This allows it to work with the generic constraints mechanism described above.

However, types that exist in both languages require special handling—for example, primitive number types. In Swift, primitive number types conform to multiple protocols that their C\# counterparts do not.

Consider the following Swift code:

```swift
public func acceptHashable<T: Hashable>(T data) {}
acceptHashable(data: 3)
```

This would map into c# as described above into:

```csharp
public interface IHashable
{
    public abstract static SwiftHandle GetIHashableProtocolWitnessTable();
}

public static void AcceptHashable<T>(T data) where T : ISwiftObject, IHashable
{
    var metadata = TypeMetadata.GetTypeMetadataOrThrow<T>();
    var protocolWitnessTable = ProtocolWitnessTable.GetProtocolWitnessTable<T, IHashable>();
    PInvokeAcceptHashable(data.GetPayload(), metadata, protocolWitnessTable);
}

[DllImport(Path, EntryPoint = "...")]
[UnmanagedCallConv(CallConvs = [typeof(CallConvSwift)])]
private static extern void PInvokeAcceptHashable(SwiftHandle data, TypeMetadata metadata, ProtocolWitnessTable protocolWitnessTable);
```

We would be able to call `AcceptHashable<T>` using a type that implements both `ISwiftObject` and `IHashable`, but we would not be able to call it using, for example, `System.Int64`.

One way to solve this problem is by creating a wrapper struct for each of the types, which would implement the necessary interfaces:

```csharp
public struct SwiftIntWrapper : ISwiftObject, IHashable, ...
{
    nint value;

    // Implement required methods and properties
}
```

Before calling a function with generic constraints, the primitive value would have to be explicitly wrapped using these wrapper structs.

**NOTE:** This makes an assumption that binary representation of value of a frozen struct with a single `Int` field is the same as of `Int` itself.
