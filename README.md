# .NET Swift interop tooling

This is a collection of tools designed to consume a compiled Apple Swift library and generate bindings and wrappers that enable it to be used as a .NET library.

## Installation and usage

The tool will be available as a NuGet CLI package in the [`dotnet-experimental`](https://dev.azure.com/dnceng/public/_packaging?_a=feed&feed=dotnet-experimental) feed.

Options:
```
  -d, --dylibs             Required. Paths to the dynamic libraries (dylibs), separated by commas.
  -s, --swiftinterfaces    Required. Paths to the Swift interface files, separated by commas.
  -o, --output             Required. Output directory for generated bindings.
  -h, --help               Display this help message.
  --version                Display version information.
```

## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## License

.NET (including the runtime repo) is licensed under the [MIT](LICENSE.TXT) license.
