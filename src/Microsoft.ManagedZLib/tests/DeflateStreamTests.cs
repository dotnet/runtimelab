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

    private const string Message = "Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.";
    private static readonly byte[] s_messageBytes = Encoding.ASCII.GetBytes(Message);

    [Fact]
    public void verifyCompression()
    {
        using var streamN = new MemoryStream();
        using var compressorN = new DeflateStream(streamN, CompressionMode.Compress, leaveOpen: true);
        compressorN.Write(s_messageBytes, 0, s_messageBytes.Length);
        Console.WriteLine($"The compressed stream (with NATIVE) length is {streamN.Length} bytes.");
        streamN.Position = 0;

        using var streamM = new MemoryStream();
        using var compressorM = new DeflateStream(streamM, CompressionMode.Compress, leaveOpen: true);
        compressorM.Write(s_messageBytes, 0, s_messageBytes.Length);
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

    //[Theory]
    //[MemberData(nameof(UncompressedTestFiles))]
    //public void CompressFile(string path)
    //{
    //    using FileStream fileStream = File.OpenRead(path);
    //    VerifyRead(fileStream);
    //}

    // For a fast content check, I'll compress just 4000 bytes of each
    // and check the decompressed data using System.IO.Compression inflater
    private static void VerifyCompression(Stream actualStream)
    {
        MemoryStream compressedDestNative = new(); // Compressed data (with System.IO.Compresion)
        byte[] nativeBytes = new byte[3000];
        actualStream.ReadAtLeast(nativeBytes, 1024);
        using (System.IO.Compression.DeflateStream compressorN = new System.IO.Compression.DeflateStream(compressedDestNative, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            compressorN.Write(nativeBytes);  //Copies the compressed data to Compressor
        }
        compressedDestNative.Position = 0;
        actualStream.Position = 0;

        MemoryStream compressedDestManaged = new(); // Compressed data (with Managed.ZLib)
        byte[] managedBytes = new byte[3000];
        actualStream.ReadAtLeast(nativeBytes, 1024);
        using (DeflateStream compressorM = new DeflateStream(compressedDestManaged, CompressionLevel.Optimal))
        {
            compressorM.Write(managedBytes);  //Copies the compressed data to Compressor
        }
        compressedDestManaged.Position = 0;
        actualStream.Position = 0;

        //--------------------------------------------------- Decompression -------------------------------------------------------
        MemoryStream expectedStream = new();
        using (DeflateStream decompressorN = new DeflateStream(compressedDestNative, CompressionMode.Decompress, leaveOpen: true))
        {
            decompressorN.CopyTo(expectedStream); //Copies decompress data to ExpectedStream
        }
        compressedDestManaged.Position = 0;
        expectedStream.Position = 0;

        using (DeflateStream decompressorM = new DeflateStream(compressedDestManaged, CompressionMode.Decompress, leaveOpen: true))
        {
            decompressorM.CopyTo(actualStream); //Copies decompress data to ExpectedStream
        }
        compressedDestManaged.Position = 0;
        actualStream.Position = 0;

        byte[] bufferActual = new byte[3000];
        byte[] bufferExpected = new byte[3000];
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
}
