[![Build Status](https://dev.azure.com/dnceng-public/public/_apis/build/status%2Fdotnet%2Fruntimelab%2Fruntimelab?branchName=feature%2Fswift-bindings)](https://dev.azure.com/dnceng-public/public/_build/latest?definitionId=163&branchName=feature%2Fswift-bindings)

# Swift Bindings  (Experimental)

**WARNING**: This package is currently in an experimental phase.

Tooling that can consume a compiled Apple Swift library and generates C# bindings that allow it to be surfaced as a .NET library. The projection tooling is intended for use with C# and any other .NET language is beyond its scope.

## Usage

The tooling can consume a Swift ABI file or a framework name from the standard library. If a framework name is provided, the tool generates the ABI file based on the `.swiftinterface` file. This ABI file contains a JSON representation of the abstract syntax tree of the `.swiftinterface` file. Multiple Swift ABI files and frameworks can be specified for bindings.

```
Description:
  Swift bindings generator.

Usage:
  SwiftBindings [options]

Options:
    -a, --swiftabi, -f, --framework     Required. Path to the Swift ABI file or framework
    -o, --output                        Required. Output directory for generated bindings
    -platform                           Platform, e.g., MacOSX
    -sdk                                SDK version, e.g., 14.4
    -arch                               Architecture, e.g., arm64e
    -target                             Target, e.g., apple-macos
    -v                                  Information about work in process
    -h, --help                          Display a help message
    --version                           Show version information
    -?, -h, --help                      Show help and usage information
```

 If an unsupported syntax element is encountered in the ABI file, the tooling will ignore it and generate C# source code for known syntax elements. The generated C# bindings are published as source files to the output directory, allowing users to modify them before compilation.

## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## License

.NET (including the runtime repo) is licensed under the [MIT](LICENSE.TXT) license.
