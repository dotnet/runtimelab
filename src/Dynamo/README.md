# Dynamo

Dynamo is code generator that uses C# combinators to write source code. The target language can be any language you wish. At present, it is built to support C# and Swift which are requirements for Binding Tools for Swift.

Dynamo was meant to be a step up from using `Console.WriteLine` and as such doesn't care about the order in which the code is being generated. This is important in Binding Tools for Swift in that it allows locality of responsibility. Consider the process of writing a binding for an enum from Swift. If we start with the following Swift:

```swift
public enum OtherOptional<T> {
    case none
    case some(T)
    case error(Error)
    
    public var hasError: Bool {
        get {
            switch (self) {
            case .error(_): return true
            default: return true
            }
        }
    }
}
```

We have to generate the following types in C#:
1 - an enum for the cases
2 - a class to represent the enum along with a payload
3 - a class for pinvokes

In C#, the binding will look something like this:
```csharp
public enum OtherOptionalCases {
    None,
    Some,
    Error,
}

public class OtherOptional<T> : IDisposable, ISwiftObject {
    byte [] _payload;
    OtherOptional<T>(byte[] payload) {
        _payload = payload;
    }
    public OtherOptionalCases Case {
        get {
            unsafe {
                fixed (byte * payloadPtr = &_payload) {
                    var typeMetadata = TypeMetadata.GetTypeMetadataOrThrow(typeof(T));
                    return OtherOptionalPinvokes.Case(payloadPtr, typeMetadata);
                }
            }
        }
    }
    
    public static OtherOptional<T> CreateNone () {
        var typeMetadata = TypeMetadata.GetTypeMetadataOrThrow(typeof(T));
        byte[] payload = new byte[TypeMetadata.SizeOf(this);
        OtherOptionalPinvokes.CreateNone(payload, typeMetadata);
        return new OtherOptional(payload);
    }
    
    public static OtherOptional<T> CreateSome(T value)
    {
        var typeMetadata = TypeMetadata.GetTypeMetadataOrThrow(typeof(T));
        byte[] payload = new byte[TypeMetadata.SizeOf(this);
        OtherOptionalPinvokes.CreateSome(payload, value, typeMetadata); // this is wrong, value needs to be runtime marshaled
        return new OtherOptional(payload);
    }
    
    public static OtherOptional<T> CreateError(SwiftError value)
    {
        var typeMetadata = TypeMetadata.GetTypeMetadataOrThrow(typeof(T));
        byte[] payload = new byte[TypeMetadata.SizeOf(this);
        OtherOptionalPinvokes.CreateNone(payload, value, typeMetadata);
        return new OtherOptional(payload);
    }
    
    public T SomeValue {
        get {
            if (Case != OtherOptionalCases.Some) throw new Exception ();
            unsafe {
                fixed (byte * payloadPtr = &_payload) {
                    var typeMetadata = TypeMetadata.GetTypeMetadataOrThrow(typeof(this));
                    return OtherOptionalPinvokes.SomeValue(payloadPtr, typeMetadata); // this is also wrong the return value needs to be marshaled
                }
            }
        }
    }
    
    public SwiftError {
        get {
            if (Case != OtherOptionalCases.Error) throw new Exception ();
            unsafe {
                fixed (byte *payloadPtr = &_payload) {
                    var typeMetadata = TypeMetadata.GetTypeMetadataOrThrow(typeof(this));
                    return OtherOptionalPinvokes.SomeValue(payloadPtr, typeMetadata);
                }
            }
        }
    }
}

internal static class OtherOptionalPinvokes {
      public static unsafe extern void OtherOptionalCases Case(byte *payload, TypeMetadata meta);
      public static unsafe extern void CreateNone(byte *payload, TypeMetadata meta);
      public static unsafe extern void CreateSome(byte *payload, void *someValue, TypeMetadata meta);
      public static unsafe extern void CreateError(byte *payload, SwiftError error, TypeMetadata meta);
      public static unsafe extern void *SomeValue(byte *payload, TypeMetadata meta);
      public static unsafe extern SwiftError ErrorValue(byte *payload, TypeMetadata meta);
}
```

In order to do this with `Console.WriteLine` you must make 3 separate passes through the type from Swift: one for each of the types listed above, each of which will have to have similar repeated logic (does this case have a payload? is the payload generic? etc)

With Dynamo, it's easy to manage this in one pass:

```
foreach case in cases
    create the case in the enum
    create the factory method in the main type
    create the pinvoke method for the factory if needed
    create the accessor method in the main type if needed
    create the pinvoke method for the accessor if needed
end
```

This gets even more complicated when you need to implement a Swift protocol as that will require writing Swift that calls back into C# (reverse pinvoke). Keeping everything in one place is a huge advantage. In the case of a protocol implementation, we have to write:
1 - a C# interface
2 - a C# proxy type that implements that interface
3 - an extension in Swift that understands how to map to the proxy in C#
4 - a C# class to hold reverse pinvokes
5 - a C# class to hold pinvokes

In writing methods, the interface declaration, the C# proxy method, the Swift extension method, the reverse pinvoke, and the pinvoke will all have very similar layouts and if marshaling is complicated by generics, the code will be interdependent.  Rather than scatter and duplicate this logic across 5 different places, it can instead be put in one place with locality of responsibility.

## Operation 

The general notion is that Dynamo lets you build up a syntax tree using language elements which can be connected with familiar collection methods or with C# operators.

The Swift flavor, for example, has the abstract type `SLBaseExpr` which is a base type for expressions. From that there are unary, binary and ternary subclasses. `SLBaseExpr` overloads all the C# overloadable operators, so if you write `someExpr + someOtherExpr` this will combine them into a `SLBinaryExpr` with `+` operator. This makes it easy to generate syntactically correct expressions that read like the code you want it to be.

Generally speaking all the types within Dynamo are meant to be immutable with the exception of collection types. For example a method declaration type would allow you to add to its body or to its parameters, but not modify its visibility, return type, etc.

In addition to making writing code easier, Dynamo has an event system that is used when an element is written that makes it easy to create "attachments" to code elements. This is natural for attributes in C# which can appear before type declarations or parameter declarations. It also makes it straightforward to write conditional compilation directives, pragmas, regions, code comments and documentation, etc.