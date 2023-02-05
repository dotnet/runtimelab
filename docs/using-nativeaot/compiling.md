# Compiling with Native AOT

This document explains how to compile and publish your project using Native AOT toolchain. First, please _ensure that [pre-requisites](prerequisites.md) are installed_. If you are starting a new project, you may find the [HelloWorld sample](../../samples/HelloWorld/README.md) directions useful.

## Add ILCompiler package reference

To use Native AOT with your project, you need to add a reference to the ILCompiler NuGet package containing the Native AOT compiler and runtime. Make sure the `nuget.config` file for your project contains the following package sources under the `<packageSources>` element:
```xml
<add key="dotnet-experimental" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-experimental/nuget/v3/index.json" />
<add key="nuget" value="https://api.nuget.org/v3/index.json" />
```

If your project has no `nuget.config` file, it may be created by running
```bash
> dotnet new nugetconfig
```

from the project's root directory. New package sources must be added after the `<clear />` element if you decide to keep it.

Once you have added the package sources, add a reference to the ILCompiler package either by running
```bash
> dotnet add package Microsoft.DotNet.ILCompiler -v 7.0.0-*
```

or by adding the following element to the project file:
```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.DotNet.ILCompiler" Version="7.0.0-*" />
  </ItemGroup>
```

## Compile and publish your app

Use the `dotnet publish` command to compile and publish your app:
```bash
> dotnet publish -r <RID> -c <Configuration>
```

where `<Configuration>` is your project configuration (such as Debug or Release) and `<RID>` is the runtime identifier reflecting your host OS and architecture (one of win-x64, linux-x64, osx-x64). For example, to publish the Release build of your app for Windows x64, run the following command:
```bash
> dotnet publish -r win-x64 -c Release
```

If the compilation succeeds, the native executable will be placed under the `bin/<Configuration>/net5.0/<RID>/publish/` path relative to your project's root directory.

## Cross-architecture compilation

Native AOT toolchain allows targeting ARM64 on an x64 host and vice versa for both Windows and Linux. Cross-OS compilation, such as targeting Linux on a Windows host, is not supported. To target win-arm64 on a Windows x64 host, in addition to the `Microsoft.DotNet.ILCompiler` package reference, also add the `runtime.win-x64.Microsoft.DotNet.ILCompiler` package reference to get the x64-hosted compiler:
```xml
<PackageReference Include="Microsoft.DotNet.ILCompiler.LLVM; runtime.win-x64.Microsoft.DotNet.ILCompiler.LLVM" Version="7.0.0-alpha.1.21423.2" />
```

Note that it is important to use _the same version_ for both packages to avoid potential hard-to-debug issues (use the latest version from the [dotnet-experimental feed](https://dev.azure.com/dnceng/public/_packaging?_a=package&feed=dotnet-experimental&package=Microsoft.DotNet.ILCompiler&protocolType=NuGet)). After adding the package reference, you may publish for win-arm64 as usual:
```bash
> dotnet publish -r win-arm64 -c Release
```
### WebAssembly

Install and activate Emscripten. See [Install Emscripten](https://emscripten.org/docs/getting_started/downloads.html#installation-instructions-using-the-emsdk-recommended)

For WebAssembly, it is always a cross-architecture scenario as the compiler runs on Windows/Linux/MacOS and the runtime is for WebAssembly.  WebAssembly is not integrated into the main ILCompiler so first remove (if you added it from above)

```xml
<PackageReference Include="Microsoft.DotNet.ILCompiler" Version="7.0.0-*" />
```

Then, the required package reference is
```xml
<PackageReference Include="Microsoft.DotNet.ILCompiler.LLVM; runtime.win-x64.Microsoft.DotNet.ILCompiler.LLVM" Version="7.0.0-*" />
```
and the publish command (there is no Release build currently)
```bash
> dotnet publish -r browser-wasm -c Debug /p:TargetArchitecture=wasm /p:PlatformTarget=AnyCPU /p:MSBuildEnableWorkloadResolver=false --self-contained
```

Note that the wasm-tools workload is identified as a dependency even though its not used, and this confuses the toolchain, hence `/p:MSBuildEnableWorkloadResolver=false`

#### WebAssembly native libraries
To compile a WebAssembly native library that exports a function `Answer`:
```cs
[System.Runtime.InteropServices.UnmanagedCallersOnly(EntryPoint = "Answer")]
public static int Answer()
{
    return 42;
}
```
```bash
> dotnet publish /p:NativeLib=Static /p:SelfContained=true -r browser-wasm -c Debug /p:TargetArchitecture=wasm /p:PlatformTarget=AnyCPU /p:MSBuildEnableWorkloadResolver=false /p:EmccExtraArgs="-s EXPORTED_FUNCTIONS=_Answer -s EXPORTED_RUNTIME_METHODS=cwrap" --self-contained
```

#### WebAssembly module imports
Functions in other WebAssembly modules can be imported and invoked using `DllImport` e.g.
```cs
[DllImport("*")]
static extern int random_get(byte* buf, uint size);
```
Be default emscripten will create a WebAssembly import for this function, importing from the `env` module.  This can be controlled with `WasmImport` items in the project file.  For example
```xml
<ItemGroup>
    <WasmImport Include="wasi_snapshot_preview1!random_get" />
</ItemGroup>
```
Will cause the above `random_get` to create this WebAssembly:
```
(import "wasi_snapshot_preview1" "random_get" (func $random_get (type 3)))
```

This can be used to import WASI functions that are in other modules, either as the above, in WASI, `wasi_snapshot_preview1`, or in other WebAssembly modules that may be linked with [WebAssembly module linking](https://github.com/WebAssembly/module-linking)

# WASM applications configuration

Currently NativeAOT-LLVM supports following additional properties
- `WasmHtmlTemplate` which specified path to the HTML template within which WASM application would be embeded.

### Cross-compiling on Linux
Similarly, to target linux-arm64 on a Linux x64 host, in addition to the `Microsoft.DotNet.ILCompiler` package reference, also add the `runtime.linux-x64.Microsoft.DotNet.ILCompiler` package reference to get the x64-hosted compiler:
```xml
<PackageReference Include="Microsoft.DotNet.ILCompiler; runtime.linux-x64.Microsoft.DotNet.ILCompiler" Version="7.0.0-alpha.1.21423.2" />
```

You also need to specify the sysroot directory for Clang using the `SysRoot` property. For example, assuming you are using one of ARM64-targeting [Docker images](../workflow/building/coreclr/linux-instructions.md#Docker-Images) employed for cross-compilation by this repo, you may publish for linux-arm64 with the following command:
```bash
> dotnet publish -r linux-arm64 -c Release -p:CppCompilerAndLinker=clang-9 -p:SysRoot=/crossrootfs/arm64
```

You may also follow [cross-building instructions](../workflow/building/coreclr/cross-building.md) to create your own sysroot directory.
