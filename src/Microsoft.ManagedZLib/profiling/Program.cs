// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.ManagedZLib.Benchmarks;
using System;
using System.IO;

namespace Microsoft.ManagedZLib;

internal class Program
{
    private static void Main(string[] args)
    {
        CompressedFile compressedFile = new("TestDocument.pdf", System.IO.Compression.CompressionLevel.SmallestSize);
        compressedFile.CompressedDataStream.Position = 0;
        MemoryStream expectedStream = new();
        DeflateStream decompressor = new(compressedFile.CompressedDataStream, CompressionMode.Decompress);

        for (int i = 0; i < 10_000; i++)
        {
            Console.WriteLine($"Iteration: {i}");
            decompressor.CopyTo(expectedStream);
            compressedFile.CompressedDataStream.Position = 0;
            expectedStream.Position = 0;
        }
    }
}