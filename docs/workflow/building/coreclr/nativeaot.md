# Native AOT Developer Workflow

* [Building](#building)
  * [Building for Web Assembly](#building-for-web-assembly)
  * [Using built binaries](#using-built-binaries)
  * [Building packages](#building-packages)
* [High Level Overview](#high-level-overview)
* [Visual Studio Solutions](#visual-studio-solutions)
* [Convenience Visual Studio "repro" project](#convenience-visual-studio-repro-project)
* [Running tests](#running-tests)
  * [Running library tests](#running-library-tests)
* [Design Documentation](#design-documentation)
* [Further Reading](#further-reading)

The Native AOT toolchain can be currently built for Linux (x64/arm64), macOS (x64) and Windows (x64/arm64).

### Building for Web Assembly

- This branch contains a version of the WebAssembly compiler that creates LLVM from the clrjit to take advantage of RyuJit's optimizations specific to managed code, and its compiler infrastructure. It goes from RyuJIT IR -> LLVM instead of the older CoreRT way of CIL -> LLVM.
- The work is of highly experimental nature. Bugs and not-yet-or-ever-to-be-implemented features are to be expected.
- The build supporting a developer workflow currently only exists on Windows.
- Do not attempt to build with the emscripten debug environment variable set.  I.e do not `set EMCC_DEBUG=1` as the extra output will confuse the scripts.

There are two kinds of binary artifacts produced by the build and needed for development: the runtime libraries and the cross-targeting compilers, ILC and RyuJit. They are built differently and separately.

For the runtime libraries:
- To build for web browsers, clone the [emsdk](https://github.com/emscripten-core/emsdk) repository and use the `emsdk.bat` script it comes with to [install](https://emscripten.org/docs/getting_started/downloads.html) (and optionally "activate", i. e. set the relevant environment variables permanently) the Emscripten SDK, which will be used by the native build as a sort of "virtualized" build environment. It is recommended to use the same Emscripten version that [the CI](https://github.com/dotnet/runtimelab/blob/feature/NativeAOT-LLVM/eng/pipelines/runtimelab/install-emscripten.ps1#L16-L20) uses.
  ```
  git clone https://github.com/emscripten-core/emsdk
  cd emsdk
  # Consult with https://github.com/dotnet/runtimelab/blob/feature/NativeAOT-LLVM/eng/pipelines/runtimelab/install-emscripten.cmd#L14-L18
  # for actual commit. That may change without change here.
  git checkout 37b85e9
  ./emsdk install 3.1.47
  ./emsdk activate 3.1.47
  ```
- To build for WASI, download and install WASI-SDK 22 from https://github.com/WebAssembly/wasi-sdk/releases (only Windows and Linux are supported currently) and set the `WASI_SDK_PATH` environment variable to the location where it is installed, e.g. `set WASI_SDK_PATH=c:\github\wasi-sdk`. Note that WASI-SDK 22 only includes a copy of `pthread.h` for the `wasm32-wasi-threads` target, which we must copy to the include directory for the `wasm32-wasi` target, e.g. `cp $WASI_SDK\share\wasi-sysroot\include\wasm32-wasi-threads\pthread.h $WASI_SDK\share\wasi-sysroot\include\wasm32-wasi\`. This is a temporary workaround until https://github.com/WebAssembly/wasi-libc/issues/501 has been addressed and released.
- Run `build clr.aot+libs -c [Debug|Release] -a wasm -os [browser|wasi]`. This will create the architecture-dependent libraries needed for linking and runtime execution, as well as the managed binaries to be used as input to ILC.

For the compilers:
- Run the `llvm-install.ps1` script from `eng\pipelines\runtimelab\install-llvm.ps1`. The script:
  - Clones the `llvm-project` repository (note this will take a long time). You can skip this with `-noclone`.
  - Runs CMake configuration, with VS 2022 as the generator.
  - Builds the projects needed by RyuJit. You can skip this with `-nobuild`.
  - Sets enviroment variables that the runtime build will later use to find LLVM sources: `LLVM_CMAKE_CONFIG_DEBUG` and `LLVM_CMAKE_CONFIG_RELEASE`.
    For convenience, the script does this in scope of the current user, so you don't need to run it more than once (unless the LLVM version changes).
    You can manually set these variables too, to use `Release` LLVM with a `Checked` Jit, or use a different checkout of `llvm-project`.
  - You can alter the configurations the script builds with `-configs`. By default, LLVM is built in `Debug` and `Release`.
- Build the Jits and the ILC: `build clr.wasmjit+clr.aot -c [Debug|Release]`. Add the `libs` subset if you want the packages for publishing, e.g. `build clr.wasmjit+clr.aot+libs -c Debug`.
- You can use the `-msbuild` option, `build clr.wasmjit -msbuild`, to generate a Visual Studio solution for the Jit, to be found in `artifacts/obj/coreclr/windows.x64.Debug/ide/jit`.

With the above binaries built, the ILC can be run and debugged as normal. The runtime tests can also be built, in bulk: `src/tests/build nativeaot [debug|release] wasm tree nativeaot`, or individually: `cd <test-directory> && dotnet build TestProjectName.csproj /t:BuildNativeAot /p:TestBuildMode=nativeaot /p:TargetArchitecture=wasm /p:TargetOS=browser`, and run as described in the sections below. When building individual tests, `src/tests/build nativeaot [debug|release] wasm skipmanaged` needs to be run beforehand at least once, to build the native assets and restore test projects. Note that by default, the tests use `Release` libraries - if you want to use `Debug` ones, pass `/p:LibrariesConfiguration=Debug`.

Working on the Jit itself, one possible workflow is taking advantage of the generated VS project:
- Open the Ilc solution and add the aforementioned Jit project, `clrjit_universal_wasm32_x64.vcxproj`. Then in the project properties, General section, change the output folder to the full path for `artifacts\bin\coreclr\windows.x64.Debug\ilc` e.g. `E:\GitHub\runtimelab\artifacts\bin\coreclr\windows.x64.Debug\ilc`. Build `clrjit_universal_wasm32_x64` project and you should now be able to change and put breakpoints in the C++ code.

It is also possible to publish an ordinary console project for Wasm using packages produced by the build: `build nativeaot.packages && build nativeaot.packages -a wasm -os browser`, assuming all the binaries mentioned above have been built (note that the order is important - the build always produces an architecture-independent package that has a dependency on an architecture-dependent one, and we want that architecture-dependent package to be built for Wasm). Add the `path-to-repo/artifacts/packages/[Debug|Release]/Shipping` directory to your project's `NuGet.Config`, add the property
```xml
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
</PropertyGroup>
```
and the following two references to the project file itself:
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.DotNet.ILCompiler.LLVM" Version="8.0.0-dev" />
  <PackageReference Include="runtime.win-x64.Microsoft.DotNet.ILCompiler.LLVM" Version="8.0.0-dev" />
</ItemGroup>
```
You should now be able to publish the project for Wasm: `dotnet publish --self-contained -r browser-wasm /p:MSBuildEnableWorkloadResolver=false`. This produces `YourApp.html` and `YourApp.js` files under `bin\<Config>\<TFM>\browser-wasm\native`. The former can be opened in the browser, the latter - run via NodeJS.

## Building

1. [Install pre-requisites](/docs/workflow/README.md#build-requirements)
1. Run `build[.cmd|.sh] clr.aot+libs -rc [Debug|Release]` from the repo root to build binaries for local development. This will build individual components, but not the NuGet packages and builds much faster.

### Using built binaries

The paths to major components can be overridden using `IlcToolsPath`, `IlcSdkPath`, `IlcFrameworkPath`, `IlcFrameworkNativePath` and `IlcMibcPath` properties for `dotnet publish`. For example, `/p:IlcToolsPath=<repo root>\artifacts\bin\coreclr\windows.x64.Debug\ilc` can be used to override the compiler with a local debug build for troubleshooting or quick iterations.

### Building packages

Run `build[.cmd|.sh] -c Release` from the repo root to build the NativeAOT toolchain packages. The build will place the toolchain packages at `artifacts\packages\Release\Shipping`. To publish your project using these packages:

* Add the package directory to your `nuget.config` file. For example, add `<add key="local" value="C:\runtime\artifacts\packages\Release\Shipping" />`
* Run `dotnet add package Microsoft.DotNet.ILCompiler -v 9.0.0-dev` to add the local package reference to your project.
* Run `dotnet publish --packages pkg -r [win-x64|linux-x64|osx-64] -c [Debug|Release]` to publish your project. `--packages pkg` option restores the package into a local directory that is easy to cleanup once you are done. It avoids polluting the global nuget cache with your locally built dev package.

## High Level Overview

Native AOT is a stripped down version of the CoreCLR runtime specialized for ahead of time compilation, with an accompanying ahead of time compiler.

The main components of the toolchain are:

* The AOT compiler (ILC/ILCompiler) built on a shared codebase with crossgen2 (src/coreclr/tools/aot). Where crossgen2 generates ReadyToRun modules that contain code and data structures for the CoreCLR runtime, ILC generates code and self-describing datastructures for a stripped down version of CoreCLR into object files. The object files use platform specific file formats (COFF with CodeView on Windows, ELF with DWARF on Linux, and Mach-O with DWARF on macOS).
* The stripped down CoreCLR runtime (NativeAOT specific files in src/coreclr/nativeaot/Runtime, the rest included from the src/coreclr). The stripped down runtime is built into a static library that is linked with object file generated by the AOT compiler using a platform-specific linker (link.exe on Windows, ld on Linux/macOS) to form a standalone executable.
* The bootstrapper library (src/coreclr/nativeaot/Bootstrap). This is a small native library that contains the actual native `main()` entrypoint and bootstraps the runtime and dispatches to managed code. Two flavors of the bootstrapper are built - one for executables, and another for dynamic libraries.
* The core libraries (src/coreclr/nativeaot): System.Private.CoreLib (corelib), System.Private.Reflection.* (the implementation of reflection), System.Private.TypeLoader (ability to load new types that were not generated statically).
* The dotnet integration (src/coreclr/nativeaot/BuildIntegration). This is a set of .targets/.props files that hook into `dotnet publish` to run the AOT compiler and execute the platform linker.

The AOT compiler typically takes the app, core libraries, and framework libraries as input. It then compiles the whole program into a single object file. Then the object file is linked to form a runnable executable. The executable is standalone (doesn't require a runtime), modulo any managed DllImports.

The executable looks like a native executable, in the sense that it can be debugged with native debuggers and have full-fidelity access to locals, and stepping information.

The compiler also has a mode where each managed assembly can be compiled into a separate object file. The object files are later linked into a single executable using the platform linker. This mode is mostly used in testing (it's faster to compile this way because we don't need to recompiling the same code from e.g. CoreLib). It's not a shipping configuration and has many problems (requires exactly matching compilation settings, forfeits many optimizations, and has trouble around cross-module generic virtual method implementations).

## Visual Studio Solutions

The repository has a number of Visual Studio Solutions files (`*.sln`) that are useful for editing parts of the repository. Build the repo from command line first before building using the solution files. Remember to select the appropriate configuration that you built. By default, `build.cmd` builds Debug x64 and so `Debug` and `x64` must be selected in the solution build configuration drop downs.

Solutions related to this:

* `src\coreclr\nativeaot\nativeaot.sln`. This solution is for the runtime libraries.
* `src\coreclr\tools\aot\ilc.sln`. This solution is for the compiler.

Typical workflow for working on the compiler:

* Open `ilc.sln` in Visual Studio
* Set "ILCompiler" project in solution explorer as your startup project
* Set Working directory in the project Debug options to your test project directory, e.g. `C:\test`
* Set Application arguments in the project Debug options to the response file that was generated by regular native aot publishing of your test project, e.g. `@obj\Release\net6.0\win-x64\native\HelloWorld.ilc.rsp`
* Build & run using **F5**

## Convenience Visual Studio "repro" project

Typical native AOT runtime developer scenario workflow is to native AOT compile a short piece of C# and run it. The repo contains helper projects that make debugging the AOT compiler and the runtime easier.

The workflow looks like this:

* Build the repo using the Building instructions above
* Open the ilc.sln solution described above. This solution contains the compiler, but also an unrelated project named "repro". This repro project is a small Hello World. You can place any piece of C# you would like to compile in it. Building the project will compile the source code into IL, but also generate a response file that is suitable to pass to the AOT compiler.
* Make sure you set the solution configuration in VS to the configuration you just built (e.g. x64 Debug).
* In the ILCompiler project properties, on the Debug tab, set the "Application arguments" to `@$(ArtifactsBinDir)repro\$(TargetArchitecture)\$(Configuration)\compile-with-Release-libs.rsp`. The `@` at the front of the argument indicates that this is the path to the response file generated when "repro" was built. Adjust the "compile-with-Release-libs" part to "compile-with-Debug-libs" depending on how you built the libraries (the `-lc` argument to `build.cmd`). Visual Studio will expand the path to something like `@C:\runtime\artifacts\bin\repro\x64\Debug\compile-with-Release-libs.rsp`.
* Build & run ILCompiler using **F5**. This will compile the repro project into an `.obj` file. You can debug the compiler and set breakpoints in it at this point.
* The last step is linking the object file into an executable so that we can launch the result of the AOT compilation.
* Open the src\coreclr\tools\aot\ILCompiler\reproNative\reproNative.vcxproj project in Visual Studio. This project is configured to pick up the `.obj` file we just compiled and link it with the rest of the runtime.
* Set the solution configuration to the tuple you've been using so far (e.g. x64 Debug)
* Build & run using **F5**. This will run the platform linker to link the obj file with the runtime and launch it. At this point you can debug the runtime and the various System.Private libraries.

## Running tests

If you haven't built the tests yet, run `src\tests\build.cmd nativeaot [Debug|Release] tree nativeaot` on Windows, or `src/tests/build.sh -nativeaot [Debug|Release] -tree:nativeaot` on Linux. This will build the smoke tests only - they usually suffice to ensure the runtime and compiler is in a workable shape. To build all Pri-0 tests, drop the `tree nativeaot` parameter. The `Debug`/`Release` parameter should match the build configuration you used to build the runtime.

Note that to run WASM tests targeting Browser, NodeJS that supports the exception handling feature is required (v17+). In particular, NodeJS that comes with the Emscripten SDK is not suitable.

To run all the tests that got built, run `src\tests\run.cmd runnativeaottests [Debug|Release] [wasm]` on Windows, or `src/tests/run.sh --runnativeaottests [Debug|Release] [wasm]` on Linux. The `Debug`/`Release` flag should match the flag that was passed to `build.cmd` in the previous step.

To run an individual test (after it was built), navigate to the `artifacts\tests\coreclr\[windows|linux|osx[.x64.[Debug|Release]\$path_to_test` directory. `$path_to_test` matches the subtree of `src\tests`. You should see a `[.cmd|.sh]` file there. This file is a script that will compile and launch the individual test for you. Before invoking the script, set the following environment variables:

* CORE_ROOT=$repo_root\artifacts\tests\coreclr\[windows|linux|osx].x64.[Debug|Release]\Tests\Core_Root
* CLRCustomTestLauncher=$repo_root\src\tests\Common\scripts\nativeaottest[.cmd|.sh]

`$repo_root` is the root of your clone of the repo.

Sometimes it's handy to be able to rebuild the managed test manually or run the compilation under a debugger. A response file that was used to invoke the ahead of time compiler can be found in `$repo_root\artifacts\tests\coreclr\obj\[windows|linux|osx].x64.[Debug|Release]\Managed`.

For more advanced scenarios, look for at [Building the Tests](/docs/workflow/testing/coreclr/testing.md#building-the-tests) and [Building the Core_Root](../../testing/coreclr/testing.md#building-the-coreroot)

### Running library tests

Build library tests by passing the `libs.tests` subset together with the `/p:TestNativeAot=true` to build the libraries, i.e. `clr.aot+libs+libs.tests /p:TestNativeAot=true` together with the full arguments as specified [above](#building). Then, to run a specific library, go to the tests directory of the library and run the usual command to run tests for the library (see [Running tests for a single library](/docs/workflow/testing/libraries/testing.md#running-tests-for-a-single-library)) but add the `/p:TestNativeAot=true` and the build configuration that was used, i.e. `dotnet.cmd build /t:Test /p:TestNativeAot=true -c Release`.

### NativeAOT-LLVM: `wasmjit-diff.ps1`

This script under `src/tests/nativeaot/SmokeTests/HelloWasm` is useful for assesing codegen changes and works a bit like `jit-diff`/`jit-analyze`. Invoke it without arguments to get details about usage. Note that the script requires a modern (7.0+) PowerShell version.

## Design Documentation

* [ILC Compiler Architecture](/docs/design/coreclr/botr/ilc-architecture.md)
* [Managed Type System](/docs/design/coreclr/botr/managed-type-system.md)

## Native Sanitizers

Using native sanitizers with NativeAOT requires additional care compared to using them with CoreCLR. In addition to passing the `-fsanitize` flag to the command that builds NativeAOT, you must also pass the `EnableNativeSanitizers` MSBuild property to any commands that build projects with a sanitized NativeAOT build to ensure that any sanitizer runtimes are correctly linked with the project.

## Further Reading

If you want to know more about working with _NativeAOT_ in general, you can check out their [more in-depth docs](/src/coreclr/nativeaot/docs/README.md) in the `src/coreclr/nativeaot` subtree.
