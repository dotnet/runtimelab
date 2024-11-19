# Compiling with NativeAOT-LLVM

Compilation using NativeAOT-LLVM is currently only supported on Windows x64 and Linux x64. Contributions enabling the compiler on other platforms are welcome.

This document explains how to compile and publish your project using NativeAOT-LLVM toolchain. First, please _ensure that [pre-requisites](prerequisites.md) are installed_. If you are starting a new project, you may find the [samples](../../samples) useful.

## Add ILCompiler package reference

To use NativeAOT-LLVM with your project, you need to add a reference to the ILCompiler NuGet packages containing the Native AOT compiler and runtime. Make sure the `nuget.config` file for your project contains the following package sources under the `<packageSources>` element:
```xml
<add key="dotnet-experimental" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-experimental/nuget/v3/index.json" />
<add key="nuget" value="https://api.nuget.org/v3/index.json" />
```

If your project has no `nuget.config` file, it may be created by running
```bash
> dotnet new nugetconfig
```

from the project's root directory. New package sources must be added after the `<clear />` element if you decide to keep it.

Once you have added the package sources, add references to the ILCompiler packages by adding the following elements to the project file:
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.DotNet.ILCompiler.LLVM" Version="10.0.0-*" />
  <PackageReference Include="runtime.$(NETCoreSdkPortableRuntimeIdentifier).Microsoft.DotNet.ILCompiler.LLVM" Version="10.0.0-*" />
</ItemGroup>
```

Note that it is important to use _the same version_ for both packages to avoid potential hard-to-debug issues (use the latest version from the [dotnet-experimental feed](https://dev.azure.com/dnceng/public/_artifacts/feed/dotnet-experimental/NuGet/Microsoft.DotNet.ILCompiler.LLVM)).

## Adjust the project configuration

NativeAOT-LLVM is not integrated into the SDK, so first remove
```xml
<PublishAot>true</PublishAot>
```
from any `PropertyGroup` tags if you have it. Instead, add
```xml
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <SelfContained>true</SelfContained>
  <MSBuildEnableWorkloadResolver>false</MSBuildEnableWorkloadResolver>
</PropertyGroup>
```

Note that the wasm-tools workload is identified as a dependency even though its not used, and this confuses the toolchain, hence `MSBuildEnableWorkloadResolver=false`.

## Compile and publish your app

Use the `dotnet publish` command to compile and publish your app:
```bash
> dotnet publish -r <RID> -c <Configuration>
```

where `<Configuration>` is your project configuration (such as Debug or Release) and `<RID>` is the runtime identifier reflecting your target (`browser-wasm` or `wasi-wasm`). For example, to publish the Release build of your app for `browser-wasm`, run the following command:
```bash
> dotnet publish -r browser-wasm -c Release
```

If the compilation succeeds, the native artifacts will be placed under the `bin/<Configuration>/net9.0/<RID>/publish/` path relative to your project's root directory.

* For `browser-wasm`, the output can be run via NodeJS: `node <..>/publish/YourProject.js`, or opened in a browser: `emrun <..>/publish/YourProject.html`.
* For `wasi-wasm`, the output can be run via any WASM runtime that supports WASI, e. g. `wasmtime`: `wasmtime publish/YourProject.wasm`.

## WebAssembly application configuration

By default, the build will produce a binary with debug information, which is usually quite large. If you do not need it, add `/p:DebugType=none` to the publish command line.

Another large contributor to the size is globalization support (ICU data and code). You can opt out by setting the [`InvariantGlobalization`](https://learn.microsoft.com/en-us/dotnet/core/runtime-config/globalization) MSBuild property to `true`.

Additionally, NativeAOT-LLVM supports the following properties:
- `WasmHtmlTemplate`: specifies path to the HTML template within which the WASM application will be embedded. An example of a minimal template can be found in the Emscripten repo: https://github.com/emscripten-core/emscripten/blob/main/src/shell_minimal.html

## WebAssembly native libraries

To compile a WebAssembly native library that exports a function `Answer`:
```cs
[System.Runtime.InteropServices.UnmanagedCallersOnly(EntryPoint = "Answer")]
public static int Answer()
{
    return 42;
}
```
```xml
<PropertyGroup>
  <OutputType>library</OutputType> <!-- In addition to the other properties. -->
</PropertyGroup>

<ItemGroup>
  <LinkerArg Include="-sEXPORTED_RUNTIME_METHODS=cwrap" />
  <LinkerArg Include="--post-js=invokeLibraryFunction.js" />
</ItemGroup>
```
```bash
> dotnet publish -r browser-wasm
```
Where `invokeLibraryFunction.js` is a Javascript file with the callback to call `Answer`, e.g.
```js
Module['onRuntimeInitialized'] = function() { 
  // Call your function
  const answer = Module.cwrap('Answer', 'number', []);
  console.log(answer())
};
```

Note that assemblies other than the one being published (e. g. those from referenced projects) need to be explicitly specified in the project file if you want their methods to be exported:
```xml
<ItemGroup>
  <UnmanagedEntryPointsAssembly Include="DependencyAssembly" />
</ItemGroup>
```

See also the [NativeLibrary sample](../../samples/NativeLibrary).

## WebAssembly module imports

Functions in other WebAssembly modules can be imported and invoked using `DllImport` and `WasmImportLinkage` e.g.
```cs
[WasmImportLinkage]
[DllImport("wasi_snapshot_preview1", EntryPoint = "random_get")]
static extern int GetRandom(byte* buf, uint size);
```
This will create an import from the `wasi_snapshot_preview1` module with the function name `random_get`.  The `import` in the WAT would look like this:
```
(import "wasi_snapshot_preview1" "random_get" (func $random_get (type 3)))
```
Note: `WasmImportLinkageAttribute` is currently only available in the nightly SDK. You can either build against the nightly SDK or you can define this attribute in your code:
```cs
namespace System.Runtime.InteropServices
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class WasmImportLinkageAttribute : Attribute
    {
        public WasmImportLinkageAttribute() { }
    }
}
```

This can be used to import WASI functions that are in other modules, either as the above, in WASI, `wasi_snapshot_preview1`, or in other WebAssembly modules that may be linked with [WebAssembly module linking](https://github.com/WebAssembly/module-linking).
