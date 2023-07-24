// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.ManagedZLib.Benchmarks;

// Referring the dotnet/performance documentation (performance/docs/microbenchmark-design-guidelines.md)
// BenchmarkDotNet creates a type which derives from type with benchmarks. 
// So the type with benchmarks must not be sealed and it can NOT BE STATIC 
// and it has to BE PUBLIC. It also has to be a class (no structs support).
public class ManagedZLibBenchmark
{
    public static IEnumerable<string> UncompressedTestFileNames()
    {
        yield return "TestDocument.pdf"; // 199 KB small test document with repeated paragraph, PDF are common
        yield return "alice29.txt"; // 145 KB, copy of "ALICE'S ADVENTURES IN WONDERLAND" book, an example of text file
        yield return "sum"; // 37.3 KB, some binary content, an example of binary file
    }

    public CompressedFile? CompressedFile;
    private MemoryStream _outputStream;

    [GlobalSetup]
    public void Setup()
    {
        Debug.Assert(File != null);
        CompressedFile = new CompressedFile(File, Level);
        _outputStream = new MemoryStream(UncompressedData.Length); 
    }


    [ParamsSource(nameof(UncompressedTestFileNames))]
    public string? File { get; set; }

    [Params(System.IO.Compression.CompressionLevel.SmallestSize,
            System.IO.Compression.CompressionLevel.Optimal,
            System.IO.Compression.CompressionLevel.Fastest)] // we don't test the performance of CompressionLevel.NoCompression on purpose
    public System.IO.Compression.CompressionLevel Level { get; set; }


    [GlobalCleanup]
    public void Cleanup() => CompressedFile?.CompressedDataStream.Dispose();

    [Benchmark(Baseline = true)]
    public void DecompressNative()
    {
        CompressedFile!.CompressedDataStream.Position = 0;
        _outputStream.Position = 0;
        
        System.IO.Compression.DeflateStream decompressor = new System.IO.Compression.DeflateStream(CompressedFile.CompressedDataStream, System.IO.Compression.CompressionMode.Decompress);
        decompressor.CopyTo(_outputStream);
    }

    [Benchmark]
    public void DecompressManaged()
    {
        CompressedFile!.CompressedDataStream.Position = 0;
        _outputStream.Position = 0;

        DeflateStream decompressor = new DeflateStream(CompressedFile.CompressedDataStream, CompressionMode.Decompress);
        decompressor.CopyTo(_outputStream);
    }

    public class ProgramRun
    {
        static void Main() => BenchmarkRunner.Run<ManagedZLibBenchmark>();
    }

}
