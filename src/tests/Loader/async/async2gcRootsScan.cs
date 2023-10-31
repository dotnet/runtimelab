// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using System.Diagnostics;
using Xunit;

public class Async2RootReporting
{
    private static TaskCompletionSource<int> cs = new TaskCompletionSource<int>();

    // static async Task<int> Recursive(int n)
    static async2 int Recursive(int n)
    {
        Task<int> cTask = cs.Task;

        // make some garbage
        object o = n;

        if (n == 0)
            return await cTask;

        var result = await Recursive(n - 1);

        Assert.Equal(n, (int)o);

        return result;
    }

    [Fact]
    public static void Test()
    {
        int numStacks = 1000;
        int stackDepth = 1000;
        int numGCs = 200;

        Task[] tasks = new Task[numStacks];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Recursive(stackDepth);
        }

        Stopwatch sw = Stopwatch.StartNew();

        // should not take too long since we are not alocating anything new.
        for (int i = 0; i < numGCs; i++)
        {
            if (i % 10 == 0)
                Console.Write('.');

            GC.Collect(0);

            if (sw.ElapsedMilliseconds > 1000000)
                Assert.Fail("Gen1 GCs take too long");
        }

        System.Console.WriteLine("Time: " + sw.ElapsedMilliseconds);

        cs.SetResult(100);
        Task.WaitAll(tasks);
    }
}
