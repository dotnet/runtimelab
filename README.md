# Compatibility packages

This project is meant to provide a set of packages which help satisfy binary dependencies of .NETFramework assemblies on .NET.

These assemblies aren't meant to be "real" or full implementations of their .NETFramework predecessors, they are only meant to satisfy the surface area in order to permit old libraries which depended on them to load and JIT without loader or type system exceptions.

## System.Data.Linq

System.Data.Linq is the .NETFramework assembly which exposed [Linq to SQL](https://docs.microsoft.com/en-us/dotnet/framework/data/adonet/sql/linq/).  This technology is not supported on .NETCore.

This project provides a version of this assembly which will throw `PlatformNotSupportedException` when accessing most API.

## System.Web.Compatibility

System.Web is the .NETFramework assembly which exposed the ASP.NET app model.  This app model is not supported on .NETCore and was replaced with [ASP.NETCore](https://github.com/dotnet/aspnetcore) so it's not possible to support this assembly in its entirety.  Only one type from this assembly exists on .NETCore [System.Web.HttpUtility](https://docs.microsoft.com/en-us/dotnet/api/system.web.httputility) so we carry a copy of this assembly in .NETCore with a single type forward for this one type.

This project is a replacement for that copy that takes precedence over the inbox copy and implements more types.  It fills in the entire surface area from System.Web in order to satisfy the JIT compiler and never throw `MissingMethodException`, `TypeNotFoundException`, or others when JIT'ing code which depends on System.Web.  These implementations will all throw `PlatormNotSupportedException` when called, except for a select subset which can return a default value that is useful for code that might only need to detect when not in an ASP.NET application.

It is a non-goal of this project to provide any real, functionally equivalent implementation of System.Web types, especially not a full implementation of ASP.NET.

## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## License

.NET (including the runtime repo) is licensed under the [MIT](LICENSE.TXT) license.
