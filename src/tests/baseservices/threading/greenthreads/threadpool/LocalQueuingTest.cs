// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

public class Test_greenthread_threadpool_LocalQueuingTest
{
    public static int Main()
    {
        int result = 100;

        Console.WriteLine("Test queuing a work item to the thread pool with 'preferLocal: true' from a green thread");
        ThreadPool.SetMinThreads(1, 1);
        ThreadPool.SetMaxThreads(1, 1);
        Task.RunAsGreenThread(() =>
        {
            new Func<Task>(async () =>
            {
                // Local work items run in LIFO order
                bool task1Started = false, task2Started = false;
                var task1 = Task.Run(() =>
                {
                    Console.WriteLine("  Task 1 started");
                    if (!task2Started)
                    {
                        Console.WriteLine("    Failed");
                        result = 101;
                    }
                    task1Started = true;
                });
                var task2 = Task.Run(() =>
                {
                    Console.WriteLine("  Task 2 started");
                    if (task1Started)
                    {
                        Console.WriteLine("    Failed");
                        result = 101;
                    }
                    task2Started = true;
                });

                await Task.WhenAll(task1, task2);
            })().Wait();
        }).Wait();


        return result;
    }
}
