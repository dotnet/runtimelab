# .NET Runtime - Managed Zlib

This branch contains a template for standalone experiments, which means that experiments that are a library, which doesn't depend on runtime changes. This minimal template allows such experiments to avoid the overhead of having all the runtime, libraries and installer code.

This branch contains an experimental porting and comparison of the C Mark Adler's Zlib library used in System.IO.Compression to a C#. This is an exploration on whether we would benefit from switching our dependencies from madler/zlib to our own managed version. With the current approach, using madler/zlib, the C# libraries team doesn't have guarantees on handling the security issues quickly which can be a vulnerability. 
The evaluation of this experiment will be based on performance information using dotnet benchmark and madler/zlib  as baseline,  to see the main areas of opportunity of performance improvement. Because the idea is to implement a managed version, porting madler/zlib, without losing functionality in the way, this experiment covers a set of tests for securing the minimum functionality, based on the API's needs and the RFC and Zlib's manual specifications. It is necessary enough of the design for the information gathered to be reliable. 
This is a full exploration of the idea of owning a managed performance improved version of the Compression Zlib API, considering not just porting the native version currently used but evaluating other separate and specific implementations.!


## Create your experiment

1. Create a new branch from this branch and make sure the branch name follows the naming guidelines to get CI and Official Build support. The name should use the `feature/` prefix.

2. Identify whether you need to consume new APIs or features from `dotnet/runtime` and need to be able to consume these on a faster cadence than using a daily SDK build:
    - I don't need to depend on `dotnet/runtime`:
        1. Update the `global.json` file:
             - Specify the minimum required `dotnet` tool and SDK version that you need to build and run tests
             - Remove the `runtimes` section under `tools`
        2. Set `UseCustomRuntimeVersion` property to `false` in `Directory.Build.props`
        3. Remove the `VS.Redist.Common.NetCore.SharedFramework.x64.6.0` dependency from `Version.Details.xml` and the corresponding property from `Version.props`
    - I do need to depend on `dotnet/runtime`:
        1. Set a DARC dependency from `dotnet/runtime` to your branch in this repository. For more information on how to do it, see [here](https://github.com/dotnet/arcade/blob/main/Documentation/Darc.md#darc)


        > Note that if you want to run `dotnet test` in you test projects you will need to either first run `build.cmd/sh` or install the runtime version specified by `MicrosoftNETCoreAppVersion` property in `Versions.props` in your global dotnet install. If you run `build.cmd/sh` arcade infrastructure will make sure that the repo `dotnet` SDK found in `<RepoRoot>\.dotnet` folder, has this runtime installed. Then during the build of the test projects, we generate a `.runsettings` file that points to this `dotnet` SDK.


> For both options above, you can choose whether your experiment needs arcade latest features, to do that, set the required DARC subscription from `dotnet/arcade` to this repository following these [instructions](https://github.com/dotnet/arcade/blob/main/Documentation/Darc.md#darc).

3. Set the right version for your library. In order to do that, set the following properties in `Versions.props`:
    - `VersionPrefix`: the version prefix for the produced nuget package.
    - `MajorVersion/MinorVersion/PatchVersion`: Properties that control file version.
    - `PreReleaseVersionLabel`: this is the label that your package will contain when producing a non stable package. i.e: `MyExperiment.1.0.0-alpha-23432.1.nupkg`.

4. Choose the right set of platforms for CI and Official Builds by tweaking `eng/pipelines/runtimelab.yml` file.

5. Rename `Experimental.sln`, `Experimental.csproj` and `Experimental.Tests.csproj` to your experiment name.

The package produced from your branch will be published to the the [`dotnet-experimental`](https://dev.azure.com/dnceng/public/_packaging?_a=feed&feed=dotnet-experimental) feed.

## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## License

.NET (including the runtime repo) is licensed under the [MIT](LICENSE.TXT) license.
