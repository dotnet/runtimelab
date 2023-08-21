// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Linq;
using Xunit;

namespace Microsoft.ManagedZLib.Tests;

public class DeflateStreamTests
{

    public static IEnumerable<object[]> UncompressedTestFiles()
    {
        foreach (var path in Directory.EnumerateFiles("UncompressedTestFiles", "*", SearchOption.AllDirectories))
        {
            yield return new object[] { path };
        }
    }
    public static IEnumerable<object[]> UncompressedTestFilesBasic()
    {
        foreach (var path in Directory.EnumerateFiles("UncompressedTestFiles", "*", SearchOption.TopDirectoryOnly))
        {
            yield return new object[] { path };
        }
    }
    public static IEnumerable<CompressionLevel> GetCompressionLevels()
    {
        yield return CompressionLevel.Optimal; 
        yield return CompressionLevel.SmallestSize; 
        yield return CompressionLevel.Fastest;
    }
    public static IEnumerable<object[]> ByteArrayData()
    {
        yield return new object[] { new byte[] { 0x4C, 0x4C, 0x4F, 0x52, 0x41 } };
        yield return new object[] {  new byte[] { 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41,
                                            0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41,
                                            0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41,
                                            0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41,
                                            0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41,
                                            0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41,
                                            0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41,
                                            0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41} };
    }

    [Theory]
    [MemberData(nameof(ByteArrayData))]
    public void ReadSmallArrays(byte[] bytes)
    {
        using MemoryStream originalData = new(bytes);
        VerifyRead(originalData);
    }

    [Theory]
    [MemberData(nameof(UncompressedTestFiles))]
    public void ReadFile(string path)
    {
        using FileStream fileStream = File.OpenRead(path);
        VerifyRead(fileStream);
    }

