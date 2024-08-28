## Native library with NativeAOT-LLVM

This is an example of a simple native library that can be built with NativeAOT-LLVM.

See the [compiling](../../docs/using-nativeaot/compiling.md) document for details on how to set up your project.

## Publish and run this sample

Open a new shell/command prompt window and run the following commands:
```bash
> dotnet publish -r browser-wasm
> emrun NativeLibraryHost.html
```

The build produces a `.js` file with the accompanying `.wasm` under `./bin/Release/net9.0/browser-wasm/publish` that the `NativeLibraryHost.html` references.

## Loading native libraries from the host

When targeting `browser-wasm`, the library produced includes Emscripten-generated JavaScript code that loads and instantiates the WASM.

See Emscripten documentation on how to properly interact with this code: https://emscripten.org/docs/porting/connecting_cpp_and_javascript/Interacting-with-code.html.

This sample uses the `Using direct function calls` option: exported functions are made available globally in JavaScript, prefixed by an underscore.

When targeting `wasi-wasm`, the library produced is a self-contained WASM module. Interacting with it involves simply calling its exports.

## Exporting methods

For a C# method in the native library to be consumable by the host, it has to be explicitly exported using the `[UnmanagedCallersOnly]` attribute.
Apply the attribute to the method, specifying the `EntryPoint`:

```csharp
[UnmanagedCallersOnly(EntryPoint = "add")]
public static int Add(int a, int b)
{
    return a + b;
}
```

After the native library library is built, the above C# `Add` method will be available as a WASM export named `add` to consumers of the library. Here are some limitations to consider when deciding what managed method to export:

* Exported methods have to be static.
* Exported methods can only naturally accept or return primitives or value types (i.e structs), they have to marshal all reference type arguments.
* Exported methods cannot be called from regular managed C# code, an exception will be thrown.
* Exported methods cannot use regular C# exception handling, they should return error codes instead.

The sample [source code](NativeLibrary.cs) demonstrates common techniques used to stay within these limitations.
