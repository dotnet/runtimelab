// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

public class Test_greenthread_delay {

    public static int Main() {

        var t = Task.RunAsGreenThread(GreenWork);

        t.Wait();
        return 100;
    }

    private static void GreenWork()
    {
        Console.WriteLine($"In GreenThread {Thread.IsGreenThread}");
        var red = new Task(RedWork);
        red.Start();
        red.Wait();
        Console.WriteLine($"In GreenThread {Thread.IsGreenThread}");
    }

    private static void RedWork() {
        GC.Collect();
        Thread.Sleep(2000);
    }
}
