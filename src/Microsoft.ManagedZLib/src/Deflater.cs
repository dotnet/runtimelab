// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ZFlushCode = Microsoft.ManagedZLib.ManagedZLib.FlushCode;

namespace Microsoft.ManagedZLib;

internal sealed class Deflater
{
    internal Deflater(CompressionLevel compressionLevel, int windowBits)
    {
    }

    private int DeflateInit( int windowBits)
    {
        throw new NotImplementedException();
    }

    private void DeflateInit2(ManagedZLib.CompressionLevel level,
        ManagedZLib.CompressionMethod method,
        int windowBits,
        int memLevel,
        ManagedZLib.CompressionStrategy strategy)
    {
    }

    ~Deflater()
    {
    }

    public void Dispose()
    {
    }

    private void Dispose(bool disposing)
    {
    }

    internal void SetInput(ReadOnlySpan<byte> inputBuffer)
    {
    }

    internal void SetInput(Span<byte> inputBuffer, int count)
    {
    }

    internal int GetDeflateOutput(byte[] outputBuffer)
    {
        throw new NotImplementedException();
    }

    public int ReadDeflateOutput(Span<byte> outputBuffer, ZFlushCode flushCode)
    {
        throw new NotImplementedException();
    }

    internal int Finish(byte[] outputBuffer)
    {
        throw new NotImplementedException();
    }

    internal bool Flush(byte[] outputBuffer)
    {
        throw new NotImplementedException();
    }

    private int Deflate(ZFlushCode flushCode)
    {
        throw new NotImplementedException();
    }
}
