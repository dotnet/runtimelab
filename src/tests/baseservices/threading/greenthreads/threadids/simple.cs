// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

public class Test_greenthread_delay
{
    [DllImport("kernel32.dll")]
    static extern uint GetCurrentThreadId();

    static uint GetOSThreadID()
    {
         if (OperatingSystem.IsWindows())
            return GetCurrentThreadId();
        else
            return 0; // Non-Windows is not handled by thius test.
    }

    public static void TestFunction()
    {
        Console.WriteLine($"In GreenThread {Thread.IsGreenThread} with ThreadID {Thread.CurrentThread.ManagedThreadId} on OS thread {GetOSThreadID()}");
        int oldId = Thread.CurrentThread.ManagedThreadId;
        Task.Delay(2000).Wait();
        Console.WriteLine($"In GreenThread {Thread.IsGreenThread} with ThreadID {Thread.CurrentThread.ManagedThreadId} on OS thread {GetOSThreadID()} after wait");
        if (oldId != Thread.CurrentThread.ManagedThreadId) throw new Exception();
    }

    public static int Main() {
        Console.WriteLine("This test is designed to test managed thread id behavior for green threads. In particular, that the thread id remains the same across a resume event, even if the thread hops from one OS thread to another.");

        Console.WriteLine("Run warmup green thread.");
        // Make sure that only 1 of these things has to wait on JITting and such.
        Task t1 = Task.RunAsGreenThread(TestFunction);
        t1.Wait();

        Task[] greenThreads = new Task[10];

        Console.WriteLine("Launch a bunch of green threads. Some of these should share OS thread ID but have different managed thread ids. With luck, we will observe that some green threads have moved from one OS thread to another upon resume.");
        for (int i = 0; i < greenThreads.Length; i++)
            greenThreads[i] = Task.RunAsGreenThread(TestFunction);

        foreach (Task t in greenThreads)
            t.Wait();

        Console.WriteLine("Launch another bunch of green threads. We should see re-use of managed thread IDs in this set, thus verifying that free-ing of managed thread ids is working.");
        for (int i = 0; i < greenThreads.Length; i++)
            greenThreads[i] = Task.RunAsGreenThread(TestFunction);

        foreach (Task t in greenThreads)
            t.Wait();

        Console.WriteLine("Validation of these behaviors is manual, as the behavior of the runtime here is non-deterministic, and difficult to make reliable.");

        return 100;
    }
}
