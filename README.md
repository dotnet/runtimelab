# .NET Runtime - Hot/Cold Splitting
This branch contains an experimental fork of the CoreCLR [.NET runtime](https://github.com/dotnet/runtime) with hot/cold code splitting enabled for Crossgen2. By separating frequently-run (or "hot") code sections from infrequently-run (or "cold") sections, we can increase hot code density, thus improving cache locality and reducing page faults. This feature is currently a collaboration between 2022 summer interns on the .NET JIT and runtime teams. Current efforts are focused specifically on supporting x64 end-to-end.

## Documentation
* While somewhat JIT-specific, the [Summer 2022 Intern Project Proposal](https://microsoft.sharepoint.com/:w:/t/netfx/CLR/JIT/EWLYVNr08DVLq8tvGeajBz4BVO0abH5bc6UI_X6qjjiU8g?e=6XNRXS) explains the background and motivations for hot/cold splitting.
* Andrew Au's [initial PR](https://github.com/dotnet/runtimelab/pull/1900) documents remaining work items and issues known from the experiment's start.

## How to Engage
We welcome your feedback. Relevant issues tagged `area-hot-cold-splitting` are a good place to start the conversation. Feel free to reach out to us:
* [Andrew Au](https://github.com/cshung) owns this experiment.
* [Eugenio Peña](https://github.com/EugenioPena) and [Aman Khalid](https://github.com/amanasifkhalid) are interns on the runtime and JIT teams, respectively.

---

# .NET Runtime
[![Build Status](https://dnceng.visualstudio.com/public/_apis/build/status/dotnet/runtime/runtime?branchName=main)](https://dnceng.visualstudio.com/public/_build/latest?definitionId=686&branchName=main)
[![Help Wanted](https://img.shields.io/github/issues/dotnet/runtime/help%20wanted?style=flat-square&color=%232EA043&label=help%20wanted)](https://github.com/dotnet/runtime/labels/help%20wanted)
[![Gitter](https://badges.gitter.im/Join%20Chat.svg)](https://gitter.im/dotnet/runtime)
[![Discord](https://img.shields.io/discord/732297728826277939?style=flat-square&label=Discord&logo=discord&logoColor=white&color=7289DA)](https://aka.ms/dotnet-discord)

This repo contains the code to build the .NET runtime, libraries and shared host (`dotnet`) installers for
all supported platforms, as well as the sources to .NET runtime and libraries.

## What is .NET?

Official Starting Page: https://dotnet.microsoft.com

* [How to use .NET](https://docs.microsoft.com/dotnet/core/get-started) (with VS, VS Code, command-line CLI)
  * [Install official releases](https://dotnet.microsoft.com/download)
  * [Install daily builds](docs/project/dogfooding.md)
  * [Documentation](https://docs.microsoft.com/dotnet/core) (Get Started, Tutorials, Porting from .NET Framework, API reference, ...)
    * [Deploying apps](https://docs.microsoft.com/dotnet/core/deploying)
  * [Supported OS versions](https://github.com/dotnet/core/blob/main/os-lifecycle-policy.md)
* [Roadmap](https://github.com/dotnet/core/blob/main/roadmap.md)
* [Releases](https://github.com/dotnet/core/tree/main/release-notes)

## How can I contribute?

We welcome contributions! Many people all over the world have helped make this project better.

* [Contributing](CONTRIBUTING.md) explains what kinds of contributions we welcome
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
* [API Reference docs](https://docs.microsoft.com/dotnet/api)
* [.NET API Catalog](https://apisof.net) (incl. APIs from daily builds and API usage info)
* [API docs writing guidelines](https://github.com/dotnet/dotnet-api-docs/wiki) - useful when writing /// comments
* [.NET Discord Server](https://aka.ms/dotnet-discord) - a place to discuss the development of .NET and its ecosystem

## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

There are many .NET related projects on GitHub.

- [.NET home repo](https://github.com/Microsoft/dotnet) - links to 100s of .NET projects, from Microsoft and the community.
- [ASP.NET Core home](https://docs.microsoft.com/aspnet/core) - the best place to start learning about ASP.NET Core.

This project has adopted the code of conduct defined by the [Contributor Covenant](https://contributor-covenant.org) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](https://www.dotnetfoundation.org/code-of-conduct).

General .NET OSS discussions: [.NET Foundation Discussions](https://github.com/dotnet-foundation/Home/discussions)

## License

.NET (including the runtime repo) is licensed under the [MIT](LICENSE.TXT) license.
