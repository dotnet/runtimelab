## Hello World console app with NativeAOT-LLVM

This is an example of a simple console application that can be built with NativeAOT-LLVM.

See the [compiling](../../docs/using-nativeaot/compiling.md) document for details.

## Publish and run this sample

Open a new shell/command prompt window and run the following commands:
```bash
# If targeting Browser (NodeJS):
> dotnet publish -r browser-wasm
> node bin\Release\net9.0\browser-wasm\publish\HelloWorld.js

# If targeting WASI:
> dotnet publish -r wasi-wasm
> wasmtime bin\Release\net9.0\wasi-wasm\publish\HelloWorld.wasm
```
