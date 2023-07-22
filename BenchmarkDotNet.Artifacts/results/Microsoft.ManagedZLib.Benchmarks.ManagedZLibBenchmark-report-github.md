``` ini

BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.22621.1992/22H2/2022Update/SunValley2)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=8.0.100-preview.5.23303.2
  [Host]     : .NET 8.0.0 (8.0.23.28008), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.28008), X64 RyuJIT AVX2


```
|            Method |        Level |             File |     Mean |    Error |   StdDev | Ratio | RatioSD |
|------------------ |------------- |----------------- |---------:|---------:|---------:|------:|--------:|
|  DecompressNative | SmallestSize | TestDocument.pdf | 52.47 μs | 0.519 μs | 0.460 μs |  0.93 |    0.04 |
| DecompressManaged | SmallestSize | TestDocument.pdf | 56.27 μs | 1.123 μs | 2.394 μs |  1.00 |    0.00 |
