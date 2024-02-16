# .NET Swift interop tooling

This is a collection of tools designed to consume a compiled Apple Swift library and generate bindings and wrappers that enable it to be used as a .NET library.

## Installation and usage

The tool will be available as a NuGet CLI package in the [`dotnet-experimental`](https://dev.azure.com/dnceng/public/_packaging?_a=feed&feed=dotnet-experimental) feed.

Options:
```
  -d, --dylib             Required. Path to the dynamic library.
  -s, --swiftinterface    Required. Path to the Swift interface file.
  -o, --output            Required. Output directory for generated bindings.
  -v, --verbose           Information about work in process.
  -h, --help              Display this help message.
```

## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## License

.NET (including the runtime repo) is licensed under the [MIT](LICENSE.TXT) license.
