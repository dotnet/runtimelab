// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

public class Test_greenthread_perf_YieldResume
{
    public static int Main(string[] args)
    {
        if (args.Length < 1 || args[0] != "run")
        {
            Console.WriteLine("Pass argument 'run' to run benchmark.");
            return 100;
        }

        ThreadPool.SetMinThreads(1, 1);
        ThreadPool.SetMaxThreads(1, 1);

        var sw = new Stopwatch();
        int count = 0;

        Action greenThreadAction = null;
        greenThreadAction = () =>
        {
            count++;
            Task.RunAsGreenThread(greenThreadAction);
        };
        Task.RunAsGreenThread(greenThreadAction);

        for (int i = 0; i < 8; i++)
        {
            int initialCount = count;
            sw.Restart();
            Thread.Sleep(500);
            int finalCount = count;
            sw.Stop();
            Console.WriteLine($"{sw.Elapsed.TotalNanoseconds / (finalCount - initialCount),15:0.000} ns");
        }

        return 100;
    }
}
