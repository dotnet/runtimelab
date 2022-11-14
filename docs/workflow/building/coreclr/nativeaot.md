# Native AOT Developer Workflow

The Native AOT toolchain can be currently built for Linux, macOS and Windows x64.

## Building

- [Install pre-requisites](../../README.md#build-requirements)
- Run `build[.cmd|.sh] nativeaot+libs+nativeaot.packages -rc [Debug|Release] -lc Release`. This will restore nuget packages required for building and build the parts of the repo required for the Native AOT toolchain.
- The build will place the toolchain packages at `artifacts\packages\[Debug|Release]\Shipping`. To publish your project using these packages:
   - Add the package directory to your `nuget.config` file. For example, replace `dotnet-experimental` line in `samples\HelloWorld\nuget.config` with `<add key="local" value="C:\runtimelab\artifacts\packages\Debug\Shipping" />`
   - Run `dotnet publish --packages pkg -r [win-x64|linux-x64|osx-64] -c [Debug|Release]` to publish your project. `--packages pkg` option restores the package into a local directory that is easy to cleanup once you are done. It avoids polluting the global nuget cache with your locally built dev package.
- *Optional*. If you want fix ObjWriter, or work on unsupported platform, as additional pre-requiresites you need to run `build[.cmd|.sh] nativeaot.objwriter` before building `nativeaot` subset.

## Building for Web Assembly

- This branch contains a version of the WebAssembly compiler that creates LLVM from the clrjit to take advantage of RyuJit's optimizations specific to managed code, and its compiler infrastructure. It goes from RyuJIT IR -> LLVM instead of the older CoreRT way of CIL -> LLVM.
- The work is of highly experimental nature. Bugs and not-yet-or-ever-to-be-implemented features are to be expected.
- The build supporting a developer workflow currently only exists on Windows.
- Do not attempt to build with the emscripten debug environment variable set.  I.e do not `set EMCC_DEBUG=1` as the extra output will confuse the scripts.

There are two kinds of binary artifacts produced by the build and needed for development: the runtime libraries and the cross-targeting compilers, ILC and RyuJit. They are built differently and separately.

For the runtime libraries:
- Clone the [emsdk](https://github.com/emscripten-core/emsdk) repository and use the `emsdk.bat` script it comes with to [install](https://emscripten.org/docs/getting_started/downloads.html) (and optionally "activate", i. e. set the relevant environment variables permanently) the Emscripten SDK, which will be used by the native build as a sort of "virtualized" build environment. It is recommended to use the same Emscripten version that [the CI](https://github.com/dotnet/runtimelab/blob/feature/NativeAOT-LLVM/eng/pipelines/runtimelab/install-emscripten.cmd#L14-L18) uses.
  ```
  git clone https://github.com/emscripten-core/emsdk
  cd emsdk
  # Consult with https://github.com/dotnet/runtimelab/blob/feature/NativeAOT-LLVM/eng/pipelines/runtimelab/install-emscripten.cmd#L14-L18
  # for actual commit. That may change without change here.
  git checkout 044d620
  ./emsdk install 2.0.33
  ./emsdk activate 2.0.33
  ```
- Run `build nativeaot+libs -c [Debug|Release] -a wasm -os Browser`. This will create the architecture-dependent libraries needed for linking and runtime execution, as well as the managed binaries to be used as input to ILC.

For the compilers:
- Download the LLVM 11.0.0 source from https://github.com/llvm/llvm-project/releases/download/llvmorg-11.0.0/llvm-11.0.0.src.tar.xz
- Extract it and create a subdirectory in the `llvm-11.0.0.src` folder (`path-to-the-build-directory`).
- Configure the LLVM source to use the same runtime as the Jit: `cmake -G "Visual Studio 17 2022" -DCMAKE_BUILD_TYPE=Debug -D LLVM_USE_CRT_DEBUG=MTd path-to-the-build-directory` or if building for the Release configuration `cmake -G "Visual Studio 17 2022" -DCMAKE_BUILD_TYPE=Release -D LLVM_USE_CRT_RELEASE=MT path-to-the-build-directory`
- Build LLVM either from the command line (`cmake --build . --target LLVMCore LLVMBitWriter`) or from VS 2022. Currently the Jit depends only on the output of LLVMCore and LLVMBitWriter projects.  For the Release configuration, `cmake --build . --config Release  --target LLVMCore LLVMBitWriter`
- Set the enviroment variable `LLVM_CMAKE_CONFIG` to locate the LLVM config: `set LLVM_CMAKE_CONFIG=path-to-the-build-directory/lib/cmake/llvm`. This location should contain the file `LLVMConfig.cmake`.
- Build the Jits and the ILC: `build clr.jit+clr.wasmjit+nativeaot.ilc -c [Debug|Release]`. Note that `clr.jit` only needs to be built once.
- You can use the `-msbuild` option, `build clr.wasmjit -msbuild`, to generate a Visual Studio solution for the Jit, to be found in `artifacts/obj/coreclr/windows.x64.Debug/ide/jit`.

With the above binaries built, the ILC can be run and debugged as normal. The runtime tests can also be built, in bulk: `src/tests/build nativeaot debug wasm skipnative targetsNonWindows /p:SmokeTestsOnly=true`, or individually: `cd <test-directory> && dotnet build TestProjectName.csproj /p:TargetArchitecture=wasm /p:TargetOS=Browser`, and run as described in the sections below. A response file for debugging ILC can also be obtained from the test build, e. g. for `SmokeTests\HelloWasm` it'd be located in `artifacts\tests\coreclr\Browser.wasm.Debug\nativeaot\SmokeTests\HelloWasm\HelloWasm\native\HelloWasm.ilc.rsp`.

Working on the Jit itself, one possible workflow is taking advantage of the generated VS project:
- Open the Ilc solution and add the aforementioned Jit project, `clrjit_browser_wasm32_x64.vcxproj`. Then in the project properties, General section, change the output folder to the full path for `artifacts\bin\coreclr\windows.x64.Debug\ilc` e.g. `E:\GitHub\runtimelab\artifacts\bin\coreclr\windows.x64.Debug\ilc`. Build `clrjit_browser_wasm32_x64` project and you should now be able to change and put breakpoints in the C++ code.

It is also possible to publish an ordinary console project for Wasm using packages produced by the build: `build nativeaot.packages && build nativeaot.packages -a wasm -os Browser`, assuming all the binaries mentioned above have been built (note that the order is important - the build always produces an architecture-independent package that has a dependency on an architecture-dependent one, and we want that architecture-dependent package to be built for Wasm). Add the `path-to-repo/artifacts/packages/[Debug|Release]/Shipping` directory to your project's `NuGet.Config`, and the following two references to the project file itself:
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.DotNet.ILCompiler.LLVM" Version="7.0.0-dev" />
  <PackageReference Include="runtime.win-x64.Microsoft.DotNet.ILCompiler.LLVM" Version="7.0.0-dev" />
</ItemGroup>
```
You should now be able to publish the project for Wasm: `dotnet publish --self-contained -r browser-wasm /p:MSBuildEnableWorkloadResolver=false`. This produces `YourApp.html` and `YourApp.js` files under `bin\<Config>\<TFM>\browser-wasm\native`. The former can be opened in the browser, the latter - run via NodeJS.

## Visual Studio Solutions

The repository has a number of Visual Studio Solutions files (`*.sln`) that are useful for editing parts of the repository. Build the repo from command line first before building using the solution files. Remember to select the appropriate configuration that you built. By default, `build.cmd` builds Debug x64 and so `Debug` and `x64` must be selected in the solution build configuration drop downs.

- `src\coreclr\nativeaot\nativeaot.sln`. This solution is for the runtime libraries.
- `src\coreclr\tools\aot\ilc.sln`. This solution is for the compiler.

Typical workflow for working on the compiler:
- Open `ilc.sln` in Visual Studio
- Set "ILCompiler" project in solution explorer as your startup project
- Set Working directory in the project Debug options to your test project directory, e.g. `C:\runtimelab\samples\HelloWorld`
- Set Application arguments in the project Debug options to the response file that was generated by regular native aot publishing of your test project, e.g. `@obj\Release\net5.0\win-x64\native\HelloWorld.ilc.rsp`
- Build & run using **F5**

## Convenience Visual Studio "repro" project

Typical native AOT runtime developer scenario workflow is to native AOT compile a short piece of C# and run it. The repo contains helper projects that make debugging the AOT compiler and the runtime easier.

The workflow looks like this:

- Build the repo using the Building instructions above
- Open the ilc.sln solution described above. This solution contains the compiler, but also an unrelated project named "repro". This repro project is a small Hello World. You can place any piece of C# you would like to compile in it. Building the project will compile the source code into IL, but also generate a response file that is suitable to pass to the AOT compiler.
- Make sure you set the solution configuration in VS to the configuration you just built (e.g. x64 Debug).
- In the ILCompiler project properties, on the Debug tab, set the "Application arguments" to the generated response file. This will be a file such as "C:\runtimelab\artifacts\bin\repro\x64\Debug\compile-with-Release-libs.rsp". Prefix the path to the file with "@" to indicate this is a response file so that the "Application arguments" field looks like "@some\path\to\file.rsp".
- For WebAssembly, edit the .rsp file and 
- Build & run ILCompiler using **F5**. This will compile the repro project into an `.obj` file. You can debug the compiler and set breakpoints in it at this point.
- The last step is linking the file into an executable so that we can launch the result of the AOT compilation.
- Open the src\coreclr\tools\aot\ILCompiler\reproNative\reproNative.vcxproj project in Visual Studio. This project is configured to pick up the `.obj` file we just compiled and link it with the rest of the runtime.
- Set the solution configuration to the tuple you've been using so far (e.g. x64 Debug)
- Build & run using **F5**. This will run the platform linker to link the obj file with the runtime and launch it. At this point you can debug the runtime and the various System.Private libraries.

## Running tests

If you haven't built the tests yet, run `src\tests\build[.cmd|.sh] nativeaot [Debug|Release] /p:SmokeTestsOnly=true`. This will build the smoke tests only - they usually suffice to ensure the runtime and compiler is in a workable shape. To build all Pri-0 tests, drop the `SmokeTestsOnly` parameter. The `Debug`/`Release` parameter should match the build configuration you used to build the runtime.

To run all the tests that got built, run `src\tests\run.cmd runnativeaottests [Debug|Release] [wasm]` on Windows, or `src/tests/run.sh --runnativeaottests [Debug|Release] [wasm]` on Linux. The `Debug`/`Release` flag should match the flag that was passed to `build.cmd` in the previous step.

To run an individual test (after it was built), navigate to the `artifacts\tests\coreclr\[Windows|Linux|OSX[.x64.[Debug|Release]\$path_to_test` directory. `$path_to_test` matches the subtree of `src\tests`. You should see a `[.cmd|.sh]` file there. This file is a script that will compile and launch the individual test for you. Before invoking the script, set the following environment variables:

* CORE_ROOT=$repo_root\artifacts\tests\coreclr\[Windows|Linux|OSX[.x64.[Debug|Release]\Tests\Core_Root
* RunNativeAot=1
* __TestDotNetCmd=$repo_root\dotnet[.cmd|.sh]

`$repo_root` is the root of your clone of the repo.

## Design Documentation

- [ILC Compiler Architecture](../../../design/coreclr/botr/ilc-architecture.md)
- [Managed Type System](../../../design/coreclr/botr/managed-type-system.md)
