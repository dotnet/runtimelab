// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

public partial class Test_greenthread_await_AwaitTest
{
    public static int RunAwaitTest(bool inlineContinuation, bool useNonDefaultExecutionContext, bool configureAwait)
    {
        int result = 100;

        Console.WriteLine($"Test await and task continuations - inlineContinuation: {inlineContinuation} | useNonDefaultExecutionContext: {useNonDefaultExecutionContext} | configureAwait: {configureAwait}");
        Task.RunAsGreenThread(() =>
        {
            async Task TestAsync()
            {
                var asyncLocal = new AsyncLocal<int>();
                if (useNonDefaultExecutionContext)
                {
                    asyncLocal.Value = 1;
                }

                Console.WriteLine($"  Before await - on green thread: {Thread.IsGreenThread} | managed thread ID: {Environment.CurrentManagedThreadId}");
                if (!Thread.IsGreenThread)
                {
                    Console.WriteLine("    Failed - Not on green thread");
                    result = 101;
                }

                Action<Task> awaitContinuationAction = _ =>
                {
                    Console.WriteLine($"  After await - on green thread: {Thread.IsGreenThread} | managed thread ID: {Environment.CurrentManagedThreadId}");
                    if (!Thread.IsGreenThread)
                    {
                        Console.WriteLine("    Failed - Not on green thread");
                        result = 101;
                    }
                    if (useNonDefaultExecutionContext && asyncLocal.Value != 1)
                    {
                        Console.WriteLine("    Failed - Execution context did not flow");
                        result = 101;
                    }
                };

                var taskToAwait = Task.Delay(1000);
                if (!inlineContinuation)
                {
                    taskToAwait =
                        taskToAwait.ContinueWith(
                            awaitContinuationAction,
                            TaskContinuationOptions.RunContinuationsAsynchronously);
                }
                if (configureAwait)
                {
                    await taskToAwait.ConfigureAwait(false);
                }
                else
                {
                    await taskToAwait;
                }

                if (inlineContinuation)
                {
                    awaitContinuationAction(null);
                }
            }

            // For more clarity in the output, don't exit this green thread until the await continuation completes, to prevent
            // the managed thread ID from being reused
            var testAsyncTask = TestAsync();
            Console.WriteLine($"  After async call - on green thread: {Thread.IsGreenThread} | managed thread ID: {Environment.CurrentManagedThreadId}");
            testAsyncTask.Wait();
        }).Wait();

        return result;
    }
}
