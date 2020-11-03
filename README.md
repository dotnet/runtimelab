# .NET Runtime - Managed QUIC implementation

## Supported QUIC Protocol Features

This implementation is highly in WIP state. The features are at the draft-27 version. The goal was
to obtain a minimal working implementation able to reliably transmit data between client and server.
This can be used to evaluate the viability of purely C# implementation for production releases. Some
transport-unrelated features are left unimplemented.

Currently implemented:

- Basic connection establishment
- Encryption
- TLS integration backed by [modified OpenSSL](https://github.com/openssl/openssl/pull/8797)
- Sending data, flow control, loss recovery, congestion control
- Stream/Connection termination
- Coalescing packets into a single UDP datagram

## Unsupported QUIC Protocol Features

A list of highlights of unimplemented protocol features follows:

- Connection migration
- Stateless reset
- 0-RTT data
- Server Preferred Address
- Version Negotiation
- Address Validation
- Encryption key updates

## OpenSSL integration

To function correctly, the implementation requires a custom branch of OpenSSL from Akamai
https://github.com/akamai/openssl/tree/OpenSSL_1_1_1g-quic. The implementation tries to detect that
a QUIC-supporting OpenSSL is present in the path. If not, then a mock implementation is used
instead.

The mock implementation can be used for trying the implementation locally, but interoperation with
other QUIC implementations is, of course, possible only with the OpenSSL-based TLS. If you want to
make sure your implementation runs with OpenSSL, define the `DOTNETQUIC_OPENSSL` environment
variable. This will cause the implementation to throw if proper OpenSSL version cannot be loaded.

## Tracing and qvis

To simplify debugging, this implementation can emit traces that can be analyzed. Note that emiting
traces has negative impact on the performance of the implementation.

### Console

To get traces printed to the console, define `DOTNETQUIC_TRACE` environment variable to `console`.

### qlog and qvis

The implementation can produce traces that can be consumed by https://qvis.edm.uhasselt.be
visualizer. To collect traces, define `DOTNETQUIC_TRACE` environment variable to `qlog`.
The traces will be saved in the working directory in a file named
_[timestamp]-[server|client].qvis_.

## Disabling encryption

Regardless of the TLS used, you can circumvent the encryption by defining the `DOTNETQUIC_NOENCRYPT`
environment variable to something nonempty.

# .NET Runtime

[![Build Status](https://dnceng.visualstudio.com/public/_apis/build/status/dotnet/runtime/runtime?branchName=master)](https://dnceng.visualstudio.com/public/_build/latest?definitionId=686&branchName=master)
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
