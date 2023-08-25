// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.ManagedZLib.Benchmarks;

public class CompressedFile
{
    public string Name { get; }
    public System.IO.Compression.CompressionLevel CompressionLevel { get; }

    public byte[] UncompressedData { get; }
    public byte[] CompressedData { get; }
    public MemoryStream CompressedDataStream { get; }

    public CompressedFile(string fileName, System.IO.Compression.CompressionLevel compressionLevel)
    {
        Name = fileName;
        CompressionLevel = compressionLevel;

        var filePath = GetFilePath(fileName);
        UncompressedData = File.ReadAllBytes(filePath);
        CompressedDataStream = new MemoryStream(capacity: UncompressedData.Length);

        var compressionStream = new System.IO.Compression.DeflateStream(CompressedDataStream, compressionLevel, leaveOpen: true);
        compressionStream.Write(UncompressedData, 0, UncompressedData.Length);
        compressionStream.Flush();

        CompressedDataStream.Position = 0;
        CompressedData = CompressedDataStream.ToArray();
    }

    public override string ToString() => Name;

    internal static string GetFilePath(string fileName)
        => Path.Combine("DeflateTestData", fileName);
}
