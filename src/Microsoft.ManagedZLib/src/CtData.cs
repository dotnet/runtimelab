// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.ManagedZLib;

public class CtData
{
    public ushort Freq { get; set; } // frequency count
    //public ushort Code { get; set; } // bit string
    public ushort Len { get; set; } // length of bit string
    //public ushort Parent { get; set; } // father node in Huffman tree
}
