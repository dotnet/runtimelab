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
public class ManagedZLibBenchmark
{
    public static IEnumerable<string> UncompressedTestFileNames()
    {
        //yield return "TestDocument.pdf"; // 199 KB small test document with repeated paragraph, PDF are common
        //yield return "alice29.txt"; // 145 KB, copy of "ALICE'S ADVENTURES IN WONDERLAND" book, an example of text file
        yield return "sum"; // 37.3 KB, some binary content, an example of binary file
    }

    [ParamsSource(nameof(UncompressedTestFileNames))]
    public string? Files { get; set; }

    [Params(System.IO.Compression.CompressionLevel.SmallestSize,
            System.IO.Compression.CompressionLevel.Optimal,
            System.IO.Compression.CompressionLevel.Fastest)] // we don't test the performance of CompressionLevel.NoCompression on purpose
    public System.IO.Compression.CompressionLevel Level { get; set; }

    [Params(CompressionLevel.SmallestSize,
            CompressionLevel.Optimal,
            CompressionLevel.Fastest)] // we don't test the performance of CompressionLevel.NoCompression on purpose
    public CompressionLevel ManagedLevel { get; set; }

    public byte[]? UncompressedData { get; set; }
    public byte[]? CompressedData { get; set; }

    public MemoryStream? CompressedStreamN { get; set; }
    public MemoryStream? CompressedStreamM { get; set; }

    DeflateStream? compressionStreamM;
    System.IO.Compression.DeflateStream? compressionStreamN;

    internal static string GetFilePath(string fileName)
        => Path.Combine("UncompressedTestFiles", fileName);

    public CompressedFile? CompressedFile;

    [GlobalSetup]
    public void Setup()
    {
        Debug.Assert(Files != null);
        CompressedFile = new CompressedFile(Files, Level);

        var filePath = GetFilePath(Files);
        UncompressedData = File.ReadAllBytes(filePath);

        //Managed
        CompressedStreamM = new MemoryStream(capacity: UncompressedData.Length);
        compressionStreamM = new DeflateStream(CompressedStreamM, ManagedLevel, leaveOpen: true);

        //Native
        CompressedStreamN = new MemoryStream(capacity: UncompressedData.Length);
        compressionStreamN = new System.IO.Compression.DeflateStream(CompressedStreamN, Level, leaveOpen: true);
    }


    [Benchmark(Baseline = true)]
    public int DecompressNative()
    {
        CompressedFile!.CompressedDataStream.Position = 0;
        MemoryStream expectedStream = new();
        System.IO.Compression.DeflateStream decompressor = new System.IO.Compression.DeflateStream(CompressedFile.CompressedDataStream, System.IO.Compression.CompressionMode.Decompress);
        decompressor.CopyTo(expectedStream);
        return 0;
    }

    [Benchmark]
    public int DecompressManaged()
    {
        CompressedFile!.CompressedDataStream.Position = 0;
        MemoryStream expectedStream = new();
        DeflateStream decompressor = new DeflateStream(CompressedFile.CompressedDataStream, CompressionMode.Decompress);
        decompressor.CopyTo(expectedStream);
        return 0;
    }

    [Benchmark(Baseline = true)]
    public int CompressNative()
    {
        CompressedStreamN!.Position = 0;
        compressionStreamN!.Write(UncompressedData!, 0, UncompressedData!.Length);
        compressionStreamN.Flush();
        return 0;
    }

    [Benchmark]
    public int CompressManaged()
    {
        CompressedStreamM!.Position = 0;
        compressionStreamM!.Write(UncompressedData!, 0, UncompressedData!.Length);
        compressionStreamM.Flush();
        return 0;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        CompressedFile?.CompressedDataStream.Dispose();
        CompressedStreamN?.Dispose();
        CompressedStreamM?.Dispose();
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