    private static void VerifyRead(Stream actualStream)
    {
        MemoryStream compressedDestination = new(); // Compressed data (with System.IO.Compresion)
        using (System.IO.Compression.DeflateStream compressor = new System.IO.Compression.DeflateStream(compressedDestination, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            actualStream.CopyTo(compressor);  //Copies the compressed data to Compressor
        }
        compressedDestination.Position = 0;

        // Decompression
        MemoryStream expectedStream = new();
        using (DeflateStream decompressor = new DeflateStream(compressedDestination, CompressionMode.Decompress, leaveOpen: true))
        {
            decompressor.CopyTo(expectedStream); //Copies decompress data to ExpectedStream
        }
        actualStream.Position = 0;

        byte[] bufferActual = new byte[4096];
        byte[] bufferExpected = new byte[4096];
        int bytesReadActual = 0;
        int bytesReadExpected = 0;
        while (bytesReadActual != 0 && bytesReadExpected != 0)
        {
            //actualStream against expectedStream
            bytesReadActual = actualStream.Read(bufferActual, 0, bufferActual.Length);
            bytesReadExpected = expectedStream.Read(bufferExpected, 0, bufferExpected.Length);

            Assert.Equal(bytesReadExpected, bytesReadActual);
        }
    }

    private const string Message = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et " +
        "dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur." +
        " Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum." +
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et " +
        "dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur." +
        " Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";

    private static readonly byte[] s_messageBytes = Encoding.ASCII.GetBytes(Message);

    [Fact]
    public void verifyCompression()
    {
        using var streamN = new MemoryStream();
        using (var compressorN = new System.IO.Compression.DeflateStream(streamN, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            compressorN.Write(s_messageBytes, 0, s_messageBytes.Length);
        }
        Console.WriteLine($"The compressed stream (with NATIVE) length is {streamN.Length} bytes.");
        streamN.Position = 0;

        using var streamM = new MemoryStream();
        using (var compressorM = new DeflateStream(streamM, CompressionMode.Compress, leaveOpen: true))
        {
            compressorM.Write(s_messageBytes, 0, s_messageBytes.Length);
        }
        Console.WriteLine($"The compressed stream (with MANAGED) length is {streamM.Length} bytes.");
        streamM.Position = 0;

        int nativeBytes = DecompressStreamToBytes(streamN);
        int managedBytes = DecompressStreamToBytes(streamM);

        Console.WriteLine($"Decompression with NATIVE length is {nativeBytes} bytes.");
        Console.WriteLine($"Decompression with MANAGED length is {managedBytes} bytes.");

        // I'll first check number of bytes and if it can compress
        // THis last one meaning, System.IO.Compression can decompress it no matter the actual bytes
        Assert.Equal(nativeBytes, managedBytes);
    }
    private static int DecompressStreamToBytes(Stream stream)
    {
        stream.Position = 0;
        int bufferSize = 512;
        byte[] decompressedBytes = new byte[bufferSize];
        using var decompressor = new System.IO.Compression.DeflateStream(stream, System.IO.Compression.CompressionMode.Decompress);
        int length = decompressor.Read(decompressedBytes, 0, bufferSize);
        return length;
    }

    [Fact]
    private static void VerifyCompressionResults_loremIpsum()
    {
        using MemoryStream originalData = new(s_messageBytes); 
        //using MemoryStream garbageData = new(Encoding.ASCII.GetBytes("I'm not the expected data"));

        using var streamM = new MemoryStream();
        //Compress with mine
        using (var compressorM = new DeflateStream(streamM, CompressionLevel.Fastest, leaveOpen: true))
        {
            compressorM.Write(s_messageBytes, 0, s_messageBytes.Length);
        }
        streamM.Position = 0;

        // Decompression with native
        MemoryStream expectedStream = new();
        using (System.IO.Compression.DeflateStream decompressor = new (streamM, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true))
        {
            decompressor.CopyTo(expectedStream); //Copies decompress data to ExpectedStream
        }
        expectedStream.Position = 0;
        streamM.Position = 0;

        byte[] bufferOriginal = new byte[200];
        byte[] bufferDecompressed = new byte[200];
        long counter = 0;
        int bytesReadFromOriginal = 0;
        int bytesReadDecompressed = 0;

        while (counter < originalData.Length) //If one gets emptie first, the comparison continues and eventually will mismatch
        {
            //comparing decompression results
            bytesReadFromOriginal = originalData.Read(bufferOriginal, 0, bufferOriginal.Length);
            bytesReadDecompressed = expectedStream.Read(bufferDecompressed, 0, bufferDecompressed.Length);

            //Assert.Equal(bufferOriginal, bufferDecompressed);
            counter += bytesReadFromOriginal;
        }
        Assert.Equal(originalData.Length, counter);
    }
    //private string filepath = Path.Combine("UncompressedTestFiles", "TestDocument.pdf");
    public byte[] UncompressedData { get; set; }

    [Theory]
    [MemberData(nameof(UncompressedTestFilesBasic))] //Figure out how to also pass the compression level like the file names
    public void verifyCompression_Files_Optimal(string filepath)
    {
        CompressionLevel compressionLevel = CompressionLevel.Optimal;
        UncompressedData = File.ReadAllBytes(filepath);
        using MemoryStream originalData = new(UncompressedData); //Line maybe redundant since it was a fileStream already

        MemoryStream CompressedDataStream = new(capacity: UncompressedData.Length);
        DeflateStream compressionStream = new DeflateStream(CompressedDataStream, compressionLevel, leaveOpen: true);

        compressionStream.Write(UncompressedData, 0, UncompressedData.Length);
        compressionStream.Flush();
        CompressedDataStream.Position = 0;

        decompression_verification(originalData, CompressedDataStream);
    }

    [Theory]
    [MemberData(nameof(UncompressedTestFiles))] //Figure out how to also pass the compression level like the file names
    public void verifyCompression_Files_SmallestSize(string filepath)
    {
        CompressionLevel compressionLevel = CompressionLevel.SmallestSize;
        UncompressedData = File.ReadAllBytes(filepath);
        using MemoryStream originalData = new(UncompressedData); //Line maybe redundant since it was a fileStream already

        MemoryStream CompressedDataStream = new(capacity: UncompressedData.Length);
        DeflateStream compressionStream = new DeflateStream(CompressedDataStream, compressionLevel, leaveOpen: true);

        compressionStream.Write(UncompressedData, 0, UncompressedData.Length);
        compressionStream.Flush();
        CompressedDataStream.Position = 0;

        decompression_verification(originalData, CompressedDataStream);
    }

    [Theory]
    [MemberData(nameof(UncompressedTestFilesBasic))] //Figure out how to also pass the compression level like the file names
    public void verifyCompression_Files_Fastest(string filepath)
    {
        CompressionLevel compressionLevel = CompressionLevel.Fastest;
        UncompressedData = File.ReadAllBytes(filepath);
        using MemoryStream originalData = new(UncompressedData); //Line maybe redundant since it was a fileStream already

        MemoryStream CompressedDataStream = new(capacity: UncompressedData.Length);
        DeflateStream compressionStream = new DeflateStream(CompressedDataStream, compressionLevel, leaveOpen: true);

        compressionStream.Write(UncompressedData, 0, UncompressedData.Length);
        compressionStream.Flush();
        CompressedDataStream.Position = 0;

        decompression_verification(originalData, CompressedDataStream);
    }

    private void decompression_verification(MemoryStream originalData, MemoryStream streamManagedCompression)
    {
        MemoryStream expectedStream = new();
        using (System.IO.Compression.DeflateStream decompressor = new(streamManagedCompression, System.IO.Compression.CompressionMode.Decompress, leaveOpen: true))
        {
            decompressor.CopyTo(expectedStream); //Copies decompress data to ExpectedStream
        }
        expectedStream.Position = 0;
        streamManagedCompression.Position = 0;

        byte[] bufferOriginal = new byte[200];
        byte[] bufferDecompressed = new byte[200];
        long counter = 0;
        int bytesReadFromOriginal = 0;
        int bytesReadDecompressed = 0;

        while (counter < originalData.Length) //If one gets emptie first, the comparison continues and eventually will mismatch
        {
            //comparing decompression results
            bytesReadFromOriginal = originalData.Read(bufferOriginal, 0, bufferOriginal.Length);
            bytesReadDecompressed = expectedStream.Read(bufferDecompressed, 0, bufferDecompressed.Length);

            //Assert.Equal(bufferOriginal, bufferDecompressed);
            counter += bytesReadFromOriginal;
        }
        Assert.Equal(originalData.Length, counter);
    }
}
