# .NET Runtime Lab

This repo is for experimentation and exploring new ideas that may or may not make it into the main [dotnet/runtime](https://github.com/dotnet/runtime) repo. [Encouraging .NET Runtime Experiments](https://github.com/dotnet/runtime/issues/35609) describes reasons that motivated creating this repository.

## Active Experimental Projects

Currently, this repo contains the following experimental projects:

- [DllImportGenerator](https://github.com/dotnet/runtimelab/tree/feature/DllImportGenerator) - Roslyn Source Generator used for generating P/Invoke IL stubs.
- [JsonCodeGen](https://github.com/dotnet/runtimelab/tree/feature/JsonCodeGen) - Code generation for JSON.
- [NativeAOT](https://github.com/dotnet/runtimelab/tree/feature/NativeAOT) - .NET runtime optimized for ahead of time compilation.
- [Utf8String](https://github.com/dotnet/runtimelab/tree/feature/Utf8String) - A new UTF-8 String data type in the runtime.
- [RegexSRM](https://github.com/dotnet/runtimelab/tree/feature/regexsrm) - Incorporating MSR's Symbolic Regex Matcher (SRM) into System.Text.RegularExpressions
- [FreeBSD](https://github.com/dotnet/runtimelab/tree/feature/FreeBSD) - Port of .NET runtime to FreeBSD
- [s390x](https://github.com/dotnet/runtimelab/tree/feature/s390x) - Port of .NET runtime (Mono) to the s390x architecture
- [NativeAOT-LLVM](https://github.com/dotnet/runtimelab/tree/feature/NativeAOT-LLVM) - LLVM generation for Native AOT compilation (including Web Assembly)
- [ManagedQuic](https://github.com/dotnet/runtimelab/tree/feature/ManagedQuic) - Fully managed implementation of QUIC protocol
- [LLHTTP](https://github.com/dotnet/runtimelab/tree/feature/LLHTTP2) - a  set of flexible, lower-level HTTP APIs.
- [RegexSourceGenerator](https://github.com/dotnet/runtimelab/tree/feature/RegexSourceGenerator) - Code generator for compiled regular expressions.

You can create your own experiment, learn more [here](CreateAnExperiment.md)!

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
