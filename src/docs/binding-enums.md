# Binding Enums

Swift enums are a hybrid value type between enumerations (names associated with cardinal values) and discriminated unions. Swift enums can be broken down into three categories:
- Trivial - these are enums that have a integral raw type. These can be nearly fully represented by C# enums
- Homogeneous - these are enums where the payloads are the same non-integral type
- Heterogeneous - there are enums where the payloads have at least two different types

Anything but trivial enums present problems when binding into C# because C# simply doesn't have discriminated unions.

## Language Parity Mismatches

Enums in swift can have instance methods including constructors.
Enum type names and case names can contain unicode characters unavailable to C#.
Enum backing type is limited to any type that implements `RawRepresentable` but that means that you can have...weird types as the backing value since Swift allows all manner of retroactive modeling.

## ABI Differences

None since this doesn't really exist in C#.

## Runtime Differences

None since this doesn't really exist in C#.

## Idiomatic differences

Since the notion of methods on an enum is not common in C#, it will feel weird, but for trivial enums, extension methods are not a bad way to go.

## Accessibility

This section covers homogeneous and heterogeneous enums.

This is the hard part.

Discriminated Unions do not exist in C#. The straightforward way to do this is reduce the union to operations that we care about:

1. Construction
2. Discriminator Access
3. Payload Access

It makes sense to have an outboard C# enum that defines the possible cases and then a type to host the enum.

In Binding tools for Swift, the type was implemented with a class implementing `IDisposable` with an opaque payload. This was done for the same reason as [structs](binding-structs.md), and because of that we need take into consideration whether or not the payloads are non-blittable.

We should not, under any circumstances, try to synthesize Swift enums ourselves. The memory layout of enums is not only opaque, it is fragile and the tools available in the value witness table are undocumented.

Fortunately, writing a wrapper to get a predictable result in Swift is straightforward:
```swift
// given:
public enum SomeEnum {
    case intVal(Int)
    case floatVal(Float)
    case stringVal(String)
}
// we can write this:

public func _SomeEnumCase(p: UnsafePointer<SomeEnum>) -> Int {
    switch p.pointee {
    case .intVal:
        return 0
    case .floatVal:
        return 1
    case .stringVal:
        return 2
    }
}
```

Getting a particular payload is straightforward, but also involves writing some Swift code:
```swift
public enum SomeRuntimeError : Error { // this would be a more general type
    case failure(message: String)
}

public func _SomeEnumPayloadIntVal (p: UnsafePointer<SomeEnum>) throws -> Int
{
    if case let .intVal(val) = p.pointee {
        return val
    }
    throw SomeRuntimeError.failure (message: "enum failure")
}

public func _SomeEnumNewIntVal(p: UnsafeMutablePointer<SomeEnum>, nint val)
{
    p.pointee = .intVal(val)
}

// repeat for each payload
```
With these two pieces, it's possible to treat Swift in a way that approximates discriminated union in C#:

```csharp
public enum SomeEnumCase {
    IntVal = 0
    FloatVal,
    StringVal,
}

public class SomeEnum : SwiftNativeValueType {
    byte[] payload;
    SomeEnum()
    {
        payload = new byte[Marshal.SwiftSizeof(this)];
    }
    public static SomeEnum NewIntVal(nint val) {
        var e = new SomeEnum();
        unsafe {
            fixed (byte *p = &payload) {
                _SomeEnumNewIntVal();
            }
        }
    }
    public nint IntVal {
        get {
            if (Case != SomeEnumCase.IntVal)
                throw new ArgumentException(); // or whatever
            unsafe {
                fixed (byte *p = &payload) {
                    return _SomeEnumPayloadIntVal(p);
                }
            }
        }
    }
    public static SomeEnum NewFloatVal(nfloat val) {
        // etc
    }
    public nfloat FloatVal {
        get {
            // etc
        }
    }
    public static SomeEnum NewStringVal(SwiftString val) {
        // etc
    }
    public SwiftString StringVal {
        get {
            // etc
        }
    }

    public SomeEnumCase Case {
        get {
            unsafe {
                unsafe {
                    fixed (byte *p = &payload) {
                        return _SomeEnumCase(p);
                    }
                }
            }
        }
    }
    protected void DisposeUnmanagedResources () { // called from Dispose()
        unsafe {
            StructMarshal.NominalDestroy(this);
        }
    }
}
```

There are a number of ways that we can improve the experience somewhat. For example, we can define a common interface for
all enums:
```csharp
public interface ISwiftEnum<T> where T : System.Enum {
    T Case { get; }
}
```
Which at the very least couples the cases and the enum.

I've been in contact with the C# team to encourage that whatever form unions take we can hook this into as cleanly as possible. To this end, I feel it's important to have an approach which is "fast track/abstract" which means that there will be a native C# implementation that works with C# syntax in a way that's performant, but also a way to make a non-native implementation operate with the supporting C# syntax. This precedent already exists in C# in `foreach`: if you use `foreach` on an array you get a fast-track implementation. If you use `foreach` on an `IEnumerable<T>` you get a functional, but less efficient version, but from the user's point of view it operates the same.

One way to achieve that is to mark the enum implementation with attributes to inform the compiler of its purpose:

```csharp
[UnionImplementation(typeof(SomeEnumCase))]
public class SomeEnum : {
    [UnionFactory(SomeEnumCase.IntVal)]
    public static SomeEnum NewIntVal(nint val) {
        // ...
    }
    [UnionPayload(SomeEnumCase.IntVal)]
    public nint IntVal {
        get {
            // ...
        }
    }
    // etc

    [UnionDiscriminator]
    public static EnumCase Case {
        get {
            // ...
        }
    }   
}
```
This is just one possible way of doing this, but the process is the same: statically advertise the cases and methods to get the case, the payload and factory methods to create them. There is a C# proposal for something similar [here](https://github.com/dotnet/csharplang/blob/main/proposals/TypeUnions.md#custom-unions).