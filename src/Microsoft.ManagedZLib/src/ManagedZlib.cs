// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.ManagedZLib;

public static class ManagedZLib
{
    public enum FlushCode : int
    {
        NoFlush = 0,
        SyncFlush = 2,
        Finish = 4,
        Block = 5
    }

    public enum ErrorCode : int
    {
        Ok = 0,
        StreamEnd = 1,
        StreamError = -2,
        DataError = -3,
        MemError = -4,
        BufError = -5,
        VersionError = -6
    }

    public enum BlockType
    {
        Uncompressed = 0,
        Static = 1,
        Dynamic = 2
    }

    public enum CompressionLevel : int
    {
        NoCompression = 0,
        BestSpeed = 1,
        DefaultCompression = -1,
        BestCompression = 9
    }

    public enum CompressionStrategy : int
    {
        DefaultStrategy = 0
    }

    public enum CompressionMethod : int
    {
        Deflated = 8
    }

    public const int Deflate_DefaultWindowBits = -15;
    public const int GZip_DefaultWindowBits = 31;
    public const int Deflate_DefaultMemLevel = 8;
    public const int Deflate_NoCompressionMemLevel = 7;

    public const byte GZip_Header_ID1 = 31;
    public const byte GZip_Header_ID2 = 139;

    public sealed class ZLibStreamHandle
    {
        public ErrorCode DeflateInit2_(CompressionLevel level, int windowBits, int memLevel, CompressionStrategy strategy)
        {
            throw new NotImplementedException();
        }

        public ErrorCode Deflate(FlushCode flush)
        {
            throw new NotImplementedException();
        }

        public ErrorCode DeflateEnd()
        {
            throw new NotImplementedException();
        }

        public ErrorCode InflateInit2_(int windowBits)
        {
            throw new NotImplementedException();
        }

        public ErrorCode Inflate(FlushCode flush)
        {
            throw new NotImplementedException();
        }

        public ErrorCode InflateEnd()
        {
            throw new NotImplementedException();
        }
    }
}
