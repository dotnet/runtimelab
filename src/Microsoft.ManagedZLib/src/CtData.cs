// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.ManagedZLib;

public class CtData
{
    //Freq or Code
    public ushort Freq { get; set; } // frequency count
    //Dad or Len
    public ushort Len { get; set; } // length of bit string
}
