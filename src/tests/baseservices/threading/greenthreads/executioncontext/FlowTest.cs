// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

public class Test_greenthread_delay {

    public static int Main() {
        int result = 100;
        var defaultExecutionContext = ExecutionContext.Capture();

        Console.WriteLine("Test flow of non-default execution context");
        var asyncLocal = new AsyncLocal<int>();
        asyncLocal.Value = 1;
        Action taskAction = () =>
        {
            int asyncLocalValueBeforeYield = asyncLocal.Value;
            Console.WriteLine($"  Before yield - OS thread ID: {GetCurrentThreadId()} | managed thread ID: {Environment.CurrentManagedThreadId} | asyncLocal: {asyncLocalValueBeforeYield}");
            if (asyncLocalValueBeforeYield != 1)
            {
                Console.WriteLine($"  Failure before yield - OS thread ID: {GetCurrentThreadId()} | managed thread ID: {Environment.CurrentManagedThreadId} | asyncLocal: {asyncLocalValueBeforeYield}");
                result = 101;
            }

            asyncLocal.Value = 2;
            Task.Delay(1000).Wait();

            int asyncLocalValueAfterYield = asyncLocal.Value;
            Console.WriteLine($"  After yield - OS thread ID: {GetCurrentThreadId()} | managed thread ID: {Environment.CurrentManagedThreadId} | asyncLocal: {asyncLocalValueAfterYield}");
            if (asyncLocalValueAfterYield != 2)
            {
                Console.WriteLine($"  Failure after yield - OS thread ID: {GetCurrentThreadId()} | managed thread ID: {Environment.CurrentManagedThreadId} | asyncLocal: {asyncLocalValueAfterYield}");
                result = 101;
            }
        };

        // Start a few tasks to make it more likely that one would be resumed on a different OS thread than where it yielded
        var tasks = new Task[8];
        for (int i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.RunAsGreenThread(taskAction);
        }
        Task.WaitAll(tasks);

        Console.WriteLine("Test with default execution context");
        Task task;
        ExecutionContext.Run(defaultExecutionContext, _ =>
        {
            task = Task.RunAsGreenThread(() =>
            {
                int asyncLocalValue = asyncLocal.Value;
                Console.WriteLine($"  OS thread ID: {GetCurrentThreadId()} | managed thread ID: {Environment.CurrentManagedThreadId} | asyncLocal: {asyncLocalValue}");
                if (asyncLocalValue != 0)
                {
                    Console.WriteLine($"  Failure - OS thread ID: {GetCurrentThreadId()} | managed thread ID: {Environment.CurrentManagedThreadId} | asyncLocal: {asyncLocalValue}");
                    result = 102;
                }
            });
            task.Wait();
        }, null);

        Console.WriteLine("Test suppressed flow of non-default execution context");
        taskAction = () =>
        {
            int asyncLocalValueBeforeYield = asyncLocal.Value;
            Console.WriteLine($"  Before yield - OS thread ID: {GetCurrentThreadId()} | managed thread ID: {Environment.CurrentManagedThreadId} | asyncLocal: {asyncLocalValueBeforeYield}");
            if (asyncLocalValueBeforeYield != 0)
            {
                Console.WriteLine($"  Failure before yield - OS thread ID: {GetCurrentThreadId()} | managed thread ID: {Environment.CurrentManagedThreadId} | asyncLocal: {asyncLocalValueBeforeYield}");
                result = 103;
            }

            // Flow of the EC through a yield is implicit through thread locals, so suppressing flow has no effect
            asyncLocal.Value = 2;
            using (var afc2 = ExecutionContext.SuppressFlow())
            {
                Task.Delay(1000).Wait();
            }

            int asyncLocalValueAfterYield = asyncLocal.Value;
            Console.WriteLine($"  After yield - OS thread ID: {GetCurrentThreadId()} | managed thread ID: {Environment.CurrentManagedThreadId} | asyncLocal: {asyncLocalValueAfterYield}");
            if (asyncLocalValueAfterYield != 2)
            {
                Console.WriteLine($"  Failure after yield - OS thread ID: {GetCurrentThreadId()} | managed thread ID: {Environment.CurrentManagedThreadId} | asyncLocal: {asyncLocalValueAfterYield}");
                result = 103;
            }
        };

        using (AsyncFlowControl afc = ExecutionContext.SuppressFlow())
        {
            // Start a few tasks to make it more likely that one would be resumed on a different OS thread than where it yielded
            for (int i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.RunAsGreenThread(taskAction);
            }
        }
        Task.WaitAll(tasks);

        return result;
    }

    [DllImport("kernel32.dll")]
    static extern uint GetCurrentThreadId();
}
