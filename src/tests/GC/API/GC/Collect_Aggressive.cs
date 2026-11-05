// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using Xunit;

public class AggressiveCollect
{
    private const long MinimumReclaimedBytes = 2000 * 4000;

    public static bool IsSatori => GC.GetConfigurationVariables().TryGetValue("SatoriGC", out object value) && value is true;

    [Fact]
    public static int TestEntryPoint()
    {
        long before = CreateGarbage();
        GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        long after = GC.GetGCMemoryInfo().TotalCommittedBytes;
        long reclaimed = before - after;
        if (reclaimed < MinimumReclaimedBytes)
        {
            // If we reach this case, the aggressive GC is not releasing as much memory as
            // we wished, something is wrong.
            return 101;
        }
        else
        {
            // Doing some extra allocation (and also trigger GC indirectly) here
            // should be just fine.
            for (int i = 0; i < 10; i++)
            {
                CreateGarbage();
            }
            return 100;
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public static void CompactingCollectWithoutRelocationCompletes(int generation)
    {
        int before = GC.CollectionCount(generation);
        GC.Collect(generation, GCCollectionMode.Forced, blocking: true, compacting: true);
        Assert.True(GC.CollectionCount(generation) > before);
    }

    // Another thread running back-to-back collections starves the allocating thread under the default GC.
    [ConditionalFact(typeof(AggressiveCollect), nameof(IsSatori))]
    public static void ConcurrentAggressiveCollectReclaimsMemory()
    {
        int stop = 0;
        using var started = new ManualResetEventSlim();
        var collector = new Thread(() =>
        {
            started.Set();
            while (Volatile.Read(ref stop) == 0)
            {
                GC.Collect(1, GCCollectionMode.Forced, blocking: true);
                GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                Thread.Yield();
            }
        });
        collector.IsBackground = true;
        collector.Start();

        try
        {
            Assert.True(started.Wait(TimeSpan.FromSeconds(30)));
            for (int iteration = 0; iteration < 20; iteration++)
            {
                long before = CreateGarbage();
                GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                long reclaimed = before - GC.GetGCMemoryInfo().TotalCommittedBytes;
                Assert.True(reclaimed >= MinimumReclaimedBytes, $"Iteration {iteration}: reclaimed {reclaimed} bytes.");
            }
        }
        finally
        {
            Volatile.Write(ref stop, 1);
            Assert.True(collector.Join(TimeSpan.FromSeconds(30)));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static long CreateGarbage()
    {
        byte[][] smallGarbage = new byte[2000][];

        // This will force us to use more than one region in the small object heap
        for (int i = 0; i < 2000; i++)
        {
            // It will roughly span one page
            smallGarbage[i] = new byte[4000];
        }

        // This will force us to use more than one region in the large object heap
        byte[] largeGarbage = new byte[33 * 1024 * 1024];

        // This will force us to use more than one region in the pin object heap
        byte[] pinnedGarbage = GC.AllocateArray<byte>(33 * 1024 * 1024, /* pinned = */true);

        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        long committed = GC.GetGCMemoryInfo().TotalCommittedBytes;

        GC.KeepAlive(smallGarbage);
        GC.KeepAlive(largeGarbage);
        GC.KeepAlive(pinnedGarbage);

        return committed;
    }
}
