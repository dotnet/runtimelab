// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
﻿using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;

public class TaskBasedAsyncFibonacciWithoutYields
{
    const uint Threshold = 1_000;

    [Fact]
    public static int Test() { return AsyncMain().Result; }
    
    static async Task<int> AsyncMain()
    {
        for (int i = 0; i < 10; i++)
        {
            var sw = new Stopwatch();
            sw.Start();
            uint result = await A(100_000_000);
            Console.WriteLine($"{sw.ElapsedMilliseconds} ms result={result}");
        }

        return 100;
    }

    static async Task<uint> A(uint n)
    {
        uint result = n;
        for (uint i = 0; i < n; i++)
            result = await B(result);
        return result;
    }

    #pragma warning disable CS1998
    static async Task<uint> B(uint n)
    {
        uint result = n;

        result = result * 1_999_999_981;

        result = result * 1_999_999_981;

        result = result * 1_999_999_981;

        return result;
    }
}