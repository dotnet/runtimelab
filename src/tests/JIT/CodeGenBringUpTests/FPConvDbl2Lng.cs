// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//


using System;
using System.Runtime.CompilerServices;
using Xunit;
public class BringUpTest_FPConvDbl2Lng
{
    const int Pass = 100;
    const int Fail = -1;

    [MethodImplAttribute(MethodImplOptions.NoInlining)]
    public static long FPConvDbl2Lng(double x) { return (long) x; }

    [MethodImplAttribute(MethodImplOptions.NoInlining)]
    public static UInt64 FPConvDbl2Lng(float x) { return (UInt64) x; }


    [Fact]
    public static int TestEntryPoint()
    {
        int result = Fail;
        long x = FPConvDbl2Lng(3294168832d);
        Console.WriteLine(x);
        if (x == 3294168832L) result = Pass;
        
        int result2 = Fail;
        UInt64 y = FPConvDbl2Lng(3294168832f);
        Console.WriteLine(y);
        if (y == 3294168832UL) result2 = Pass;

        if (result == Pass && result2 == Pass) return Pass;
        return Fail;

    }
}
