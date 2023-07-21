``` ini

BenchmarkDotNet=v0.13.5, OS=Windows 11 (10.0.22621.1992/22H2/2022Update/SunValley2)
11th Gen Intel Core i9-11950H 2.60GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK=8.0.100-preview.5.23303.2
  [Host]     : .NET 8.0.0 (8.0.23.28008), X64 RyuJIT AVX2
  DefaultJob : .NET 8.0.0 (8.0.23.28008), X64 RyuJIT AVX2


```
|     Method |         Level |             File |       Mean |     Error |    StdDev |
|----------- |-------------- |----------------- |-----------:|----------:|----------:|
| **Decompress** |       **Optimal** | **TestDocument.pdf** |  **59.434 μs** | **1.1349 μs** | **1.2143 μs** |
| **Decompress** |       **Optimal** |      **alice29.txt** |   **5.923 μs** | **0.0640 μs** | **0.0599 μs** |
| **Decompress** |       **Optimal** |              **sum** | **100.582 μs** | **1.3466 μs** | **1.1938 μs** |
| **Decompress** |       **Fastest** | **TestDocument.pdf** |  **57.491 μs** | **1.1370 μs** | **1.7017 μs** |
| **Decompress** |       **Fastest** |      **alice29.txt** |   **5.883 μs** | **0.0664 μs** | **0.0589 μs** |
| **Decompress** |       **Fastest** |              **sum** |  **97.853 μs** | **1.7906 μs** | **1.6749 μs** |
| **Decompress** | **NoCompression** | **TestDocument.pdf** |  **51.759 μs** | **0.5304 μs** | **0.4962 μs** |
| **Decompress** | **NoCompression** |      **alice29.txt** |   **5.938 μs** | **0.0763 μs** | **0.0713 μs** |
| **Decompress** | **NoCompression** |              **sum** |   **2.319 μs** | **0.0333 μs** | **0.0278 μs** |
