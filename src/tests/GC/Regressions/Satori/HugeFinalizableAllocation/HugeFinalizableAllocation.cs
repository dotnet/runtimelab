// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Xunit;

public static class HugeFinalizableAllocation
{
    private const int PayloadSize = 3 * 1024 * 1024;
    private static int s_finalizedCount;
    private static int s_corruptedCount;

    [Fact]
    public static int TestEntryPoint()
    {
        if (!GC.GetConfigurationVariables().TryGetValue("SatoriGC", out object? satori) || satori is not true)
        {
            return 100;
        }

        for (int iteration = 0; iteration < 2; iteration++)
        {
            if (!AllocateAndVerifyLive())
            {
                return 1;
            }

            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            if (Volatile.Read(ref s_finalizedCount) != iteration + 1 || Volatile.Read(ref s_corruptedCount) != 0)
            {
                Console.WriteLine($"Expected {iteration + 1} intact finalizations; finalized={s_finalizedCount}, corrupted={s_corruptedCount}.");
                return 2;
            }

            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        }

        return 100;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool AllocateAndVerifyLive()
    {
        long before = GC.GetAllocatedBytesForCurrentThread();
        var value = new HugeFinalizable();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        if (allocated < PayloadSize)
        {
            Console.WriteLine($"Expected an object of at least {PayloadSize} bytes; allocated={allocated}.");
            return false;
        }

        for (int generation = 0; generation <= GC.MaxGeneration; generation++)
        {
            GC.Collect(generation, GCCollectionMode.Forced, blocking: true);
            if (!value.HasIntactPayload())
            {
                Console.WriteLine($"Huge finalizable object was corrupted after Gen{generation} collection.");
                return false;
            }
        }

        GC.KeepAlive(value);
        return true;
    }

    [StructLayout(LayoutKind.Explicit, Size = PayloadSize)]
    private sealed class HugeFinalizable
    {
        [FieldOffset(0)]
        private readonly byte _first;
        [FieldOffset(PayloadSize - 1)]
        private readonly byte _last;

        public HugeFinalizable()
        {
            _first = 0x3A;
            _last = 0xC5;
        }

        public bool HasIntactPayload()
        {
            return _first == 0x3A && _last == 0xC5;
        }

        ~HugeFinalizable()
        {
            if (!HasIntactPayload())
            {
                Interlocked.Increment(ref s_corruptedCount);
            }

            Interlocked.Increment(ref s_finalizedCount);
        }
    }
}
