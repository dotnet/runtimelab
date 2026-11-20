// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Xunit;

public static class LocalCollectionOutcomes
{
    [DllImport("ThreadLocalCollectionOutcomesNative", CallingConvention = CallingConvention.Cdecl)]
    private static extern int RunLocalCollectionOutcomes();

    [Fact]
    public static void TestEntryPoint()
    {
        Assert.Equal(0, RunLocalCollectionOutcomes());
    }
}
