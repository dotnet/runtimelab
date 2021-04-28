# DllImport Generator

Work on this project can be tracked and discussed via the [official issue](https://github.com/dotnet/runtime/issues/43060) in `dotnet/runtime`. The [P/Invoke source generator proposal](https://github.com/dotnet/runtime/blob/main/docs/design/features/source-generator-pinvokes.md) contains additional details.

## Example

The [Demo project](./DllImportGenerator/Demo) is designed to be immediately consumable by everyone. It demonstrates a simple use case where the marshalling code is generated and a native function call with only [blittable types](https://docs.microsoft.com/dotnet/framework/interop/blittable-and-non-blittable-types) is made. The project is configured to output the generated source to disk; the files can be found in the project's intermediate directory (`obj/<Configuration>/<TargetFramework>/generated`). A managed assembly with [native exports](./DllImportGenerator/TestAssets/NativeExports) is used in the P/Invoke scenario.

### Recommended scenarios:

* Step into the `Demo` application and observe the generated code for the `Sum` functions.
* Find the implementation of the `sumrefi` function and set a breakpoint. Run the debugger and explore the stack.
* Add a new export in the `NativeExports` project and update the `Demo` application to call the new export.
* Try the above scenarios when building in `Debug` or `Release`. Consider the differences.

## Designs

- [Code generation pipeline](./designs/Pipeline.md)
- [Struct Marshalling](./designs/StructMarshalling.md)

## Workflow

This repo consumes new APIs from `dotnet/runtime` and is [configured](./global.json) to build against a custom runtime version. Either globally install the .NET runtime version specified by the [`MicrosoftNETCoreAppVersion` property](./eng/Versions.props) or run `build.cmd/sh` to have the Arcade infrastructure populate the `.dotnet` folder with the matching runtime version.

All features of the [`dotnet` command line tool](https://docs.microsoft.com/dotnet/core/tools/) are supported for the respective project types (e.g. `build`, `run`, `test`). A consistent cross-platform inner dev loop with an IDE is available using [Visual Studio Code](https://code.visualstudio.com/) when appropriate .NET extensions are loaded.

On Windows, loading the [solution](./DllImportGenerator/DllImportGenerator.sln) in [Visual Studio](https://visualstudio.microsoft.com/) 2019 or later will enable the edit, build, debug inner dev loop. All features of Visual Studio are expected to operate correctly (e.g. Debugger, Test runner, Profiler).

Most of the above options have [official tutorials](https://docs.microsoft.com/dotnet/core/tutorials/). It is an aim of this project to follow canonical workflows that are intuitive to all developers.

### Testing assets

This project has no explicit native build system and should remain that way. The [`DNNE`](https://github.com/AaronRobinsonMSFT/DNNE/) project is used to create native exports that can be called from the P/Invokes during testing.

## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## License

.NET (including the runtime repo) is licensed under the [MIT](LICENSE.TXT) license.
