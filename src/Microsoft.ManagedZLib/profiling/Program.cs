// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ManagedZLib.Benchmarks;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace Microsoft.ManagedZLib;

internal class Program
{
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void TestM(MemoryStream expectedStream, CompressedFile compressedFile, DeflateStream decompressor)
    {
        //Console.WriteLine($"Iteration: {i}");
        decompressor.CopyTo(expectedStream);
        compressedFile.CompressedDataStream.Position = 0;
        expectedStream.Position = 0;
    }
    static void TestN(MemoryStream expectedStream, CompressedFile compressedFile, System.IO.Compression.DeflateStream decompressor)
    {
        //Console.WriteLine($"Iteration: {i}");
        decompressor.CopyTo(expectedStream);
        compressedFile.CompressedDataStream.Position = 0;
        expectedStream.Position = 0;
    }


    private static void Main(string[] args)
    {
        int iter = 10_000_000;
        Stopwatch stopWatch = new Stopwatch();
        CompressedFile compressedFile = new("TestDocument.pdf", System.IO.Compression.CompressionLevel.SmallestSize);
        compressedFile.CompressedDataStream.Position = 0;
        MemoryStream expectedStream = new();
        // DeflateStream decompressor = new(compressedFile.CompressedDataStream, CompressionMode.Decompress);
        System.IO.Compression.DeflateStream decompressor = new(compressedFile.CompressedDataStream, System.IO.Compression.CompressionMode.Decompress);
        for (int i = 0; i < 100; i++)
        {
            TestN(expectedStream, compressedFile, decompressor);
        }

        stopWatch.Start();
        for (int i = 0; i < iter; i++)
        {
            TestN(expectedStream, compressedFile, decompressor);
        }
        stopWatch.Stop();

        // Elapsed time as a TimeSpan value.
        TimeSpan ts = stopWatch.Elapsed;
        Console.WriteLine($"Total time elapsed (s): {ts.TotalMicroseconds}");
        double res = ts.TotalMicroseconds / iter;
        Console.WriteLine($"Time elapsed per iteration(s): {res}");
    }
}