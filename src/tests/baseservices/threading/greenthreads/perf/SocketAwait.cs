// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class Test_greenthread_perf_YieldResume
{
    public static int Main(string[] args)
    {
        if (args.Length < 1 || args[0] != "run")
        {
            Console.WriteLine("Pass argument 'run' to run benchmark.");
            return 100;
        }

        ThreadPool.SetMinThreads(2, 1);
        ThreadPool.SetMaxThreads(2, 1);

        var sw = new Stopwatch();
        int count = 0;

        Task.Run(async () =>
        {
            var buffer = new byte[1];
            var listener = new TcpListener(IPAddress.Loopback, 55555);
            listener.Start();
            var socket = await listener.AcceptSocketAsync();
            listener.Stop();

            while (true)
            {
                await socket.ReceiveAsync(buffer);
                await socket.SendAsync(buffer);
            }
        });

        Thread.Sleep(100);

        Task.Run(async () =>
        {
            var buffer = new byte[1];
            var client = new TcpClient();
            await client.ConnectAsync(IPAddress.Loopback, 55555);
            var socket = client.Client;

            while (true)
            {
                await socket.SendAsync(buffer);
                await socket.ReceiveAsync(buffer);
                ++count;
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
}
