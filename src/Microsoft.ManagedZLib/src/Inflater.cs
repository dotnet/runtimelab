// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.ManagedZLib;

internal class Inflater
{
    private static ReadOnlySpan<byte> LengthBase => throw new NotImplementedException();

    private static ReadOnlySpan<byte> ExtraLengthBits => throw new NotImplementedException();

    private static ReadOnlySpan<ushort> DistanceBasePosition => throw new NotImplementedException();

    private static ReadOnlySpan<ushort> ExtraDistancePosotionBits => throw new NotImplementedException();

    private static ReadOnlySpan<byte> CodeOrder => throw new NotImplementedException();

    private static ReadOnlySpan<byte> StaticDistanceTreeTable => throw new NotImplementedException();

    private static int InflateInit(int windowBits)
    {
        throw new NotImplementedException();
    }

    internal Inflater(int windowBits, long uncompressedSize = -1)
    {
        throw new NotImplementedException();
    }

    internal Inflater(bool deflate64, int windowBits, long uncompressedSize = -1) : this(windowBits, uncompressedSize)
    {
        throw new NotImplementedException();
    }

    public bool Finished() => throw new NotImplementedException();

    public int Inflate(Span<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public int InflateVerified(Span<byte> bufferBytes)
    {
        throw new NotImplementedException();
    }
    
    private int ReadOutput(Span<byte> outputBytes)
    {
        throw new NotImplementedException();
    }

    internal bool IsGzipStream() => throw new NotImplementedException();

    public bool NonEmptyInput() => throw new NotImplementedException();

    public void SetInput(byte[] inputBuffer, int startIndex, int count)
    {
        throw new NotImplementedException();
    }

    public void SetInput(Memory<byte> inputBuffer)
    {
        throw new NotImplementedException();
    }

    private bool Decode()
    {
        throw new NotImplementedException();
    }

    private bool DecodeBlock(out bool end_of_block_code_seen)
    {
        throw new NotImplementedException();
    }

    private bool DecodeUncompressedBlock(out bool end_of_block)
    {
        throw new NotImplementedException();
    }

    private bool DecodeDynamicBlockHeader()
    {
        throw new NotImplementedException();
    }
}
