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
    -a, --swiftabi     Required. Path to the Swift ABI file.
    -d, --dylib        Required. Path to the dynamic library.
    -t, --tbd          Required. Path to the TBD file.
    -o, --output       Required. Output directory for generated bindings.
    -v, --verbose      Verbosity level. 0 = No logging, 1 = General information, 2 = Debugging information. (default: 1)
```

## Supported scenarios

StoreKit in-app purchase example:

```csharp
SwiftArray<SwiftString> productIdentifiers = new SwiftArray<SwiftString>();
productIdentifiers.Append(new SwiftString("id1"));
productIdentifiers.Append(new SwiftString("id2"));
Task<SwiftArray<SwiftString>> productsTask = Product.products<SwiftArray<SwiftString>>(productIdentifiers);
SwiftArray<Product> products = await productsTask;

Product product = products[0];
await product.purchase(new SwiftSet<Product.PurchaseOption>());
```

A complete list of supported scenarios is available in `src/Swift.Bindings/tests`.

If an unsupported syntax element is encountered in the ABI file, the tooling will ignore it and generate C# source code for known syntax elements. The generated C# bindings are published as source files to the output directory, allowing users to modify them before compilation.

## Experimental packages

NuGet feed: https://dev.azure.com/dnceng/public/_artifacts/feed/dotnet-experimental

StoreKit bindings:
```
Swift.Bindings.MacOSX.Experimental -version 1.0.0-alpha.25201.1
Swift.Bindings.MacCatalyst.Experimental -version 1.0.0-alpha.25201.1
Swift.Bindings.iPhoneSimulator.Experimental -version 1.0.0-alpha.25201.1
Swift.Bindings.iPhoneOS.Experimental -version 1.0.0-alpha.25201.1
Swift.Bindings.AppleTVSimulator.Experimental -version 1.0.0-alpha.25201.1
Swift.Bindings.AppleTVOS.Experimental -version 1.0.0-alpha.25201.1
```

Projection tooling:
```
Swift.Bindings -version 1.0.0-alpha.25201.1
```

## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## License

.NET (including the runtime repo) is licensed under the [MIT](LICENSE.TXT) license.
