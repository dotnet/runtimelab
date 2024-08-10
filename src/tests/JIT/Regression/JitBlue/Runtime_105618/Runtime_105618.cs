// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Generated by Fuzzlyn v2.1 on 2024-07-28 17:07:50
// Run on Arm Linux
// Seed: 1272406038738543012
// Reduced from 724.7 KiB to 0.4 KiB in 00:20:19
// Hits JIT assert in Release:
// Assertion failed 'op2->TypeIs(TYP_I_IMPL)' in 'Program:Main(Fuzzlyn.ExecutionServer.IRuntime)' during 'Morph - Global' (IL size 29; hash 0xade6b36b; FullOpts)
// 
//     File: /__w/1/s/src/coreclr/jit/promotiondecomposition.cpp Line: 1223
// 
using System;
using System.Runtime.CompilerServices;
using Xunit;

public struct S0
{
    public sbyte F0;
    public float F1;
}

public class C0
{
    public S0 F0;
}

public class Runtime_105618
{
    [Fact]
    public static void TestEntryPoint()
    {
        try
        {
            C0 vr0 = default(C0);
            System.Console.WriteLine(System.BitConverter.SingleToUInt32Bits(vr0.F0.F1));
        }
        catch
        {
        }
    }
}
