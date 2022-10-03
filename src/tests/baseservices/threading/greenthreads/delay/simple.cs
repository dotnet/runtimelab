// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

public class Test_greenthread_delay {

    public static int Main() {

        var t = Task.RunAsGreenThread(() =>
        {
            Console.WriteLine($"In GreenThread {Thread.IsGreenThread}");
            Task.Delay(2000).Wait();
            Console.WriteLine($"In GreenThread {Thread.IsGreenThread}");
        });

        t.Wait();
        return 100;
    }
}
