// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using Dia2Lib;
using Perfolizer.Horology;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Microsoft.ManagedZLib.Benchmarks;

// Referring the dotnet/performance documentation (performance/docs/microbenchmark-design-guidelines.md)
// BenchmarkDotNet creates a type which derives from type with benchmarks. 
// So the type with benchmarks must not be sealed and it can NOT BE STATIC 
// and it has to be public. It also has to be a class (no structs support).
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ManagedZLibBenchmark
{
    public static IEnumerable<string> UncompressedTestFileNames()
    {
        // The commented file is going to be added later on:
        yield return "TestDocument.pdf"; // 199 KB small test document with repeated paragraph, PDF are common
        yield return "alice29.txt"; // 145 KB, copy of "ALICE'S ADVENTURES IN WONDERLAND" book, an example of text file
        yield return "sum"; // 37.3 KB, some binary content, an example of binary file
    }

    [ParamsSource(nameof(UncompressedTestFileNames))]
    public string? Files { get; set; }

    public byte[]? UncompressedData { get; set; }
    public byte[]? CompressedData { get; set; }

    public MemoryStream? CompressedStrmN { get; set; }
    public MemoryStream? CompressedStrmM { get; set; }

    internal static string GetFilePath(string fileName)
        => Path.Combine("UncompressedTestFiles", fileName);

    [GlobalSetup]
    public void Setup()
    {
        Debug.Assert(Files != null);

        var filePath = GetFilePath(Files); // For compression
        UncompressedData = File.ReadAllBytes(filePath);

        //Managed
        CompressedStrmM = new MemoryStream(capacity: UncompressedData.Length);
        //Native
        CompressedStrmN = new MemoryStream(capacity: UncompressedData.Length);
    }


    [BenchmarkCategory("Smallest"), Benchmark(Baseline = true)]
    public void CompressNative_small() //with creation/disposal of stream
    {
        CompressedStrmN!.Position = 0;
        System.IO.Compression.DeflateStream compressionStream = new(CompressedStrmN, System.IO.Compression.CompressionLevel.SmallestSize, leaveOpen: true);
        compressionStream.Write(UncompressedData!, 0, UncompressedData!.Length);
        compressionStream.Flush();
        compressionStream.Dispose();
    }

    [BenchmarkCategory("Smallest"), Benchmark]
    public void CompressManaged_small()
    {
        CompressedStrmM!.Position = 0;
        DeflateStream compressionStream = new(CompressedStrmM, CompressionLevel.SmallestSize, leaveOpen: true);
        compressionStream.Write(UncompressedData!, 0, UncompressedData!.Length);
        compressionStream.Flush();
        compressionStream.Dispose();
    }

    [BenchmarkCategory("Optimal"), Benchmark(Baseline = true)]
    public void CompressNative_optimal() //with creation/disposal of stream
    {
        CompressedStrmN!.Position = 0;
        System.IO.Compression.DeflateStream compressionStream = new(CompressedStrmN, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true);
        compressionStream.Write(UncompressedData!, 0, UncompressedData!.Length);
        compressionStream.Flush();
        compressionStream.Dispose();
    }

    [BenchmarkCategory("Optimal"), Benchmark]
    public void CompressManaged_optimal()
    {
        CompressedStrmM!.Position = 0;
        DeflateStream compressionStream = new(CompressedStrmM, CompressionLevel.Optimal, leaveOpen: true);
        compressionStream.Write(UncompressedData!, 0, UncompressedData!.Length);
        compressionStream.Flush();
        compressionStream.Dispose();
    }

    [BenchmarkCategory("Fastest"), Benchmark(Baseline = true)]
    public void CompressNative_fastest() //with creation/disposal of stream
    {
        CompressedStrmN!.Position = 0;
        System.IO.Compression.DeflateStream compressionStream = new(CompressedStrmN, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true);
        compressionStream.Write(UncompressedData!, 0, UncompressedData!.Length);
        compressionStream.Flush();
        compressionStream.Dispose();
    }

    [BenchmarkCategory("Fastest"), Benchmark]
    public void CompressManaged_fastest()
    {
        CompressedStrmM!.Position = 0;
        DeflateStream compressionStream = new(CompressedStrmM, CompressionLevel.Fastest, leaveOpen: true);
        compressionStream.Write(UncompressedData!, 0, UncompressedData!.Length);
        compressionStream.Flush();
        compressionStream.Dispose();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Compression underlying streams
        CompressedStrmN?.Dispose();
        CompressedStrmM?.Dispose();
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