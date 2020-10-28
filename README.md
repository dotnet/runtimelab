# MsQuic for .NET 5

This branch contains sources for the System.Net.Experimental.MsQuic package which lights up HTTP/3 support in .NET 5.

## Usage

> **Note**: HTTP/3 is experimental in .NET 5 and is not supported in production environments.

Add the package System.Net.Experimental.MsQuic to your .NET 5 project that intends to use HTTP/3.

> TODO: examples for HttpClient and Kestrel.

## Building

- clone the repo recursively or run `git submodule update --init --recursive` to get all the submodules.
- run build.sh or build.cmd

### Manual build instructions

> TODO: remove once build.cmd works.

- clone the repo recursively or run `git submodule update --init --recursive` to get all the submodules.
- In src\msquic, build MsQuic for both x86 and x64 (Windows only):
	- [Ensure prerequisites are installed](https://github.com/microsoft/msquic/blob/dc2a6cf0dd12e273710843158b2f2e612360da0a/docs/BUILD.md).
	- `.\scripts\build.ps1 -Config Release -Arch x86`
	- `.\scripts\build.ps1 -Config Release -Arch x64`
- In src\System.Net.Experimental.MsQuic, build the NuGet package:
	- `nuget pack .\System.Net.Experimental.MsQuic.nuspec`

## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## License

.NET (including the runtime repo) is licensed under the [MIT](LICENSE.TXT) license.
