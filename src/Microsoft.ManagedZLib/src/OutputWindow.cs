// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.ManagedZLib;

internal sealed class OutputWindow
{
    internal OutputWindow(int windowBits)
    {
        throw new NotImplementedException();
    }

    internal OutputWindow()
    {
        throw new NotImplementedException();
    }

    internal void ClearBytesUsed()
    {
        throw new NotImplementedException();
    }

    public void Write(byte b)
    {
        throw new NotImplementedException();
    }

    public void WriteLengthDistance(int length, int distance)
    {
        throw new NotImplementedException();
    }

    public int CopyFrom(InputBuffer input, int length)
    {
        throw new NotImplementedException();
    }

    public int FreeBytes => throw new NotImplementedException();

    public int AvailableBytes => throw new NotImplementedException();

    public int CopyTo(Span<byte> usersOutput)
    {
        throw new NotImplementedException();
    }
}

