// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

public class Test_greenthread_perf_WaitDeepGT
{
    public static int Main(string[] args)
    {
        if (args.Length < 2 || args[0] != "run" || !int.TryParse(args[1], out int depth))
        {
            Console.WriteLine("Pass arguments 'run <depth>' to run benchmark (e.g. 'run 4').");
            return 100;
        }

        ThreadPool.SetMinThreads(1, 1);
        ThreadPool.SetMaxThreads(1, 1);

        var sw = new Stopwatch();
        int count = 0;

        Task.RunAsGreenThread(() =>
        {
            while (true)
            {
                Recurse(depth);
                count++;
            }
        });

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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Recurse(int depth)
    {
        if (depth > 0)
        {
            Recurse(depth - 1);
        }
        else
        {
            Task.Factory.StartNew(s_emptyAction, TaskCreationOptions.PreferFairness).Wait();
        }
    }

    private static readonly Action s_emptyAction = () => { };
}
