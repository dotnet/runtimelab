// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Xunit;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace System.Threading.Tasks.Tests
{
    public static class TaskGreenRtTests
    {
        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsThreadingSupported))]
        [OuterLoop]
        public static void RunRunTests()
        {
            //
            // Test that AttachedToParent is ignored in Task.RunAsGreenThread delegate
            //
            {
                Task tInner = null;

                // Test Run(Action)
                Task t1 = Task.RunAsGreenThread(() =>
                {
                    tInner = new Task(() => { }, TaskCreationOptions.AttachedToParent);
                });
                Debug.WriteLine("RunRunTests - AttachToParentIgnored:      -- Waiting on outer Task.  If we hang, that's a failure");
                t1.Wait();
                tInner.Start();
                tInner.Wait();

                // Test Run(Func<int>)
                Task<int> f1 = Task.RunAsGreenThread(() =>
                {
                    tInner = new Task(() => { }, TaskCreationOptions.AttachedToParent);
                    return 42;
                });
                Debug.WriteLine("RunRunTests - AttachToParentIgnored:      -- Waiting on outer Task<int>.  If we hang, that's a failure");
                f1.Wait();
                tInner.Start();
                tInner.Wait();
            }

            //
            // Test basic functionality w/o cancellation token
            //
            int count = 0;
            Task task1 = Task.RunAsGreenThread(() => { count = 1; });
            Debug.WriteLine("RunRunTests: waiting for a task.  If we hang, something went wrong.");
            task1.Wait();
            Assert.True(count == 1, "    > FAILED.  Task completed but did not run.");
            Assert.True(task1.Status == TaskStatus.RanToCompletion, "    > FAILED.  Task did not end in RanToCompletion state.");

            Task<int> future1 = Task.RunAsGreenThread(() => { return 7; });
            Debug.WriteLine("RunRunTests - Basic w/o CT: waiting for a future.  If we hang, something went wrong.");
            future1.Wait();
            Assert.True(future1.Result == 7, "    > FAILED.  Future completed but did not run.");
            Assert.True(future1.Status == TaskStatus.RanToCompletion, "    > FAILED.  Future did not end in RanToCompletion state.");
            //
            // Test basic functionality w/ uncancelled cancellation token
            //
            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            Task task2 = Task.RunAsGreenThread(() => { count = 21; }, token);
            Debug.WriteLine("RunRunTests: waiting for a task w/ uncanceled token.  If we hang, something went wrong.");
            task2.Wait();
            Assert.True(count == 21, "    > FAILED.  Task w/ uncanceled token completed but did not run.");
            Assert.True(task2.Status == TaskStatus.RanToCompletion, "    > FAILED.  Task w/ uncanceled token did not end in RanToCompletion state.");

            Task<int> future2 = Task.RunAsGreenThread(() => 27, token);
            Debug.WriteLine("RunRunTests: waiting for a future w/ uncanceled token.  If we hang, something went wrong.");
            future2.Wait();
            Assert.True(future2.Result == 27, "    > FAILED.  Future w/ uncanceled token completed but did not run.");
            Assert.True(future2.Status == TaskStatus.RanToCompletion, "    > FAILED.  Future w/ uncanceled token did not end in RanToCompletion state.");
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsThreadingSupported))]
        [OuterLoop]
        public static void RunRunTests_Cancellation_Negative()
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            int count = 0;
            //
            // Test that the right thing is done with a canceled cancellation token
            //
            cts.Cancel();
            Task task3 = Task.RunAsGreenThread(() => { count = 41; }, token);
            Debug.WriteLine("RunRunTests: waiting for a task w/ canceled token.  If we hang, something went wrong.");
            Assert.Throws<AggregateException>(
               () => { task3.Wait(); });
            Assert.False(count == 41, "    > FAILED.  Task w/ canceled token ran when it should not have.");
            Assert.True(task3.IsCanceled, "    > FAILED.  Task w/ canceled token should have ended in Canceled state");

            Task future3 = Task.RunAsGreenThread(() => { count = 47; return count; }, token);
            Debug.WriteLine("RunRunTests: waiting for a future w/ canceled token.  If we hang, something went wrong.");
            Assert.Throws<AggregateException>(
               () => { future3.Wait(); });
            Assert.False(count == 47, "    > FAILED.  Future w/ canceled token ran when it should not have.");
            Assert.True(future3.IsCanceled, "    > FAILED.  Future w/ canceled token should have ended in Canceled state");
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsThreadingSupported))]
        public static void IsGreenThread_Negative_NewThread()
        {
            bool checkIsGreenThread = false;

            // Validate that newly constructed thread via new Thread is not a green thread
            var thread = new Thread(()=>{ checkIsGreenThread = Thread.IsGreenThread; });
            thread.Start();
            thread.Join();
            Assert.False(checkIsGreenThread);
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsThreadingSupported))]
        public static void IsGreenThread_Negative_ThreadPoolItem()
        {
            bool checkIsGreenThread = false;
            SemaphoreSlim semaphore = new SemaphoreSlim(0);

            // Validate that newly constructed thread via new Thread is not a green thread
            Assert.True(ThreadPool.QueueUserWorkItem((object o)=>{ checkIsGreenThread = Thread.IsGreenThread; semaphore.Release(); }));
            semaphore.Wait();

            Assert.False(checkIsGreenThread);
        }

        [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsThreadingSupported))]
        public static void IsGreenThread_Positive()
        {
            //
            // Test basic functionality w/o cancellation token
            //
            int count = 0;
            Task task1 = Task.RunAsGreenThread(() => { count = 1; Assert.True(Thread.IsGreenThread); });
            Debug.WriteLine("RunRunTests: waiting for a task.  If we hang, something went wrong.");
            task1.Wait();
            Assert.True(count == 1, "    > FAILED.  Task completed but did not run.");
            Assert.True(task1.Status == TaskStatus.RanToCompletion, "    > FAILED.  Task did not end in RanToCompletion state.");

            Task<int> future1 = Task.RunAsGreenThread(() => { Assert.True(Thread.IsGreenThread); return 7; });
            Debug.WriteLine("RunRunTests - Basic w/o CT: waiting for a future.  If we hang, something went wrong.");
            future1.Wait();
            Assert.True(future1.Result == 7, "    > FAILED.  Future completed but did not run.");
            Assert.True(future1.Status == TaskStatus.RanToCompletion, "    > FAILED.  Future did not end in RanToCompletion state.");
            //
            // Test basic functionality w/ uncancelled cancellation token
            //
            CancellationTokenSource cts = new CancellationTokenSource();
            CancellationToken token = cts.Token;
            Task task2 = Task.RunAsGreenThread(() => { count = 21; Assert.True(Thread.IsGreenThread); }, token);
            Debug.WriteLine("RunRunTests: waiting for a task w/ uncanceled token.  If we hang, something went wrong.");
            task2.Wait();
            Assert.True(count == 21, "    > FAILED.  Task w/ uncanceled token completed but did not run.");
            Assert.True(task2.Status == TaskStatus.RanToCompletion, "    > FAILED.  Task w/ uncanceled token did not end in RanToCompletion state.");

            Task<int> future2 = Task.RunAsGreenThread(() => { Assert.True(Thread.IsGreenThread); return 27; }, token);
            Debug.WriteLine("RunRunTests: waiting for a future w/ uncanceled token.  If we hang, something went wrong.");
            future2.Wait();
            Assert.True(future2.Result == 27, "    > FAILED.  Future w/ uncanceled token completed but did not run.");
            Assert.True(future2.Status == TaskStatus.RanToCompletion, "    > FAILED.  Future w/ uncanceled token did not end in RanToCompletion state.");
        }
    }
}
