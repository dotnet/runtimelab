// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

public static class BlockingCollect
{
    private const int NodeCount = 256 * 1024;
    private const long MinimumReclaimedBytes = 4 * 1024 * 1024;

    public static bool IsSatori => GC.GetConfigurationVariables().TryGetValue("SatoriGC", out object? value) && value is true;
    public static bool IsSerialSatori => IsSatori && Environment.GetEnvironmentVariable("DOTNET_gcParallel") == "0";

    [ConditionalTheory(typeof(BlockingCollect), nameof(IsSatori))]
    [InlineData(-1, false)]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public static void ForcedBlockingCollectionIsNotConcurrent(int generation, bool lowLatency)
    {
        Node graph = CreateGraph();
        PrepareHeap();
        GCLatencyMode previousMode = GCSettings.LatencyMode;
        try
        {
            if (lowLatency)
            {
                GCSettings.LatencyMode = GCLatencyMode.LowLatency;
            }

            int before = GC.CollectionCount(Math.Max(1, generation));
            if (generation == -1)
            {
                GC.Collect();
            }
            else
            {
                GC.Collect(generation, GCCollectionMode.Forced, blocking: true);
            }

            Assert.True(GC.CollectionCount(Math.Max(1, generation)) > before);
            GCMemoryInfo info = GC.GetGCMemoryInfo();
            Assert.False(info.Concurrent);
            Assert.Equal(TimeSpan.Zero, info.PauseDurations[1]);
            Console.WriteLine($"Blocking gen{generation}, lowLatency={lowLatency}: passed.");
        }
        finally
        {
            GCSettings.LatencyMode = previousMode;
            GC.KeepAlive(graph);
        }
    }

    [ConditionalTheory(typeof(BlockingCollect), nameof(IsSerialSatori))]
    [InlineData(1, 1)]
    [InlineData(1, 2)]
    [InlineData(2, 1)]
    [InlineData(2, 2)]
    public static void ForcedBlockingCollectionDuringConcurrentMark(int concurrentGeneration, int requestedGeneration)
    {
        Node tenuredGraph = CreateGraph();
        PrepareHeap();
        Node youngGraph = CreateGraph();
        int before = GC.CollectionCount(concurrentGeneration);

        // With no workers, one helping quantum cannot finish this graph.
        GC.Collect(concurrentGeneration, GCCollectionMode.Forced, blocking: false);
        Assert.Equal(before, GC.CollectionCount(concurrentGeneration));

        GC.Collect(requestedGeneration, GCCollectionMode.Forced, blocking: true);
        GCMemoryInfo info = GC.GetGCMemoryInfo();
        Assert.False(info.Concurrent);
        Assert.Equal(TimeSpan.Zero, info.PauseDurations[1]);
        Assert.True(GC.CollectionCount(concurrentGeneration) > before);

        if (concurrentGeneration != info.Generation)
        {
            GCMemoryInfo concurrentCollection = GC.GetGCMemoryInfo(concurrentGeneration == 1 ? GCKind.Ephemeral : GCKind.FullBlocking);
            Assert.True(concurrentCollection.Concurrent);
            Assert.True(concurrentCollection.PauseDurations[1] > TimeSpan.Zero,
                "The running concurrent collection must finish its normal preparation phase.");
        }

        GC.KeepAlive(tenuredGraph);
        GC.KeepAlive(youngGraph);
        Console.WriteLine($"Concurrent gen{concurrentGeneration}, blocking gen{requestedGeneration}: passed.");
    }

    [ConditionalTheory(typeof(BlockingCollect), nameof(IsSatori))]
    [InlineData(false)]
    [InlineData(true)]
    public static void ForcedBlockingCollectionFinishesSweep(bool compacting)
    {
        Node graph = CreateGraph();
        PrepareHeap();
        long before = CreateGarbage();

        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: compacting);
        long reclaimed = before - GC.GetGCMemoryInfo().HeapSizeBytes;
        Assert.True(reclaimed >= MinimumReclaimedBytes, $"Reclaimed {reclaimed} bytes before returning.");

