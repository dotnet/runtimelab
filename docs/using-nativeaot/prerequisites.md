If you're new to .NET, make sure to visit the [official starting page](http://dotnet.github.io). It will guide you through installing pre-requisites and building your first app. If you're already familiar with .NET, make sure you've [downloaded and installed the latest .NET SDK](https://www.microsoft.com/net/download/core).

The following pre-requisites need to be installed for building .NET projects with NativeAOT-LLVM:

## Emscripten SDK

* Install and activate Emscripten. See [Install Emscripten](https://emscripten.org/docs/getting_started/downloads.html#installation-instructions-using-the-emsdk-recommended).
* We strongly recommend using the same version that we test against. Look for it here: [install-emscripten.cmd](https://github.com/dotnet/runtimelab/blob/feature/NativeAOT-LLVM/eng/pipelines/runtimelab/install-emscripten.cmd).
```
./emsdk install <version>
./emsdk activate <version>
```

## WASI SDK

* Only needed when targeting `wasi-wasm`.
* Download [the WASI SDK](https://github.com/WebAssembly/wasi-sdk/releases), extract it and set the `WASI_SDK_PATH` environment variable to the directory containing `share/wasi-sysroot`.
* We strongly recommend using the same version that we test against. Look for it here: [install-wasi-sdk.ps1](https://github.com/dotnet/runtimelab/blob/feature/NativeAOT-LLVM/eng/pipelines/runtimelab/install-wasi-sdk.ps1).
