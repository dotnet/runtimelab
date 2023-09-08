// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Internal.Mint;

internal static class Mint
{
    internal static void Initialize()
    {
        AppContext.SetSwitch("System.Private.Mint.Enable", true);
    }
}