        GC.KeepAlive(graph);
        Console.WriteLine($"Sweep compacting={compacting}: reclaimed {reclaimed} bytes.");
    }

    [ConditionalFact(typeof(BlockingCollect), nameof(IsSerialSatori))]
    public static void NonblockingCollectionStillDefersSweep()
    {
        Node graph = CreateGraph();
        PrepareHeap();
        long before = CreateGarbage();
        int collectionCount = GC.CollectionCount(2);
        long deadline = Environment.TickCount64 + 30000;
        do
        {
            Assert.True(Environment.TickCount64 < deadline, "Concurrent collection did not complete.");
            GC.Collect(2, GCCollectionMode.Forced, blocking: false);
        }
        while (GC.CollectionCount(2) == collectionCount);

        GCMemoryInfo info = GC.GetGCMemoryInfo();
        Assert.True(info.Concurrent);
        Assert.True(info.PauseDurations[1] > TimeSpan.Zero);
        Assert.True(before - info.HeapSizeBytes < MinimumReclaimedBytes, "Nonblocking collection unexpectedly swept synchronously.");

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        Assert.True(before - GC.GetGCMemoryInfo().HeapSizeBytes >= MinimumReclaimedBytes);
        GC.KeepAlive(graph);
        Console.WriteLine("Nonblocking collection retained concurrent marking and deferred sweeping.");
    }

    [ConditionalTheory(typeof(BlockingCollect), nameof(IsSatori))]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public static void CompetingBlockingCollectionsComplete(bool concurrentCollector, bool lowLatency)
    {
        const int CollectorCount = 4;
        const int Iterations = 20;
        Node graph = CreateGraph();
        PrepareHeap();
        GCLatencyMode previousMode = GCSettings.LatencyMode;
        if (lowLatency)
        {
            GCSettings.LatencyMode = GCLatencyMode.LowLatency;
        }
        using Barrier start = new Barrier(CollectorCount);
        Thread[] collectors = new Thread[CollectorCount];
        int completed = 0;
        int stop = 0;
        Thread? concurrent = null;
        try
        {
            if (concurrentCollector)
            {
                concurrent = new Thread(() =>
                {
                    while (Volatile.Read(ref stop) == 0)
                    {
                        GC.Collect(2, GCCollectionMode.Forced, blocking: false);
                        Thread.Yield();
                    }
                })
                {
                    IsBackground = true
                };
                concurrent.Start();
            }

            for (int i = 0; i < collectors.Length; i++)
            {
                int generation = 1 + i % 2;
                collectors[i] = new Thread(() =>
                {
                    for (int iteration = 0; iteration < Iterations; iteration++)
                    {
                        Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(30)), "Collectors did not reach the barrier.");
                        GC.Collect(generation, GCCollectionMode.Forced, blocking: true, compacting: iteration % 2 == 0);
                        Interlocked.Increment(ref completed);
                    }
                })
                {
                    IsBackground = true
                };
                collectors[i].Start();
            }

            foreach (Thread collector in collectors)
            {
                Assert.True(collector.Join(TimeSpan.FromSeconds(30)), "Blocking collector did not complete.");
            }

            Assert.Equal(CollectorCount * Iterations, Volatile.Read(ref completed));
        }
        finally
        {
            Volatile.Write(ref stop, 1);
            if (concurrent is not null)
            {
                Assert.True(concurrent.Join(TimeSpan.FromSeconds(30)), "Concurrent collector did not complete.");
            }

            GCSettings.LatencyMode = previousMode;
            GC.KeepAlive(graph);
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        Assert.False(GC.GetGCMemoryInfo().Concurrent);
        Console.WriteLine($"Competing blocking collectors: {completed} requests completed; concurrent={concurrentCollector}, lowLatency={lowLatency}.");
    }

    private static void PrepareHeap()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Node CreateGraph()
    {
        Node head = new Node();
        for (int i = 1; i < NodeCount; i++)
        {
            head = new Node { Next = head };
        }

        return head;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long CreateGarbage()
    {
        var garbage = new object?[2048][];
        for (int i = 0; i < garbage.Length; i++)
        {
            garbage[i] = new object?[4000 / IntPtr.Size];
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        long before = GC.GetGCMemoryInfo().HeapSizeBytes;
        GC.KeepAlive(garbage);

        return before;
    }

    private sealed class Node
    {
        public Node? Next;
    }
}
