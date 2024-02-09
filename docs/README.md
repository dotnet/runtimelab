# .NET Swift interop tooling documentation

This document provides a detailed overview of the .NET Swift interop tooling, focusing on the projections between Swift and .NET, and the functional design of the tooling.

## Projections

This section outlines the mappings between Swift and .NET types, and describes how the tool generates C# bindings for a Swift library. For types with similar semantics across Swift and .NET, direct interop is possible and bindings are generated. For types without direct projection, additional Swift wrappers are required and it is the user's responsibility to generate these wrappers. At this stage, the tool is designed to avoid generating any Swift code if possible to avoid complexity of maintaining different Swift compiler and SDK versions. The tool should only generate bindings for Swift types that are part of the stable ABI and don't evolve.

### Primitive types

The table below lists the Swift types and their corresponding C# types.

| Swift Type                      | C# Type  |
| ------------------------------- | -------- |
| `Swift.Int64`                   | `long`   |
| `Swift.UInt64`                  | `ulong`  |
| `Swift.Int32`                   | `int`    |
| `Swift.UInt32`                  | `uint`   |
| `Swift.Int16`                   | `short`  |
| `Swift.UInt16`                  | `ushort` |
| `Swift.Int8`                    | `sbyte`  |
| `Swift.UInt8`                   | `byte`   |
| `Swift.UnsafeRawPointer`        | `IntPtr` |
| `Swift.UnsafeMutableRawPointer` | `IntPtr` |
| `Int`                           | `nint`   |
| `UInt`                          | `nuint`  |
| `Bool`                          | `bool`   |
| `Float`                         | `float`  |
| `Double`                        | `double` |

Swift primitive types are implemented as frozen structs that conform to Swift-specific lowering processes handled by the runtime. However, such mapping can fit within the underlying calling convention as these types are below the size limit for being passed by reference. 

### Structs

Swift structs are projected into C# as `IDisposable` classes, implementing `ISwiftStruct`, which inherit from `ISwiftNominalType` and `ISwiftDisposable`. This approach is chosen due to the semantic differences in lifecycle management between Swift and C# types. Bindings are generated with `SwiftStructAttribute` that inherits `SwiftNominalTypeAttribute`. This attribute contains information about the Swift type.

Given the following Swift struct declaration:
```swift
    public struct BarInt {
        public var X:Int; 
        public init(x:Int) {
            X = x;
        }
    }
```

The projection tooling will generate the following C#, with function bodies left empty for simplicity:
```csharp
    using System;
    using System.Runtime.InteropServices;
    using SwiftRuntimeLibrary;
    using SwiftRuntimeLibrary.SwiftMarshal;
    
    namespace NewClassCompilerTests
    {
        [SwiftStruct("libNewClassCompilerTests.dylib",
            "$s21NewClassCompilerTests6BarIntVMn", 
            "$s21NewClassCompilerTests6BarIntVN", "")]
        public class BarInt : ISwiftStruct
        {
            public BarInt(nint x)
            {
            }
            internal BarInt(SwiftNominalCtorArgument unused)
            {
            }
            public static SwiftMetatype GetSwiftMetatype()
            {
            }
            public void Dispose()
            {
            }
            void Dispose(bool disposing)
            {
            }
            ~BarInt()
            {
            }
            public byte[] SwiftData
            {
                get; set;
            }
            public nint X
            {
                get { }
                set { }
            }
        }
    }
```

There is a payload `SwiftData` along with two constructors. The first maps onto the `init` method inside the swift class. The second is an internal constructor that gets used to define an uninitialized type. This constructor gets used by the marshaler when a type needs to be allocated before it gets used, for example, as a return value because Swift semantics don’t allow to explicitly have variables in an uninitialized state. 

All properties bound in C# access the value through accessor functions. If a Swift property or a Swift method mutates the value, the contents of the C# `SwiftData` property will get changed as well.

### Enums

Swift enums are projected into C# as `IDisposable` classes, implementing `ISwiftEnum`, in similar manner as structs.

Given this Swift enum:
```swift
    public enum FooECTIA {
        case a(Int)
        case b(Double)
    }
```

The projection tooling will generate the following C#, with function bodies left empty for simplicity:
```csharp
    using System;
    using System.Runtime.InteropServices;
    using SwiftRuntimeLibrary;
    using SwiftRuntimeLibrary.SwiftMarshal;
    
    namespace NewClassCompilerTests
    {
        public enum FooECTIACases
        {
            A, B
        }
        [SwiftEnumType("libNewClassCompilerTests.dylib",
            "$s21NewClassCompilerTests8FooECTIAOMn", 
            "$s21NewClassCompilerTests8FooECTIAON", "")]
        public class FooECTIA : ISwiftEnum
        {
            public void Dispose()
            {
            }
            void Dispose(bool disposing)
            {
            }
            ~FooECTIA()
            {
            }
            public static FooECTIA NewA(nint value0)
            {
            }
            public static FooECTIA NewB(double value0)
            {
            }
            public byte[] SwiftData
            {
                get; set;
            }
            public nint ValueA
            {
                get { }
            }
            public double ValueB
            {
                get { }
            }
            public FooECTIACases Case
            {
                get { }
            }
        }
    }
```

### Scaling and trimming

The bindings should be organized into namespaces to mitigate scalability issues. Additionally, generated bindings should be trim-compatible.

This subsection will be expanded during the design review process.

### Unsupported types

This subsection describes cases when direct interop is not possible and will be expanded during the design review process. This is a subset of features that require Swift wrappers provided by users:
- Protocols and classes with virtual methods
- Closures
- Passing a struct by value in more registers than P/Invoke will allow
- Exporting .NET into Swift

## Functional outline

The tooling consists of the following components:
- **CLIInterface**: A command-line interface that orchestrates the tooling workflow.
- **SwiftReflector**: A component that aggregates public API definitions from Swift modules.
- **Dynamo**: A component that provides API for C# code generation.
- **SwiftRuntimeLibrary**: A component that provides runtime marshaling support and common Swift type projections.

The `SwiftRuntimeLibrary` This component is a library that provides basic support for Swift interop that the generated code builds on. It includes bindings for common built-in types like arrays and dictionaries. It provides a single source of truth for Swift types so that they can be exposed across an assembly boundary. 

### Workflow

The general workflow for generating C# bindings from Swift code is as follows:

1. Process the Swift library interface (`.swiftinterface`) with the SwiftReflector and generate `TLDefinition` with mappings between entry points and mangled names.
2. Aggregate a public API based on `.swiftinterface` with the SwiftReflector.
3. Generate source code for C# bindings with Dynamo and compile the code into a managed library.

### Shipping

This subsection will be expanded during the design review process.

### User expirience

The tooling perhaps doesn't need to surface all APIs, but only those that are used. Users should be able to indicate a subset of APIs that should be bound.

This subsection will be expanded during the design review process.
