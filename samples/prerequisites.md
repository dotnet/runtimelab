If you're new to .NET, make sure to visit the [official starting page](http://dotnet.github.io). It will guide you through installing pre-requisites and building your first app.
If you're already familiar with .NET make sure you've [downloaded and installed the .NET 5 SDK](https://www.microsoft.com/net/download/core).

The following pre-requisites need to be installed for building .NET 5 projects with CoreRT.

# Windows

## Install [Visual Studio 2019](https://visualstudio.microsoft.com/downloads/) with Visual C++ Build Tools

The free [Community](https://visualstudio.microsoft.com/vs/community/) edition or higher is recommended.
If you wish to save disk space and do not need Visual Studio IDE, here is the minimal [Build Tools](https://visualstudio.microsoft.com/downloads/#build-tools-for-visual-studio-2019)
edition installation required. First download the [bootstrapper](https://aka.ms/vs/16/release/vs_buildtools.exe) executable.
On Windows 10 version 1803 or later you may use the `curl` tool:

```cmd
curl -L https://aka.ms/vs/16/release/vs_buildtools.exe -o vs_buildtools.exe
```

Then launch the bootstrapper passing the installation path and the two required components (requires elevation):
```cmd
vs_buildtools.exe --installPath C:\VS2019 --add Microsoft.VisualStudio.Component.VC.Tools.x86.x64 Microsoft.VisualStudio.Component.Windows10SDK.19041 --passive --norestart --nocache --lang en-US
```
Alternatively you may launch the bootstrapper without any options and use Visual Studio Installer UI to enable "C++ x64/x86 build tools" and "Windows 10 SDK" individual components.

Notes:
- You may skip the `Windows10SDK` component if you already have Windows 10 SDK installed on your machine.
- To target Windows ARM64, you need to add the `Microsoft.VisualStudio.Component.VC.Tools.ARM64` ("C++ ARM64 build tools") component instead.
- The `--installPath` option affects Build Tools installation only. Visual Studio Installer is always installed into
the `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` directory.

## Install Visual C++ 2019 Redistributable Package
If you have no `vcruntime140.dll` library installed in your `%SystemRoot%\System32` directory, you also need to install Visual C++ 2019 Redistributable package, which the compiler depends on:
- [vc_redist.x64.exe](https://aka.ms/vs/16/release/vc_redist.x64.exe) (for x64 machine)
- [vc_redist.arm64.exe](https://aka.ms/vs/16/release/vc_redist.arm64.exe) (for ARM64 machine)

# Fedora (31+)

* Install `clang` and developer packages for libraries that .NET Core depends on:

```sh
sudo dnf install clang zlib-devel krb5-libs krb5-devel ncurses-compat-libs
```

This was tested on Fedora 31, but will most likely work on lower versions too.

# Ubuntu (16.04+)

* Install `clang` and developer packages for libraries that .NET Core depends on:

```sh
sudo apt-get install clang zlib1g-dev libkrb5-dev
```

# macOS (10.12+)

* Install [Command Line Tools for XCode 8](https://developer.apple.com/xcode/download/) or higher.
