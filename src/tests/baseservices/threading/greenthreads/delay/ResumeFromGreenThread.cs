// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

public class Test_greenthread_delay_ResumeFromGreenThread
{
    public static int Main()
    {
        var tcs = new TaskCompletionSource<int>();
        var yieldingTask = Task.RunAsGreenThread(() =>
        {
            Console.WriteLine($"Before yield");
            tcs.Task.Wait();
            Console.WriteLine($"After yield");
        });

        Thread.Sleep(1000); // wait for the yield

        var resumingTask = Task.RunAsGreenThread(() =>
        {
            Console.WriteLine($"Before resume");
            tcs.SetResult(0);
            Console.WriteLine($"After resume");
        });

        Task.WaitAll(yieldingTask, resumingTask);
        return 100;
    }
}
