// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

public class Test_greenthread_perf_SocketWait
{
    public static int Main(string[] args)
    {
        if (args.Length < 1 || args[0] != "run")
        {
            Console.WriteLine("Pass argument 'run' to run benchmark.");
            return 100;
        }

        var sw = new Stopwatch();
        int count = 0;

        var serverThread = new Thread(() =>
        {
            var buffer = new byte[1];
            var listener = new TcpListener(IPAddress.Loopback, 55555);
            listener.Start();
            var socket = listener.AcceptSocket();
            listener.Stop();

            while (true)
            {
                socket.Receive(buffer);
                socket.Send(buffer);
            }
        });
        serverThread.IsBackground = true;
        serverThread.Start();

        Thread.Sleep(100);

        var clientThread = new Thread(() =>
        {
            var buffer = new byte[1];
            var client = new TcpClient();
            client.Connect(IPAddress.Loopback, 55555);
            var socket = client.Client;

            while (true)
            {
                socket.Send(buffer);
                socket.Receive(buffer);
                ++count;
            }
        });
        clientThread.IsBackground = true;
        clientThread.Start();

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
