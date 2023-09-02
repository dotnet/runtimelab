# .NET Runtime - Managed Zlib

This is an experimental managed version of ZLib which follows the same cross-platform vision as .NET while having more consistent performance and compression characteristics. The project includes [Deflate algorithm](https://www.zlib.net/feldspar.html)'s implementation for raw inflate, based on [Deflate64](https://github.com/dotnet/runtime/tree/6387a9eb56098a889021190747d31f07246dd9f2/src/libraries/System.IO.Compression/src/System/IO/Compression/DeflateManaged), and raw deflate, based on [Zlib](https://github.com/madler/zlib), with a test harness for performance and functionality.

Consuming an external dependency like [Zlib](https://github.com/madler/zlib) or [Zlib-intel](https://github.com/intel/zlib) in .NET is a high maintenance task, since it involves shipping different versions for Windows and Unix systems, as well as having to either wait for vulnerabilities to get fixed upstream when they become public, or having to fix them ourselves, causing our code to potentially diverge from the original sources.

This library is being consumed by compression solutions like [DeflateStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.deflatestream?view=net-7.0), [GZipStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.gzipstream?view=net-7.0) , [ZLibStream](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.zlibstream?view=net-7.0) and [ZipArchive](https://learn.microsoft.com/en-us/dotnet/api/system.io.compression.ziparchive?view=net-7.0), among others.

The ManagedZLib project's structure goes as following:

+ [Source](src/Microsoft.ManagedZLib/src/) - Implementations of raw inflate and deflate following the [(RFC151)](https://datatracker.ietf.org/doc/html/rfc1951) standard and [ZLib's manual](https://www.zlib.net/manual.html).
+ [Tests](src/Microsoft.ManagedZLib/tests/) - Unit tests for functionality coverage of DeflateStream's deflater and inflater on scenarios like: Text files, pdf (text heavy) and the binary files.
+ [Benchmarks](src/Microsoft.ManagedZLib/benchmarks/) - Uses BenchmarkDotNet for comparing the performance of this project, using [Zlib](https://github.com/madler/zlib) or [Zlib-intel](https://github.com/intel/zlib) as baseline.
+ [Profiling](src/Microsoft.ManagedZLib/profiling) - Profiling console app for exploring areas of performance improvement, using VS Profiler.

Note:
+ The optimizations done to this port of inflate were tested in a Windows11,x64 machine.

# Suggestions going forward

+ For improving performance, an optimization would be adding the same type of memoization during the construction of the lookup table, like done in [Zlib's](https://github.com/madler/zlib) `inftrees.c`.

+ Since this port is based on the original ZLib, porting zlib-intel's optimizations into this new version should improve performance. This can extend to more 3rd parties' optimizations, like [ZLib-NG](https://github.com/zlib-ng/zlib-ng) for ARM64 processors.

## .NET Foundation

.NET Runtime is a [.NET Foundation](https://www.dotnetfoundation.org/projects) project.

This project has adopted the code of conduct defined by the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. For more information, see the [.NET Foundation Code of Conduct](http://www.dotnetfoundation.org/code-of-conduct).

## License

.NET (including the runtime repo) is licensed under the [MIT](LICENSE.TXT) license.