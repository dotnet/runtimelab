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

public class MyClassTests
{
 
    public static IEnumerable<object[]> UncompressedTestFiles()
    {
        yield return new object[] { Path.Combine("UncompressedTestFiles", "TestDocument.doc") };
        yield return new object[] { Path.Combine("UncompressedTestFiles", "TestDocument.docx") };
        yield return new object[] { Path.Combine("UncompressedTestFiles", "TestDocument.pdf") };
        yield return new object[] { Path.Combine("UncompressedTestFiles", "TestDocument.txt") };
        yield return new object[] { Path.Combine("UncompressedTestFiles", "alice29.txt") };
        yield return new object[] { Path.Combine("UncompressedTestFiles", "asyoulik.txt") };
        yield return new object[] { Path.Combine("UncompressedTestFiles", "cp.html") };
        yield return new object[] { Path.Combine("UncompressedTestFiles", "fields.c") };
        yield return new object[] { Path.Combine("UncompressedTestFiles", "grammar.lsp") };
        yield return new object[] { Path.Combine("UncompressedTestFiles", "kennedy.xls") };
        yield return new object[] { Path.Combine("UncompressedTestFiles", "lcet10.txt") };
        yield return new object[] { Path.Combine("UncompressedTestFiles", "plrabn12.txt") };
        yield return new object[] { Path.Combine("UncompressedTestFiles", "ptt5") };
        yield return new object[] { Path.Combine("UncompressedTestFiles", "sum") };
        yield return new object[] { Path.Combine("UncompressedTestFiles", "xargs.1") };
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
        using MemoryStream originalData = new MemoryStream();
        originalData.Write(bytes);
        originalData.Position = 0;
        VerifyRead(originalData);
    }

    [Theory]
    [MemberData(nameof(UncompressedTestFiles))]
    public void ReadFile(string path)
    {
        using FileStream fileStream = File.OpenRead(path);
        VerifyRead(fileStream);
    }

    private void VerifyRead (Stream originalStream)
    {
        MemoryStream compressedDestination = new(); // Compressed data (with System.IO.Compresion)
        using (System.IO.Compression.DeflateStream compressor = new System.IO.Compression.DeflateStream(compressedDestination, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            originalStream.CopyTo(compressor);  //Compressor has the compressed data now.
        }
        compressedDestination.Position = 0;

        // Decompression
        MemoryStream expectedStream = new();
        using (DeflateStream decompressor = new DeflateStream(compressedDestination, CompressionMode.Decompress, leaveOpen: true))
        {
            decompressor.CopyTo(expectedStream);
        }

        using StreamReader readerOriginal = new StreamReader(originalStream);
        using StreamReader readerExpected = new StreamReader(expectedStream);

        string originalLine = null;
        string expectedLine = null;
        while (originalLine != string.Empty && expectedLine != string.Empty)
        {
            originalLine = readerOriginal.ReadToEnd();
            expectedLine = readerExpected.ReadToEnd();

            Assert.Equal(expectedLine, originalLine);
        }
    }
}
