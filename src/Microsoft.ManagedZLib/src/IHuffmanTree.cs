// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.ManagedZLib;

internal sealed class IHuffmanTree
{
    public static IHuffmanTree StaticLiteralLengthTree { get; } = new IHuffmanTree(GetStaticLiteralTreeLength());

    public static IHuffmanTree StaticDistanceTree { get; } = new IHuffmanTree(GetStaticDistanceTreeLength());

    public IHuffmanTree(byte[] codeLengths)
    {
        throw new NotImplementedException();
    }

    private static byte[] GetStaticLiteralTreeLength()
    {
        throw new NotImplementedException();
    }

    private static byte[] GetStaticDistanceTreeLength()
    {
        throw new NotImplementedException();
    }

    private static uint BitReverse(uint code, int length)
    {
        throw new NotImplementedException();
    }

    private uint[] CalculateHuffmanCode()
    {
        throw new NotImplementedException();
    }

    private void CreateTable()
    {
        throw new NotImplementedException();
    }

    public int GetNextSymbol(InputBuffer input)
    {
        throw new NotImplementedException();
    }
}

