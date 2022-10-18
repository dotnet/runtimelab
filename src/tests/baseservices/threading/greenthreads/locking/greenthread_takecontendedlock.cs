// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;

public class Test_greenthread_takecontendedlock {

    public static object s_lock = new object();
    public static volatile bool s_holdLock;

    public static int Main() {

        var t = Task.RunAsGreenThread(() =>
        {
            Console.WriteLine($"In GreenThread {Thread.IsGreenThread}");
            Task.Delay(2000).Wait();
            lock(s_lock)
            {
                Console.WriteLine($"In GreenThread holding lock");
            }
        });

        s_holdLock = true;

        lock(s_lock)
        {
            Thread.Sleep(3000);
        }

        t.Wait();
        return 100;
    }
}
