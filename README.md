# .NET Runtime - Managed Zlib

This is an experimental managed version, based on madler/zlib and deflate64 project, of ZLib's library, for exploring the idea of owning a competitive compression library that would follow the same cross-platform vision as .NET while having more consistent performance and compression characteristics. The project includes [Deflate algorithm](https://www.zlib.net/feldspar.html)'s implementation for raw inflate, based on [Deflate64](https://github.com/dotnet/runtime/tree/main/src/libraries/System.IO.Compression/src/System/IO/Compression/DeflateManaged), and raw deflate, based on [Zlib](https://github.com/madler/zlib), with a test harness for performance and functionality.

Consuming an external dependency, like [Zlib](https://github.com/madler/zlib) or [Zlib-intel](https://github.com/intel/zlib), involves high-maintenance cross-platform and .NET versions, being shipped differently for Windows and Linux systems, and a vulnerability when it comes to fix reported issues or bugs. This library is being consumed by compression solutions like [DeflateStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.deflatestream?view=net-7.0), [GZipStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.gzipstream?view=net-7.0) , [ZLibStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.zlibstream?view=net-7.0) and [ZipArchive](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.ziparchive?view=net-7.0), among others.

The ManagedZLib project's structure goes as following:

+ [Source](src/Microsoft.ManagedZLib/src/) - Implementations of raw inflate and deflate following the [(RFC151)](https://datatracker.ietf.org/doc/html/rfc1951) standard and [ZLib's manual](https://www.zlib.net/manual.html).
+ [Tests](src/Microsoft.ManagedZLib/tests/) - Unit tests for functionality coverage of DeflateStream's deflater and inflater on scenarios like: Text files, pdf (text heavy) and the binary files.
+ [Benchmarks](src/Microsoft.ManagedZLib/benchmarks/) - Uses BenchmarkDotNet for comparing the performance of this project, using [Zlib](https://github.com/madler/zlib) or [Zlib-intel](https://github.com/intel/zlib) as baseline.
+ [Profiling](src/Microsoft.ManagedZLib/) - Profiling console app for exploring areas of performance improvement, using VS Profiler.


## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## License

.NET (including the runtime repo) is licensed under the [MIT](LICENSE.TXT) license.
