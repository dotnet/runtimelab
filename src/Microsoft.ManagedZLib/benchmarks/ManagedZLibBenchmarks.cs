// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Perfolizer.Horology;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.ManagedZLib.Benchmarks;

// Referring the dotnet/performance documentation (performance/docs/microbenchmark-design-guidelines.md)
// BenchmarkDotNet creates a type which derives from type with benchmarks. 
// So the type with benchmarks must not be sealed and it can NOT BE STATIC 
// and it has to BE PUBLIC. It also has to be a class (no structs support).
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ManagedZLibBenchmark
{
    public static IEnumerable<string> UncompressedTestFileNames()
    {
        yield return "TestDocument.pdf"; // 199 KB small test document with repeated paragraph, PDF are common
        yield return "alice29.txt"; // 145 KB, copy of "ALICE'S ADVENTURES IN WONDERLAND" book, an example of text file
        yield return "sum"; // 37.3 KB, some binary content, an example of binary file
    }

    public CompressedFile? CompressedFile;
    private MemoryStream? outputStream;
    System.IO.Compression.DeflateStream? decompressorN;
    DeflateStream? decompressorM;

    [GlobalSetup]
    public void Setup()
    {
        Debug.Assert(File != null);
        CompressedFile = new CompressedFile(File, Level);
        outputStream = new MemoryStream(CompressedFile.UncompressedData.Length);
        decompressorN = new System.IO.Compression.DeflateStream(CompressedFile.CompressedDataStream, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true);
        decompressorM = new DeflateStream(CompressedFile.CompressedDataStream, CompressionMode.Decompress, leaveOpen: true);
    }


    [ParamsSource(nameof(UncompressedTestFileNames))]
    public string? File { get; set; }

    [Params(System.IO.Compression.CompressionLevel.SmallestSize,
            System.IO.Compression.CompressionLevel.Optimal,
            System.IO.Compression.CompressionLevel.Fastest)]
    public System.IO.Compression.CompressionLevel Level { get; set; }

    [BenchmarkCategory("Creation"), Benchmark(Baseline = true)]
    public void Init_DecompressNative()
    {
        CompressedFile!.CompressedDataStream.Position = 0;
        outputStream!.Position = 0;
        System.IO.Compression.DeflateStream decompressor = new System.IO.Compression.DeflateStream(CompressedFile.CompressedDataStream, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true);
        decompressor?.CopyTo(outputStream);
    }

    //[Benchmark]
    [BenchmarkCategory("Creation"), Benchmark]
    public void Init_DecompressManaged()
    {
        CompressedFile!.CompressedDataStream.Position = 0;
        outputStream!.Position = 0;
        DeflateStream decompressor = new DeflateStream(CompressedFile.CompressedDataStream, CompressionMode.Decompress, leaveOpen: true);
        decompressor?.CopyTo(outputStream);
    }

    //[Benchmark(Baseline = true)]
    [BenchmarkCategory("Decompression"), Benchmark(Baseline = true)]
    public void Alg_DecompressNative()
    {
        CompressedFile!.CompressedDataStream.Position = 0;
        outputStream!.Position = 0;
        decompressorN?.CopyTo(outputStream);
    }

    //[Benchmark]
    [BenchmarkCategory("Decompression"), Benchmark]
    public void Alg_DecompressManaged()
    {
        CompressedFile!.CompressedDataStream.Position = 0;
        outputStream!.Position = 0;
        decompressorM?.CopyTo(outputStream);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        outputStream?.Dispose();
        CompressedFile?.CompressedDataStream.Dispose();
        decompressorN?.Dispose();
        decompressorM?.Dispose();
    }

    public class ProgramRun
    {
        public static void Main(string[] args)
        {
            var job = Job.Default
                .WithWarmupCount(1) // 1 warmup is enough for our purpose
                .WithIterationTime(TimeInterval.FromMilliseconds(250)) // the default is 0.5s per iteration, which is slightly too much for us
                .WithMinIterationCount(15)
                .WithMaxIterationCount(20); // we don't want to run more that 20 iterations

            var config = DefaultConfig.Instance
                .AddJob(job.AsDefault());

            BenchmarkSwitcher.FromAssembly(typeof(ProgramRun).Assembly).Run(args, config);
        }
    }

}