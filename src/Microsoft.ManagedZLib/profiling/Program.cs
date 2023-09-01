// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ManagedZLib.Benchmarks;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.ManagedZLib;

internal class Program
{

    static Stopwatch stopWatch = new Stopwatch();
    static CompressedFile compressedFile = new("TestDocument.pdf", System.IO.Compression.CompressionLevel.SmallestSize);
    static MemoryStream expectedStream = new();
    //System.IO.Compression.DeflateStream decompressor = new(compressedFile.CompressedDataStream, System.IO.Compression.CompressionMode.Decompress);

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void TestM(DeflateStream decompressor)
    {
        compressedFile.CompressedDataStream.Position = 0;
        expectedStream.Position = 0;
        decompressor.CopyTo(expectedStream); 
    }
    static void TestN(MemoryStream expectedStream, CompressedFile compressedFile, System.IO.Compression.DeflateStream decompressor)
    {
        compressedFile.CompressedDataStream.Position = 0;
        expectedStream.Position = 0;
        decompressor.CopyTo(expectedStream);
    }


    private static void Main(string[] args)
    {
        int iter = 65_536;
        compressedFile.CompressedDataStream.Position = 0;
        DeflateStream decompressor = new(compressedFile.CompressedDataStream, CompressionMode.Decompress);

        Console.WriteLine($"Environment.Version: {Environment.Version}");
        Console.WriteLine($"RuntimeInformation.FrameworkDescription: {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"AssemblyFileVersion: {typeof(object).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version!}");

        for (int i = 0; i < 100; i++)
        {
            TestM(decompressor);
        }

        stopWatch.Start();
        for (int i = 0; i < iter; i++)
        {
            TestM(decompressor);
        }
        stopWatch.Stop();

        // Elapsed time as a TimeSpan value.
        TimeSpan ts = stopWatch.Elapsed;
        Console.WriteLine($"Total time elapsed (us): {ts.TotalMicroseconds}");
        double res = ts.TotalMicroseconds / iter;
        Console.WriteLine($"Time elapsed per iteration(us): {res}");
    }
}