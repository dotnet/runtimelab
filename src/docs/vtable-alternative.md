# Vtable Alternative

In Binding Tools for Swift, we use a simulated vtable in order to delegate implementations of Swift types to C#.

This process works, but it has a number of problems. All of them stem from the same issue: Swift knows nothing about C# and needs to call a function exported from C# that is `[UnmanagedCallersOnly]`. I refer to this type of function as a Receiver. The Receiver then needs to get to the actual C# implementation.
1. the Swift vtable - a struct of C calling convention function pointers to Receivers
2. a C# vtable to hold delegates for the Receivers that will be marshalled as unmanaged functions pointers. This definition lives in a separate static class since Receivers can't live inside of a (possibly) generic type
3. a C# vtable to hold managed delegates to the actual implementation which lives in the bound type.

As you can imagine, it can be painful to keep these types in sync.

C# must call into Swift in order to initialize the vtable. In addition, if the type being bound is generic, then there needs to be a vtable for each unique specialization of the type.

Because the population of the vtable is done at runtime, the calling Swift code is required to test the function pointer for null, even though it should never be null.

Most of these issues can be solved if Swift can call C# exported functions directly. This can be done if the binding library is compiled with NativeAOT, generating a dylib.

To do this, we leverage the Swift package manager. We start by creating a directory for information to pass to the compiler. In this directory create a subdirectory for C header files and aggregate any and all headers that declare the `[UnmanagedCallersOnly]` entry points. Finally, in the module directory you create a module.map file that declares where to look for the headers.
So with a file structure like this:
```
+- Module
  |
  +- Headers
  | |
  | +- csharpdeclarations.h
  |
  +- module.map
```
The swift compiler can interpret the declarations.

Here's a module.map that will work in this example:
```
module SomeCSharpLib {
	umbrella "Headers"
	export *
}
```

Let's work with a very simple Swift binding example:
```swift
public protocol Ageable {
    func getAge() -> Int32
}
```
This is a protocol defined in Swift. If we wanted to bind to it in C# we need a proxy for any C# types that implement the interface. Such a proxy could look like this:
```swift
import SomeCSharpLib // this refers to the module.map we made

public class CSAgeableProxy : Ageable {
    public func getAge() -> Int32 {
        return csGetAgeImpl()
    }
}
```
Inside the header file, we have this declaration:
```c
#ifdef __cplusplus
extern "C" {
#endif

extern int csGetAgeImpl();

#ifdef __cplusplus
}
#endif
```
And in a C# file, we have:
```csharp
public static class ThisIsNotImportant
{
    [UnmanagedCallersOnly (EntryPoint = "csGetAgeImpl")]
    public static int csGetAgeImpl()
    {
        return 17;
    }
}
```

When the proxy gets compiled, it will need a `-I` directive to point to the Module directory (eg, `-I /path/to/Module`) and a linker directive to point to the NativeAOT compiled C# code.

The ordering presents an issue in that Swift needs a compiled dylib to link to but this won't necessarily be available until much later in the binding process. We can work with this by have a post-build step to compile and link the Swift proxies. We can also work around this by writing C code as a placeholder for the C# dylib by writing stubs for each and every entrypoint:
```c
int csGetAgeImpl()
{
    return 0;
}
```
and compile this to a dylib identically named as the C# library and use the output as a placeholder for earlier compilation and linking of the Swift proxy code. Then at packaging time, the placeholder will get replaced with the NativeAOT compiled C# library.

This is a trivial example and is missing many more pieces. In reality, there would be a C# interface declaration as well as a C# type that represents a C# proxy. The Swift proxy should have an initializer that takes an `OpaquePointer` which is a C# `GCHandle` which is created from the C# proxy instance. Finally the C# receiver would not have any of the implementation, but instead use the `GCHandle` to get to the proxy and call it for the actual implementation.

It should be noted that while the details here are tied to protocol/proxy binding, it will also apply to protocols with associate types as well as to virtual classes. Proxies for PATs are somewhat more complicated because both the C# and Swift proxies will be generic.