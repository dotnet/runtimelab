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
    public Stream CreateStream(Stream stream, CompressionMode mode) => new DeflateStream(stream, mode);//For test1
    public Stream CreateStream(Stream stream, CompressionMode mode, bool leaveOpen) => new DeflateStream(stream, mode, leaveOpen);
    public Stream CreateStream(Stream stream, CompressionLevel level) => new DeflateStream(stream, level);
    public Stream CreateStream(Stream stream, CompressionLevel level, bool leaveOpen) => new DeflateStream(stream, level, leaveOpen);
    public Stream BaseStream(Stream stream) => ((DeflateStream)stream).BaseStream;
    protected string CompressedTestFile(string uncompressedPath) => Path.Combine("DeflateTestData", Path.GetFileName(uncompressedPath));
    public static string GetTestFilePath()
    => Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
    private static readonly string _rootDirPath = GetTestFilePath();
    private static readonly string _inputDirPath = Path.Combine(_rootDirPath, "inputdir");
    private static readonly string _testDirPath = Path.Combine(_inputDirPath, "test2dir");
    private static readonly string _testDirPath2 = Path.Combine(_inputDirPath, "test4dir");
    private static readonly string _testFilePath = Path.Combine(_inputDirPath, "fileTest2.txt");
    private static readonly string _testFilePath2 = Path.Combine(_inputDirPath, "fileTest4.txt");
    public static IEnumerable<object[]> UncompressedTestFiles()
    {
        yield return new object[] { Path.Combine("UncompressedTestFiles", "TestDocument.doc") };
        //yield return new object[] { Path.Combine("UncompressedTestFiles", "TestDocument.docx") };
        //yield return new object[] { Path.Combine("UncompressedTestFiles", "TestDocument.pdf") };
        //yield return new object[] { Path.Combine("UncompressedTestFiles", "TestDocument.txt") };
        //yield return new object[] { Path.Combine("UncompressedTestFiles", "alice29.txt") };
        //yield return new object[] { Path.Combine("UncompressedTestFiles", "asyoulik.txt") };
        //yield return new object[] { Path.Combine("UncompressedTestFiles", "cp.html") };
        //yield return new object[] { Path.Combine("UncompressedTestFiles", "fields.c") };
        //yield return new object[] { Path.Combine("UncompressedTestFiles", "grammar.lsp") };
        //yield return new object[] { Path.Combine("UncompressedTestFiles", "kennedy.xls") };
        //yield return new object[] { Path.Combine("UncompressedTestFiles", "lcet10.txt") };
        //yield return new object[] { Path.Combine("UncompressedTestFiles", "plrabn12.txt") };
        //yield return new object[] { Path.Combine("UncompressedTestFiles", "ptt5") };
        //yield return new object[] { Path.Combine("UncompressedTestFiles", "sum") };
        //yield return new object[] { Path.Combine("UncompressedTestFiles", "xargs.1") };
    }

    //[Theory]
    //[MemberData(nameof(UncompressedTestFiles))]
    //public async Task Read(string testFile)
    //{
    //    LocalMemoryStream uncompressedStream = await LocalMemoryStream.readAppFileAsync(testFile);
    //    LocalMemoryStream compressedStream = await LocalMemoryStream.readAppFileAsync(CompressedTestFile(testFile));
    //    using Stream decompressor = CreateStream(compressedStream, CompressionMode.Decompress);
    //    var decompressorOutput = new MemoryStream();

    //    int _bufferSize = 1024;
    //    var bytes = new byte[_bufferSize];
    //    bool finished = false;
    //    int retCount;
    //    while (!finished)
    //    {
    //        retCount = decompressor.Read(bytes, 0, _bufferSize);

    //        if (retCount != 0)
    //            decompressorOutput.Write(bytes, 0, retCount);
    //        else
    //            finished = true;
    //    }
    //    decompressor.Dispose();
    //    decompressorOutput.Position = 0;
    //    uncompressedStream.Position = 0;

    //    byte[] uncompressedStreamBytes = uncompressedStream.ToArray();
    //    byte[] decompressorOutputBytes = decompressorOutput.ToArray();

    //    Assert.Equal(uncompressedStreamBytes.Length, decompressorOutputBytes.Length);
    //    for (int i = 0; i < uncompressedStreamBytes.Length; i++)
    //    {
    //        Assert.Equal(uncompressedStreamBytes[i], decompressorOutputBytes[i]);
    //    }
    //}
    [Fact]
    public void Test1()
    {
        using MemoryStream original = new MemoryStream();

        byte[] originalBytes = new byte[] { 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41,
                                            0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41,
                                            0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41,
                                            0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41,
                                            0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41,
                                            0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41,
                                            0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41,
                                            0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41, 0x4C, 0x4C, 0x4F, 0x52, 0x41};
        byte[] compressedBytes = new byte[10];
        byte[] finalBytes = new byte[10];
        original.Write(originalBytes);

        MemoryStream originalData = new(); 
        originalData.Write(originalBytes);
        originalData.Position = 0;

        MemoryStream compressedDestination = new();
        using (System.IO.Compression.DeflateStream compressor = new System.IO.Compression.DeflateStream(compressedDestination, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            originalData.CopyTo(compressor);
        }
        compressedDestination.Position = 0;
        Console.WriteLine($"CompressedDestination size: {compressedDestination.Length}");
        compressedDestination.ReadAtLeast(compressedBytes, 5, throwOnEndOfStream: false);
        compressedDestination.Position = 0;

        MemoryStream uncompressedDestination = new();
        using (var decompressor = new DeflateStream(compressedDestination, CompressionMode.Decompress, leaveOpen: true))
        {
            decompressor.CopyTo(uncompressedDestination);
        } //Cannot access a close stream error*
        uncompressedDestination.Position = 0;
        Console.WriteLine($"UncompressedDestination size: {uncompressedDestination.Length}");
        uncompressedDestination.ReadAtLeast(finalBytes, 5, throwOnEndOfStream: false);

        Console.WriteLine($"Uncompressed: {Encoding.ASCII.GetString(originalBytes)}");
        Console.WriteLine($"Compressed: ");
        Print(compressedBytes);
        Console.WriteLine($"Final: {Encoding.ASCII.GetString(finalBytes)}");

        uncompressedDestination.Position = 0;
        originalData.Position = 0;

        byte[] uncompressedStreamBytes = originalData.ToArray();
        byte[] decompressorOutputBytes = uncompressedDestination.ToArray();

        Assert.Equal(uncompressedStreamBytes.Length, decompressorOutputBytes.Length);
        for (int i = 0; i < uncompressedStreamBytes.Length; i++)
        {
            Assert.Equal(uncompressedStreamBytes[i], decompressorOutputBytes[i]);
        }
    }

    [Fact] // Test 1 with a file stream instead of a MemoryStream
    public void Test2()
    {
        Directory.CreateDirectory(_testDirPath); // Creates all segments: root/inputdir/testdir
        FileStream fs_originalData = File.Create(_testFilePath);

        //using MemoryStream originalData = new MemoryStream(); //Original - non compressed
        MemoryStream compressedDestination = new(); // Compressed with System.IO.Compresion - Stored here

        byte[] originalBytes = new byte[] { 0x4C, 0x4C, 0x4F, 0x52, 0x41 }; //LLORA
        byte[] compressedBytes = new byte[10];
        byte[] finalBytes = new byte[10];
        fs_originalData.Write(originalBytes); //ORIGINAL - filestream
        //originalData.Write(originalBytes); //ORIGINAL
        //originalData.Position = 0;
        fs_originalData.Position = 0;

        using (System.IO.Compression.DeflateStream compressor = new System.IO.Compression.DeflateStream(compressedDestination, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            fs_originalData.CopyTo(compressor);
        }
        compressedDestination.Position = 0;
        Console.WriteLine($"CompressedDestination size: {compressedDestination.Length}");
        compressedDestination.ReadAtLeast(compressedBytes, 5, throwOnEndOfStream: false);
        compressedDestination.Position = 0;

        MemoryStream uncompressedDestination = new();
        using (var decompressor = new DeflateStream(compressedDestination, CompressionMode.Decompress, leaveOpen: true))
        {
            decompressor.CopyTo(uncompressedDestination);
        } //Cannot access a close stream error*
        uncompressedDestination.Position = 0;
        Console.WriteLine($"UncompressedDestination size: {uncompressedDestination.Length}");
        uncompressedDestination.ReadAtLeast(finalBytes, 5, throwOnEndOfStream: false);

        Console.WriteLine($"Uncompressed: {Encoding.ASCII.GetString(originalBytes)}");
        Console.WriteLine($"Compressed: ");
        Print(compressedBytes);
        Console.WriteLine($"Final: {Encoding.ASCII.GetString(finalBytes)}");

        uncompressedDestination.Position = 0;
        fs_originalData.Position = 0;

        //Array the entrada - podrias intentar sacarlo del filestream para comprobar que la informacion fue correctamente pasada
        byte[] uncompressedStreamBytes = originalBytes; 
        byte[] decompressorOutputBytes = uncompressedDestination.ToArray();

        Assert.Equal(uncompressedStreamBytes.Length, decompressorOutputBytes.Length);
        for (int i = 0; i < uncompressedStreamBytes.Length; i++)
        {
            Assert.Equal(uncompressedStreamBytes[i], decompressorOutputBytes[i]);
        }
    }

    [Fact]
    public void Test3()
    {
        using MemoryStream originalData = new MemoryStream(); //Original - non compressed
        MemoryStream compressedDestination = new(); // Compressed with System.IO.Compresion - Stored here
        //System.IO.Compression.DeflateStream compressor = new System.IO.Compression.DeflateStream(compressedDestination, System.IO.Compression.CompressionMode.Compress, leaveOpen: true);

        byte[] originalBytes = new byte[] { 0x4C, 0x4C, 0x4F, 0x52, 0x41 }; //LLORA
        originalData.Write(originalBytes); //ORIGINAL
        originalData.Position = 0;

        using (System.IO.Compression.DeflateStream compressor = new System.IO.Compression.DeflateStream(compressedDestination, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            originalData.CopyTo(compressor);  //Compressor has the compressed data now.
        }
        // In case I want to print it out:
        byte[] compressedBytes = new byte[10]; //For printing the compressed Bytes
        compressedDestination.Position = 0;
        compressedDestination.ReadAtLeast(compressedBytes, 5, throwOnEndOfStream: false); //Reading compressed bytes
        compressedDestination.Position = 0;

        // ---------------- Read --------------------- (Decompressing method)
        DeflateStream decompressor = new DeflateStream(compressedDestination, CompressionMode.Decompress, leaveOpen: true);
        MemoryStream decompressorOutput = new();

        int _bufferSize = 1024;
        var finalBytes = new byte[_bufferSize]; //User's buffer
        bool finished = false;
        int retCount;
        while (!finished)
        {
            retCount = decompressor.Read(finalBytes, 0, _bufferSize);

            if (retCount != 0)
                decompressorOutput.Write(finalBytes, 0, retCount); //Write from DeflateStream to MemoryStream
            else
                finished = true;
        }

        // Compressed Size
        Console.WriteLine($"CompressedDestination size: {compressedDestination.Length}");
        // Size after decompressing
        Console.WriteLine($"UncompressedDestination size: {finalBytes.Length}");

        Console.WriteLine($"Uncompressed: {Encoding.ASCII.GetString(originalBytes)}");
        Console.WriteLine($"Compressed: ");
        Print(compressedBytes);
        Console.WriteLine($"Final: {Encoding.ASCII.GetString(finalBytes)}");

        //From the Stream -Check results-
        decompressorOutput.Position = 0;
        originalData.Position = 0;
        byte[] uncompressedStreamBytes = originalData.ToArray();
        byte[] decompressorOutputBytes = decompressorOutput.ToArray();

        Assert.Equal(uncompressedStreamBytes.Length, decompressorOutputBytes.Length);
        for (int i = 0; i < uncompressedStreamBytes.Length; i++)
        {
            Assert.Equal(uncompressedStreamBytes[i], decompressorOutputBytes[i]);
        }
    }

    [Fact]
    public void Test4() // Test3 with a filestream
    {
        Directory.CreateDirectory(_testDirPath2); // Creates all segments: root/inputdir/testdir
        FileStream fs_originalData = File.Create(_testFilePath2);

        //File.Create(_testFilePath).Dispose();
        //File.Delete(_testDirPath);
        using MemoryStream originalData = new MemoryStream(); //Original - non compressed
        MemoryStream compressedDestination = new(); // Compressed with System.IO.Compresion - Stored here
        //System.IO.Compression.DeflateStream compressor = new System.IO.Compression.DeflateStream(compressedDestination, System.IO.Compression.CompressionMode.Compress, leaveOpen: true);

        byte[] originalBytes = new byte[] { 0x4C, 0x4C, 0x4F, 0x52, 0x41 }; //LLORA
        fs_originalData.Write(originalBytes); //ORIGINAL - filestream
        originalData.Write(originalBytes); //ORIGINAL
        originalData.Position = 0;
        fs_originalData.Position = 0;

        using (System.IO.Compression.DeflateStream compressor = new System.IO.Compression.DeflateStream(compressedDestination, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            fs_originalData.CopyTo(compressor);  //Compressor has the compressed data now.
        }
        compressedDestination.Position = 0;

        // In case I want to print it out:
        byte[] compressedBytes = new byte[10]; //For printing the compressed Bytes
        compressedDestination.ReadAtLeast(compressedBytes, 5, throwOnEndOfStream: false); //Reading compressed bytes
        compressedDestination.Position = 0;

        // ---------------- Read --------------------- (Decompressing method)
        DeflateStream decompressor = new DeflateStream(compressedDestination, CompressionMode.Decompress, leaveOpen: true);
        MemoryStream decompressorOutput = new();

        int _bufferSize = 1024;
        var finalBytes = new byte[_bufferSize]; //User's buffer
        bool finished = false;
        int retCount;
        while (!finished)
        {
            retCount = decompressor.Read(finalBytes, 0, _bufferSize);

            if (retCount != 0)
                decompressorOutput.Write(finalBytes, 0, retCount); //Write from DeflateStream to MemoryStream
            else
                finished = true;
        }

        // Compressed Size
        Console.WriteLine($"CompressedDestination size: {compressedDestination.Length}");
        // Size after decompressing
        Console.WriteLine($"UncompressedDestination size: {finalBytes.Length}");

        Console.WriteLine($"Uncompressed: {Encoding.ASCII.GetString(originalBytes)}");
        Console.WriteLine($"Compressed: ");
        Print(compressedBytes);
        Console.WriteLine($"Final: {Encoding.ASCII.GetString(finalBytes)}");

        //From the Stream -Check results-
        decompressorOutput.Position = 0;
        originalData.Position = 0;
        //Tal vez deberia checar el archivo per se, pero se hace con Read. No tiene un ToArray() como MemoryStream
        byte[] uncompressedStreamBytes = originalBytes;
        byte[] decompressorOutputBytes = decompressorOutput.ToArray(); //After read()

        fs_originalData.Dispose();

        Assert.Equal(uncompressedStreamBytes.Length, decompressorOutputBytes.Length);
        for (int i = 0; i < uncompressedStreamBytes.Length; i++)
        {
            Assert.Equal(uncompressedStreamBytes[i], decompressorOutputBytes[i]);
        }
    }

    [Theory] //Try use localMemory stream instead of filestream
    [InlineData("UncompressedTestFiles", "TestDocument.doc")]
    public async void Test5(string folderName, string fileName)  // Test3 with a filestream
    {
        string testFile = Path.Combine(folderName, fileName);
        LocalMemoryStream uncompressedStream = await LocalMemoryStream.readAppFileAsync(testFile);
        LocalMemoryStream compressedStream = await LocalMemoryStream.readAppFileAsync(CompressedTestFile(testFile)); //Compressed stream
        // ---------------- Decompressed stream - Passing it the compressed stream
        using Stream decompressor = CreateStream(compressedStream, CompressionMode.Decompress);
        var decompressorOutput = new MemoryStream();

        using MemoryStream originalData = new MemoryStream(); //Original - non compressed
        MemoryStream compressedDestination = new(); // Compressed with System.IO.Compresion - Stored here
        //System.IO.Compression.DeflateStream compressor = new System.IO.Compression.DeflateStream(compressedDestination, System.IO.Compression.CompressionMode.Compress, leaveOpen: true);


        using (System.IO.Compression.DeflateStream compressor = new System.IO.Compression.DeflateStream(compressedDestination, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            uncompressedStream.CopyTo(compressor);  //Compressor has the compressed data now.
        }
        compressedDestination.Position = 0;

        // In case I want to print it out:
        byte[] compressedBytes = new byte[50]; //For printing the compressed Bytes
        compressedDestination.ReadAtLeast(compressedBytes, 49, throwOnEndOfStream: false); //Reading compressed bytes
        compressedDestination.Position = 0;

        // ---------------- Read --------------------- (Decompressing method)
        int _bufferSize = 1024;
        var finalBytes = new byte[_bufferSize]; //User's buffer
        bool finished = false;
        int retCount;
        while (!finished)
        { //retCount will be either the length given or the available input in the underlyign (compressed data) stream
            retCount = decompressor.Read(finalBytes, 0, _bufferSize);

            if (retCount != 0)
                decompressorOutput.Write(finalBytes, 0, retCount); //Write from DeflateStream to MemoryStream
            else
                finished = true;
        }
        byte[] originalBytes = uncompressedStream.ToArray();
        // Compressed Size
        Console.WriteLine($"CompressedDestination size: {compressedDestination.Length}");
        // Size after decompressing
        Console.WriteLine($"UncompressedDestination size: {finalBytes.Length}");

        Console.WriteLine($"Uncompressed: {Encoding.ASCII.GetString(originalBytes)}");
        Console.WriteLine($"Compressed: ");
        Print(compressedBytes);
        Console.WriteLine($"Final: {Encoding.ASCII.GetString(finalBytes)}");

        //From the Stream -Check results-
        decompressorOutput.Position = 0;
        originalData.Position = 0;
        uncompressedStream.Position = 0;

        //Tal vez deberia checar el archivo per se, pero se hace con Read. No tiene un ToArray() como MemoryStream
        //byte[] uncompressedStreamBytes = originalBytes;
        byte[] uncompressedStreamBytes = uncompressedStream.ToArray();
        byte[] decompressorOutputBytes = decompressorOutput.ToArray(); //After read()

        //Porque esta casteado a Stream aunque haya creado DeflateS

        Assert.Equal(uncompressedStreamBytes.Length, decompressorOutputBytes.Length);
        for (int i = 0; i < uncompressedStreamBytes.Length; i++)
        {
            Assert.Equal(uncompressedStreamBytes[i], decompressorOutputBytes[i]);
        }
    }

    [Theory] // Test closest to what we have in the ZLibNative test suite - ToCopy
    [InlineData("UncompressedTestFiles", "TestDocument.doc")]
    public async void Test6(string folderName, string fileName)  // Test3 with a filestream
    {
        string testFile = Path.Combine(folderName, fileName);
        LocalMemoryStream uncompressedStream = await LocalMemoryStream.readAppFileAsync(testFile);
        LocalMemoryStream compressedStream = await LocalMemoryStream.readAppFileAsync(CompressedTestFile(testFile)); //Compressed stream

        using MemoryStream originalData = new MemoryStream(); //Original - non compressed
        MemoryStream compressedDestination = new(); // Compressed with System.IO.Compresion - Stored here


        using (System.IO.Compression.DeflateStream compressor = new System.IO.Compression.DeflateStream(compressedDestination, System.IO.Compression.CompressionMode.Compress, leaveOpen: true))
        {
            // Original stream (with the document info) to compressor stream (Compressed)
            uncompressedStream.CopyTo(compressor);  //Compressor has the compressed data now.
        }
        compressedDestination.Position = 0; //After making the copy into it, we have to reset the iterator

        // To print it out:
        byte[] compressedBytes = new byte[50]; //For printing the compressed Bytes
        byte[] finalBytes = new byte[50];
        // Filling a byte array with 50 bytes of the compressed data
        compressedDestination.ReadAtLeast(compressedBytes, 49, throwOnEndOfStream: false); //Reading compressed bytes
        compressedDestination.Position = 0;

        MemoryStream uncompressedDestination = new();
        using (var decompressor = new DeflateStream(compressedDestination, CompressionMode.Decompress, leaveOpen: true))
        {
            decompressor.CopyTo(uncompressedDestination); //UncompressedDestination will have the decompressed data stream from decompressor
        } //Cannot access a close stream error*
        uncompressedDestination.Position = 0;
        // Filling a byte array with 50 bytes of the just decompressed data
        uncompressedDestination.ReadAtLeast(finalBytes, 5, throwOnEndOfStream: false);
        uncompressedDestination.Position = 0;

        byte[] originalBytes = uncompressedStream.ToArray();
        // Compressed Size
        Console.WriteLine($"CompressedDestination size: {compressedDestination.Length}");
        // Size after decompressing
        Console.WriteLine($"UncompressedDestination (decompressor copy) size: {uncompressedDestination.Length}");
        Console.WriteLine($"Uncompressed: {Encoding.ASCII.GetString(originalBytes)}");
        Console.WriteLine($"Compressed: "); Print(compressedBytes);
        Console.WriteLine($"Final: {Encoding.ASCII.GetString(finalBytes)}");

        //From the Stream -Check results-
        originalData.Position = 0;
        uncompressedStream.Position = 0;

        //Making the original and decompressed stream arrays to check them byte per byte
        byte[] uncompressedStreamBytes = uncompressedStream.ToArray();
        byte[] decompressorOutputBytes = uncompressedDestination.ToArray(); //After read()
        uncompressedStream.Position = 0;
        uncompressedDestination.Position = 0;

        //Porque esta casteado a Stream aunque haya creado DeflateS

        Assert.Equal(uncompressedStreamBytes.Length, decompressorOutputBytes.Length);
        for (int i = 0; i < uncompressedStreamBytes.Length; i++)
        {
            Assert.Equal(uncompressedStreamBytes[i], decompressorOutputBytes[i]);
        }
    }

    private void Print(byte[] arr)
    {
        foreach (byte b in arr)
        {
            Console.Write($"{b}, ");
        }
        Console.WriteLine();
    }
}
