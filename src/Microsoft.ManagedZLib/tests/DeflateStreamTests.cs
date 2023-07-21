// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Sdk;

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

    private void VerifyRead(Stream actualStream)
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

        using StreamReader readerExpected = new StreamReader(expectedStream);
        using StreamReader readerActual = new StreamReader(actualStream);
        string expectedLine = null;
        string actualLine = null;
        while (expectedLine != string.Empty && actualLine != string.Empty)
        {
            expectedLine = readerExpected.ReadToEnd();
            actualLine = readerActual.ReadToEnd();

            Assert.Equal(expectedLine, actualLine);
        }
    }
}
