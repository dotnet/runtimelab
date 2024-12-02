# Swift code generation

Certain parts of the Swift ABI are poorly documented and too complex to project directly to .NET, such as metadata, async contexts, and dynamic dispatch thunks. In these cases, Swift wrappers offer the flexibility to encapsulate these poorly documented ABI components within Swift code, enabling more maintainable C#/Swift interop boundary.

The main tradeoff is between more maintainable C#/Swift interop boundary and the increased cost of shipping and trimming. To minimize complexity, we propose generating Swift wrappers only when absolutely necessary, keeping them as thin as possible.

Below, we outline the specific scenarios where Swift wrappers are required.

## Enums

Priority: High (required for StoreKit2 and SwiftUI)

Discriminated unions are not supported in C#. To interact with Swift enums, a wrapper is necessary for payload access and initialization. 

[Implementation details](binding-enums.md)

## Async
Priority: High (required for StoreKit2 and SwiftUI)

Async context in Swift is unknown and require thin wrappers for synchronous invocation. The wrapper will convert async functions into synchronous calls by using a Task as an async worker and a DispatchSemaphore to block until the operation completes. This approach encapsulates the unknown Swift async context, exposing only the stable wrapper interface to .NET.

Here is an example:
```swift
public func loadProductsSync(using storeManager: StoreManager) -> [Product]? {
    let semaphore = DispatchSemaphore(value: 0)
    var result: [Product]?
    
    Task {
        result = await storeManager.loadProducts()
        semaphore.signal()
    }
    
    semaphore.wait()
    return result
}
```

## Dynamic dispatch
Priority: Low

Vtable requirements can be reordered or expanded, making their layout unstable across versioning resilience boundaries. To manage this, the binary framework includes a dispatch thunk which abstracts vtable offsets. The dispatch thunk is part of the framework itself, enabling direct access to the correct offsets while ensuring that the vtable can be extended without breaking compatibility.

### Virtual classes

The goal is to project C# subclass of an open Swift class in Swift. Virtual classes have dynamic dispatch. If they are within the same module, ordered vtable is used for dispatch. Otherwise, a dispatch thunk is generated and invoked to allow for changes.

We want to support:
-	C# class of an open Swift class in Swift
-	C# class of an open Swift class in C#
-	C# subclass of an open Swift class in Swift
-	C# subclass of an open Swift class in C#
When a C# class is instantiated from an open Swift class, it should contain a vtable generated on the Swift side. This vtable is sufficient for performing dynamic dispatch. For a subclass of an open Swift class, a Swift wrapper is generated. The wrapper overrides all virtual methods and delegates them to C#.

When called from C#, the wrapper will invoke the superclass implementation in Swift. When called from Swift, the wrapper will use a simulated vtable. This simulated vtable calls into C#, retrieves a GCHandle for the actual C# class, and invokes its implementation, which in turn calls the Swift superclass implementation.

For types with generics, a vtable is generated for each specialization of the type. Additionally, due to limitations in how C# handles unmanaged callbacks, it is not possible to have an unmanaged callback within a generic class. As a result, separate unmanaged and managed vtables are required.

[Implementation details](binding-classes.md)

### Protocols

The goal is to project C# class in Swift that implements a protocol. Otherwise, a dispatch thunk is generated and invoked to allow for changes.

We want to support:
-	C# class that implements a protocol used as interface in C#
-	C# class that implements a protocol used as protocol in Swift

The Swift wrapper implements a simulated vtable that delegates all protocol methods to C#.

[Implementation details](binding-protocols.md)