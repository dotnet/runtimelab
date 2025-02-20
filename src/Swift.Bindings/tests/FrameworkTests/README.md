# Framework tests

Framework tests are excluded from the local build because they require the MAUI workload. This file provides instructions on how to set up environment and run tests locally.

## Prerequisites

Ensure you have the following installed:

- .NET 9 SDK (version `9.0.102`)
- Xcode 16.2
- MAUI workloads
- `xharness` CLI tool

## Setup instructions

**Update the `global.json` file to specify the required .NET SDK version:**

```json
{
    "sdk": {
    "version": "9.0.102",
    "allowPrerelease": true,
    "rollForward": "major"
    },
    "tools": {
    "dotnet": "9.0.102",
    "runtimes": {
        "dotnet": [
        "$(MicrosoftNETCoreAppVersion)"
        ]
    }
    },
    "msbuild-sdks": {
    "Microsoft.DotNet.Arcade.Sdk": "10.0.0-beta.24515.3"
    }
}
```

**Install dependencies**:

Install .NET 9 SDK:
```sh
./build.sh
```
Install MAUI workloads:
```sh
./dotnet.sh workload install maui maccatalyst --version 9.0.103 --source https://api.nuget.org/v3/index.json
```
Install XHarness locally:
```sh
./dotnet.sh new tool-manifest && ./dotnet.sh tool install microsoft.dotnet.xharness.cli --version "9.0.0-prerelease*"
```
Select Xcode 16.2:
```sh
sudo xcode-select -s /Applications/Xcode_16.2.app/Contents/Developer
```

## Building the app

To build the test application, use the following command:

```sh
./dotnet.sh build ./src/Swift.Bindings/tests/FrameworkTests/Swift.Bindings.Framework.Tests.csproj \
-f net9.0-maccatalyst \
-c Release \
-r arm64-maccatalyst \
/p:UseMaui=true
```

## Running the tests

Run the tests on an Apple device using `xharness`:

```sh
./dotnet.sh xharness apple test \
--target maccatalyst \
--timeout="00:02:00" \
--launch-timeout=00:06:00 \
--app ./src/Swift.Bindings/tests/FrameworkTests/bin/Release/net9.0-maccatalyst/maccatalyst-arm64/Swift.Bindings.Framework.Tests.app \
--output-directory artifacts
```

## Output directory

Test results and logs will be stored in the `artifacts` directory.
