// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using System.Diagnostics;
using Xunit;

public class Async2RootReporting
{
    private static TaskCompletionSource<int> cs;


    static async Task<int> Recursive1(int n)
    {
        Task<int> cTask = cs.Task;

        // make some garbage
        object o1 = n;
        //object o2 = n;
        //object o3 = n;
        //object o4 = n;
        //object o5 = n;
        //object o6 = n;
        //object o7 = n;
        //object o8 = n;
        //object o9 = n;
        //object o10 = n;
        //object o11 = n;
        //object o12 = n;

        if (n == 0)
            return await cTask;

        var result = await Recursive1(n - 1);

        Assert.Equal(n, (int)o1);
        //Assert.Equal(n, (int)o2);
        //Assert.Equal(n, (int)o3);
        //Assert.Equal(n, (int)o4);
        //Assert.Equal(n, (int)o5);
        //Assert.Equal(n, (int)o6);
        //Assert.Equal(n, (int)o7);
        //Assert.Equal(n, (int)o8);
        //Assert.Equal(n, (int)o9);
        //Assert.Equal(n, (int)o10);
        //Assert.Equal(n, (int)o11);
        //Assert.Equal(n, (int)o12);

        return result;
    }

    static async2 Task<int> Recursive2(int n)
    {
        Task<int> cTask = cs.Task;

        // make some garbage
        object o1 = n;

// THIS CAUSES CRASHES IN UNWINDER FLAVOR
        //object o2 = n;
        //object o3 = n;
        //object o4 = n;
        //object o5 = n;
        //object o6 = n;
        //object o7 = n;
        //object o8 = n;
        //object o9 = n;
        //object o10 = n;
        //object o11 = n;
        //object o12 = n;

        if (n == 0)
            return await cTask;

        var result = await Recursive2(n - 1);

        Assert.Equal(n, (int)o1);
        //Assert.Equal(n, (int)o2);
        //Assert.Equal(n, (int)o3);
        //Assert.Equal(n, (int)o4);
        //Assert.Equal(n, (int)o5);
        //Assert.Equal(n, (int)o6);
        //Assert.Equal(n, (int)o7);
        //Assert.Equal(n, (int)o8);
        //Assert.Equal(n, (int)o9);
        //Assert.Equal(n, (int)o10);
        //Assert.Equal(n, (int)o11);
        //Assert.Equal(n, (int)o12);

        return result;
    }

    [Fact]
    public static void Test()
    {
        int numStacks = 10000;
        int stackDepth = 100;
        int numGCs = 100;

        Console.WriteLine("async-1 ===================== ");
        var ticks = Environment.TickCount;

        // run a few iterations for 5 seconds total
        while (Environment.TickCount - ticks < 5000)
        {
            cs = new TaskCompletionSource<int>();
            Task[] tasks = new Task[numStacks];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Recursive1(stackDepth);
            }

            var ticksStart = Stopwatch.GetTimestamp();

            for (int i = 0; i < numGCs; i++)
            {
                if (i % 10 == 0)
                    Console.Write('.');

                GC.Collect(0);
            }

            System.Console.WriteLine("Time per Gen0 GC (microseconds): " + (Stopwatch.GetTimestamp() - ticksStart) * 1000000 / numGCs / Stopwatch.Frequency);

            cs.SetResult(100);
            Task.WaitAll(tasks);
        }

        Console.WriteLine("async-2 ===================== ");
        ticks = Environment.TickCount;

        // run a few iterations for 5 seconds total
        while (Environment.TickCount - ticks < 5000)
        {
            cs = new TaskCompletionSource<int>();
            Task[] tasks = new Task[numStacks];
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Recursive2(stackDepth);
            }

            var ticksStart = Stopwatch.GetTimestamp();

            for (int i = 0; i < numGCs; i++)
            {
                if (i % 10 == 0)
                    Console.Write('.');

                GC.Collect(0);
            }

            System.Console.WriteLine("Time per Gen0 GC (microseconds): " + (Stopwatch.GetTimestamp() - ticksStart) * 1000000 / numGCs / Stopwatch.Frequency);

            cs.SetResult(100);
            Task.WaitAll(tasks);
        }
    }
}
