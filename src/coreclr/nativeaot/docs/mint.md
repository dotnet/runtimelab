# Mint 🍃

## Project structure

There is a new managed assembly [`System.Private.Mint`](../System.Private.Mint/src/) and a native library `libmint.a` (in [Runtme/mint](../Runtime/mint/))

The managed build defines `FEATURE_MINT` (for example to build CoreLib with the right bits of reflection included) in all the nativeaot managed projects, and also `MINT_IMPLEMENTATION` in `System.Private.Mint`.

The native build defines `NATIVEAOT_MINT` which can be used to conditionally compile Mono/NativeAOT code.  There are Mono shims
in `Runtime/mint/inc/monoshim` to make the C compiler happy.

**FIXME** `config.h` - The Mono build auto-generates a `config.h` file based on some `autoconf`-style probing of the system.  In the interest of expediency, we will have a pre-generated `monoshim/config-osx-arm64.h` with the relevant defines, and as a consequence
the project only builds on Apple M1 machines.

## HelloMint - the sample app

For the purpose of the project we will use a sample app: [HelloMint](../../sample/HelloMint/HelloMint.csproj) which is configured to use locally built tools and libraries.

We use this approach to avoid having to build nuget packages for NativeAOT tools and framework libraries.

## How to build

1. Navigate to the sample project directory:
```bash
cd src/coreclr/sample/HelloMint
```

2. Build the NativeAOT runtime and all required components (by default the runtime is build in `Debug` configuration)
``` bash
make runtime
```

3. Build the `HelloMint` sample app
``` bash
make publish
```

4. Run the sample
``` bash
make run
```

### Additional notes

- A one shot command to build everything:
``` bash
make world
```
- You can also run the sample with Mint disabled with:
``` bash
make run USE_MINT=false
```
- You can build the runtime in `Release` configuration with:
``` bash
make runtime BUILD_CONFIG=Release
```

NOTE: Please inspect `Makefile` to further configure the build as desired.

## * Building the runtime packs
In order to build the `Microsoft.DotNet.ILCompiler.9.0.0-dev.nupkg` BuildIntegration nuget.  (This has the support for adding the `UseInterpreter` property to user projects)

After that if you're just changing the runtime, you can build:

```console
./build.sh clr.aot+libs+packs -rc Debug /p:BuildNativeAOTRuntimePack=true
```

For fast iteration just to validate that code is building, you can do

```
./build.sh clr.aot -rc Debug -b
```

but this will not update the runtime packs for trying it out.

## * Running with runtime packs

1. Install .NET 9 from [dotnet/installer](https://github.com/dotnet/installer)

I recommend installing into a separate directory and setting `DOTNET_ROOT` to point to it and adding it to the path.

2. Edit `sdk/9.0.100-alpha.1.23453.2/Microsoft.NETCoreSdk.BundledVersions.props`
  - Set `KnownILCompilerPack`'s property `ILCompilerPackVersion="9.0.0-dev"`
  - For `KnownRuntimePack` with `RuntimePackNamePatterns="Microsoft.NETCore.App.Runtime.NativeAOT.**RID**"`
    set `LatestRuntimeFrameworkVersion="9.0.0-dev"`

Set `/p:UseInterpreter=true` in the sample .csproj

### Complete example

#### nuget.config

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <!--To inherit the global NuGet package sources remove the <clear/> line below -->
    <clear />
    <add key="dotnet9" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet9/nuget/v3/index.json" />
    <add key="dotnet8" value="https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet8/nuget/v3/index.json" />
    <!-- Change this to your git checkout of the runtime -->
    <add key="local-build" value="/Users/alklig/work/dotnet-runtime/mint/artifacts/packages/Debug/Shipping/" />
  </packageSources>
  <config>
    <add key="globalPackagesFolder" value="./nuget-packages" />
  </config>
</configuration>
```

#### hello.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <PublishAot>true</PublishAot>

    <!-- Mint specific -->
    <UseInterpreter>true</UseInterpreter>
  </PropertyGroup>

  <!-- Use in-tree packages -->
  <ItemGroup>
    <FrameworkReference Update="Microsoft.NETCore.App" RuntimeFrameworkVersion="9.0.0-dev" />
    <PackageReference Include="Microsoft.DotNet.ILCompiler" Version="9.0.0-dev" />
  </ItemGroup>
</Project>
```

#### Program.cs

```csharp
if (AppContext.TryGetSwitch("System.Private.Mint.Enable", out var enabled))
{
        Console.WriteLine ("Hello, Mint is {0}", enabled ? "enabled": "disabled");
} else {
        Console.WriteLine ($"Hello, System.Private.Mint.Enable is unset");
}
```

### Build and run

Using the hacked up dotnet install, publish the `hello.csproj`:

```console
$ dotnet publish hello.csproj
...
Generating native code
...
$ % ./bin/Release/net8.0/osx-arm64/publish/hello
Hello, Mint is enabled
```
