# .NET Runtime Lab

This repo is for experimentation and exploring new ideas that may or may not make it into the main [dotnet/runtime](https://github.com/dotnet/runtime) repo. [Encouraging .NET Runtime Experiments](https://github.com/dotnet/runtime/issues/35609) describes reasons that motivated creating this repository.

## Active Experimental Projects

Currently, this repo contains the following experimental projects:

- [Utf8String](https://github.com/dotnet/runtimelab/tree/feature/Utf8String) - A new UTF-8 String data type in the runtime.
- [FreeBSD](https://github.com/dotnet/runtimelab/tree/feature/FreeBSD) - Port of .NET runtime to FreeBSD
- [NativeAOT-LLVM](https://github.com/dotnet/runtimelab/tree/feature/NativeAOT-LLVM) - LLVM generation for Native AOT compilation (including Web Assembly)
- [NativeAOT-Mint](https://github.com/dotnet/runtimelab/tree/feature/NativeAOT-Mint) - Mono interpreter ported to Native AOT for dynamic execution support
- [ManagedQuic](https://github.com/dotnet/runtimelab/tree/feature/ManagedQuic) - Fully managed implementation of QUIC protocol
- [LLHTTP](https://github.com/dotnet/runtimelab/tree/feature/LLHTTP2) - a  set of flexible, lower-level HTTP APIs.
- [CompatibilityPackages](https://github.com/dotnet/runtimelab/tree/feature/CompatibilityPackages) - a set of packages which help satisfy binary dependencies of .NETFramework assemblies on .NET.
- [Hot-Cold Splitting](https://github.com/dotnet/runtimelab/tree/feature/hot-cold-splitting) - Support the hot-cold splitting optimization in crossgen2.
- [ManagedZLib](https://github.com/dotnet/runtimelab/tree/feature/ManagedZLib) - Fully managed implementation of DEFLATE algorithm and GZip/ZLib envelope formats.
- [Async](https://github.com/dotnet/runtimelab/tree/feature/async2-experiment) - Move support for async state machine generation from the C# compiler to the runtime.
- [SwiftBindings](https://github.com/dotnet/runtimelab/tree/feature/swift-bindings) - Swift bindings for .NET.
- [CoreCLR Interpreter](https://github.com/dotnet/runtimelab/tree/feature/CoreclrInterpreter) - An interpreter for CoreCLR.

You can create your own experiment, learn more [here](CreateAnExperiment.md)!

## Completed Projects

- [s390x](https://github.com/dotnet/runtimelab/tree/feature/s390x) - Port of .NET runtime (Mono) to the s390x architecture. The changes were upstreamed to [dotnet/runtime](https://github.com/dotnet/runtime).
- [DllImportGenerator](https://github.com/dotnet/runtimelab/tree/feature/DllImportGenerator) - Roslyn Source Generator used for generating P/Invoke IL stubs.
- [NativeAOT](https://github.com/dotnet/runtimelab/tree/feature/NativeAOT) - .NET runtime optimized for ahead of time compilation.
- [JsonCodeGen](https://github.com/dotnet/runtimelab/tree/feature/JsonCodeGen) - Code generation for JSON.
- [AssemblyBuilder.Save()](https://github.com/dotnet/runtimelab/tree/feature/assembly-builder-save) - Prototyping the implementation of AssemblyBuilder.Save() functionality.
- [Green Threads](https://github.com/dotnet/runtimelab/tree/feature/green-threads) - Prototyping the implementation of green threads.
- [NativeAOT for Android](https://github.com/dotnet/runtimelab/tree/feature/nativeaot-android) - Prototyping the Native AOT for Android apps and Java interop layer.

## Filing issues

This repo should contain issues that are tied to the experiments hosted here.

For other issues, please use the following repos:

- For .NET Runtime issues, file in the [dotnet/runtime](https://github.com/dotnet/runtime) repo
- For .NET SDK issues, file in the [dotnet/sdk](https://github.com/dotnet/sdk) repo
- For ASP.NET issues, file in the [dotnet/aspnetcore](https://github.com/dotnet/aspnetcore) repo.

## Reporting security issues and security bugs

Security issues and bugs should be reported privately, via email, to the Microsoft Security Response Center (MSRC) <secure@microsoft.com>. You should receive a response within 24 hours. If for some reason you do not, please follow up via email to ensure we received your original message. Further information, including the MSRC PGP key, can be found in the [Security TechCenter](https://www.microsoft.com/msrc/faqs-report-an-issue).

Also see info about related [Microsoft .NET Core and ASP.NET Core Bug Bounty Program](https://www.microsoft.com/msrc/bounty-dot-net-core).

## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## License

.NET (including the runtime repo) is licensed under the [MIT](LICENSE.TXT) license.
