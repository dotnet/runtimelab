// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

// Generated by Fuzzlyn v1.2 on 2021-07-06 09:46:44
// Seed: 16635934940619066544
// Reduced from 447.5 KiB to 0.6 KiB in 00:02:24
// Debug: Runs successfully
// Release: Throws 'System.NullReferenceException'
struct S0
{
    public uint F1;
    public byte F3;
    public long F4;
    public uint F5;
    public S0(long f4): this()
    {
        F4 = f4;
    }
}

class C0
{
    public S0 F4;
}

struct S1
{
    public C0 F2;
    public S0 F8;
    public S1(C0 f2, S0 f8): this()
    {
        F2 = f2;
        F8 = f8;
    }
}

struct S2
{
    public S1 F0;
    public S2(S1 f0): this()
    {
        F0 = f0;
    }
}

public class Program
{
    [Fact]
    public static void TestEntryPoint()
    {
        S2 vr0 = new S2(new S1(new C0(), new S0(0)));
        M17(ref vr0.F0.F2.F4.F1);
    }

    static void M17(ref uint arg2)
    {
    }
}