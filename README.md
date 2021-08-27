# .NET Runtime - Native AOT

This branch contains experimental fork of CoreCLR [.NET runtime](http://github.com/dotnet/runtime) optimized for the [Native AOT Form factor](https://github.com/dotnet/designs/blob/main/accepted/2020/form-factors.md#native-aot-form-factors). The ahead-of-time (AOT) toolchain can compile .NET application into a native (architecture specific) single-file executable. It can also produce standalone dynamic or static libraries that can be consumed by applications written in other programming languages.  This branch contains the experimental feature of compiling to LLVM to target Web Assembly at present and other LLVM targets in the future.

## Samples

The packages for Linux, macOS and Windows x64 are published to a dedicated NuGet feed after each pull request. Using this AOT compiler and runtime is as simple as adding a new package reference to your .NET project and publishing it. Check out one of our samples: a "[Hello World](samples/HelloWorld)" console app or a [native library](samples/NativeLibrary). The `README.md` file in each sample's directory will guide you through the process step by step.

## Documentation

- [Using Native AOT](docs/using-nativeaot/README.md) explains how to debug, optimize and troubleshoot applications published using the native AOT toolchain. This documentation is for people who are interested in using the toolchain.
- [Developer workflow](docs/workflow/building/coreclr/nativeaot.md) explains how to build the repo, run tests and work on the Native AOT toolchain. This documentation is for people who are interested in making changes in the toolchain.

## How to Engage, Contribute and Provide Feedback
Some of the best ways to contribute are to try things out, file bugs, and join in design conversations.

Looking for something to work on? The [_help wanted_](https://github.com/dotnet/runtimelab/issues?q=is%3Aissue+is%3Aopen+label%3A%22help+wanted%22++label%3Aarea-NativeAOT+) issues are a great place to start.

[![Join the chat at https://gitter.im/dotnet/corert](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/dotnet/corert?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge)

This project is successor of RyuJIT CodeGen from [CoreRT](https://github.com/dotnet/corert) where you can find more samples and older documentation.

---

# .NET Runtime
[![Build Status](https://dnceng.visualstudio.com/public/_apis/build/status/dotnet/runtime/runtime?branchName=main)](https://dnceng.visualstudio.com/public/_build/latest?definitionId=686&branchName=main)
[![Help Wanted](https://img.shields.io/github/issues/dotnet/runtime/up-for-grabs?style=flat-square&color=%232EA043&label=help%20wanted)](https://github.com/dotnet/runtime/issues?q=is%3Aissue+is%3Aopen+label%3A%22up-for-grabs%22)
[![Gitter](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/dotnet/runtime)
[![Discord](https://img.shields.io/discord/732297728826277939?style=flat-square&label=Discord&logo=discord&logoColor=white&color=7289DA)](https://aka.ms/dotnet-discord)

This repo contains the code to build the .NET runtime, libraries and shared host (`dotnet`) installers for
all supported platforms, as well as the sources to .NET runtime and libraries.

## What is .NET?

Official Starting Page: https://dotnet.microsoft.com/

* [How to use .NET](https://docs.microsoft.com/dotnet/core/get-started) (with VS, VS Code, command-line CLI)
  * [Install official releases](https://dotnet.microsoft.com/download)
  * [Install daily builds](docs/project/dogfooding.md)
  * [Documentation](https://docs.microsoft.com/dotnet/core) (Get Started, Tutorials, Porting from .NET Framework, API reference, ...)
    * [Deploying apps](https://docs.microsoft.com/dotnet/core/deploying)
  * [Supported OS versions](https://github.com/dotnet/core/blob/master/os-lifecycle-policy.md)
* [Roadmap](https://github.com/dotnet/core/blob/master/roadmap.md)
* [Releases](https://github.com/dotnet/core/tree/master/release-notes)

## How can I contribute?

We welcome contributions! Many people all over the world have helped make this project better.

* [Contributing](CONTRIBUTING.md) explains what kinds of changes we welcome
- [Workflow Instructions](docs/workflow/README.md) explains how to build and test
* [Get Up and Running on .NET Core](docs/project/dogfooding.md) explains how to get nightly builds of the runtime and its libraries to test them in your own projects.

## Reporting security issues and security bugs

Security issues and bugs should be reported privately, via email, to the Microsoft Security Response Center (MSRC) <secure@microsoft.com>. You should receive a response within 24 hours. If for some reason you do not, please follow up via email to ensure we received your original message. Further information, including the MSRC PGP key, can be found in the [Security TechCenter](https://www.microsoft.com/msrc/faqs-report-an-issue).

Also see info about related [Microsoft .NET Core and ASP.NET Core Bug Bounty Program](https://www.microsoft.com/msrc/bounty-dot-net-core).

## Filing issues

This repo should contain issues that are tied to the runtime, the class libraries and frameworks, the installation of the `dotnet` binary (sometimes known as the `muxer`) and installation of the .NET runtime and libraries.

For other issues, please use the following repos:

- For overall .NET SDK issues, file in the [dotnet/sdk](https://github.com/dotnet/sdk) repo
- For ASP.NET issues, file in the [dotnet/aspnetcore](https://github.com/dotnet/aspnetcore) repo.

## Useful Links

* [.NET Core source index](https://source.dot.net) / [.NET Framework source index](https://referencesource.microsoft.com)
* [API Reference docs](https://docs.microsoft.com/dotnet/api/?view=netcore-3.1)
* [.NET API Catalog](http://apisof.net) (incl. APIs from daily builds and API usage info)
* [API docs writing guidelines](https://github.com/dotnet/dotnet-api-docs/wiki) - useful when writing /// comments
* [.NET Discord Server](https://aka.ms/dotnet-discord) - a place to talk and hang out with .NET community

## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

There are many .NET related projects on GitHub.

- [.NET home repo](https://github.com/Microsoft/dotnet) - links to 100s of .NET projects, from Microsoft and the community.
- [ASP.NET Core home](https://docs.microsoft.com/aspnet/core/?view=aspnetcore-3.1) - the best place to start learning about ASP.NET Core.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

General .NET OSS discussions: [.NET Foundation forums](https://forums.dotnetfoundation.org)

## License

.NET (including the runtime repo) is licensed under the [MIT](LICENSE.TXT) license.
