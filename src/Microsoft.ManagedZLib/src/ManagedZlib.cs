// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.ManagedZLib;

public static class ManagedZLib
{
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
