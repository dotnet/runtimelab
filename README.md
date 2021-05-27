# System.Net.Http.LowLevel

This branch contains the System.Net.Http.Lowlevel package.

> Note: This library is an experiment and should not be used in production code. Furthermore it is intended to rapidly evolve its API through the .NET 7 timeframe.

This implements LLHTTP, a high-performance HTTP API that puts the caller in full control over requests as outlined in the [Flexible HTTP proposal](https://github.com/dotnet/designs/blob/main/accepted/2021/flexible-http.md).

This package targets the following usages:

- As an implementation detail of a library/API that uses HTTP, to give better performance relative to `HttpClient`.
- Scenarios where HttpClient doesn't allow enough control over how a request is processed.
- Scenarios which are willing to trade ease of development for higher performance.

## Examples

TODO: adapt examples from https://github.com/scalablecory/NetworkToolkit

## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## License

.NET (including the runtime repo) is licensed under the [MIT](LICENSE.TXT) license.
