# .NET Runtime Lab

This repo is for experimentation and exploring new ideas that may or may not make it into the main [dotnet/runtime](https://github.com/dotnet/runtime) repo.

## Active Experimental Projects

Currently, this repo contains the following experimental projects:

- [DllImportGenerator](https://github.com/dotnet/runtimelab/tree/DllImportGenerator) - Roslyn Source Generator used for generating P/Invoke IL stubs.
- [JsonCodeGen](https://github.com/dotnet/runtimelab/tree/JsonCodeGen) - Code generation for JSON.
- [NativeAOT](https://github.com/dotnet/runtimelab/tree/NativeAOT) - .NET runtime optimized for ahead of time compilation. 
- [Utf8String](https://github.com/dotnet/runtimelab/tree/Utf8String) - A new UTF-8 String data type in the runtime.

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
