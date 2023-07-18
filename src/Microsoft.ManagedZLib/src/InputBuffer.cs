// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.ManagedZLib;

internal sealed class InputBuffer
{
    public bool EnsureBitsAvailable(int count)
    {
        throw new NotImplementedException();
    }

    public uint TryLoad16Bits()
    {
        throw new NotImplementedException();
    }

    private static uint GetBitMask(int count) => throw new NotImplementedException();

    public int GetBits(int count)
    {
        throw new NotImplementedException();
    }

    public int CopyTo(Memory<byte> output)
    {
        throw new NotImplementedException();
    }

    public int CopyTo(byte[] output, int offset, int length)
    {
        throw new NotImplementedException();
    }

    public bool NeedsInput() => throw new NotImplementedException();

    public void SetInput(Memory<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public void SetInput(byte[] buffer, int offset, int length)
    {
        throw new NotImplementedException();
    }

    public void SkipBits(int n)
    {
        throw new NotImplementedException();
    }

    public void SkipToByteBoundary()
    {
        throw new NotImplementedException();
    }
}

