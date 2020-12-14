# Building a Hello World console app with CoreRT

CoreRT is an AOT-optimized .NET Core runtime. This document will guide you through compiling a .NET Core Console application with CoreRT.

_Please ensure that [pre-requisites](../prerequisites.md) are installed._

## Create .NET Core Console project
Open a new shell/command prompt window and run the following commands.
```bash
> dotnet new console -o HelloWorld
> cd HelloWorld
```

This will create a simple Hello World console app in `Program.cs` and associated project files.

## Add CoreRT to your project
To use CoreRT with your project, you need to add a reference to the ILCompiler NuGet package that contains the CoreRT ahead of time compiler and runtime.
For the compiler to work, it first needs to be added to your project.

In your shell/command prompt navigate to the root directory of your project and run the command:

```bash
> dotnet new nuget 
```

This will add a nuget.config file to your application. Open the file and in the ``<packageSources> `` element under ``<clear/>`` add the following:

```xml
<add key="dotnet-experimental" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet-experimental/nuget/v3/index.json" />
<add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
```

Once you've added the package source, add a reference to the compiler by running the following command:

```bash
> dotnet add package Microsoft.DotNet.ILCompiler -v 6.0.0-*
```

## Restore and Publish your app

Once the package has been successfully added it's time to compile and publish your app! In the shell/command prompt window, run the following command:

```bash
> dotnet publish -r <RID> -c <Configuration>
```

where `<Configuration>` is your project configuration (such as Debug or Release) and `<RID>` is the runtime identifier (one of win-x64, linux-x64, osx-x64). For example, if you want to publish a release configuration of your app for a 64-bit version of Windows the command would look like:

```bash 
> dotnet publish -r win-x64 -c release
```

Once completed, you can find the native executable in the root folder of your project under `/bin/<Configuration>/net5.0/<RID>/publish/`. Navigate to `/bin/<Configuration>/net5.0/<RID>/publish/` in your project folder and run the produced native executable.

Feel free to modify the sample application and experiment. However, keep in mind some functionality might not yet be supported in CoreRT. Let us know on the [Issues page](https://github.com/dotnet/runtimelab/issues).

## Know Issues

It's recommended to target `net5.0` or newer when building CoreRT apps. However, if for some reason you have to stay with older .NET Core version like `netcoreapp2.1`, you might get the following error:

```log
Project is targeting runtime 'win-x64' but did not resolve any runtime-specific packages for the 'Microsoft.NETCore.App' package.  This runtime may not be supported by .NET Core. 
```

In such case, you need to add following setting to your project file:

```xml
<EnsureNETCoreAppRuntime>false</EnsureNETCoreAppRuntime>
```
