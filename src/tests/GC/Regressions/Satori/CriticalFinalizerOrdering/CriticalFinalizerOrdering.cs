// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.ConstrainedExecution;
using System.Threading;
using Xunit;

public static class CriticalFinalizerOrdering
{
    private const int CriticalA = 0;
    private const int CriticalB = 1;
    private const int Ordinary = 2;
    private const int FinalizableCount = 3;

    private static readonly int[] s_finalizationOrder = new int[FinalizableCount];
    private static int s_finalizationSequence;
    private static int s_finalizedCount;

    [Fact]
    public static int TestEntryPoint()
    {
        if (!GC.GetConfigurationVariables().TryGetValue("SatoriGC", out object? satori) || satori is not true)
        {
            return 100;
        }

        PrepareThreadLocalRegion();
        int threadLocalCollections = GC.CollectionCount(0);
        int globalCollections = GC.CollectionCount(1);
        AllocateFinalizables();
        TriggerThreadLocalCollection();
        GC.WaitForPendingFinalizers();

        if (GC.CollectionCount(0) <= threadLocalCollections || GC.CollectionCount(1) != globalCollections)
        {
            Console.WriteLine("The finalizable objects were not collected exclusively by thread-local GC.");
            return 3;
        }

        if (Volatile.Read(ref s_finalizedCount) != FinalizableCount)
        {
            Console.WriteLine($"Expected {FinalizableCount} finalizers, observed {s_finalizedCount}.");
            return 1;
        }

        int ordinaryOrder = Volatile.Read(ref s_finalizationOrder[Ordinary]);
        int criticalAOrder = Volatile.Read(ref s_finalizationOrder[CriticalA]);
        int criticalBOrder = Volatile.Read(ref s_finalizationOrder[CriticalB]);
        if (ordinaryOrder >= criticalAOrder || ordinaryOrder >= criticalBOrder)
        {
            Console.WriteLine($"Finalization order was critical A: {criticalAOrder}, critical B: {criticalBOrder}, ordinary: {ordinaryOrder}.");
            return 2;
        }

        return 100;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AllocateFinalizables()
    {
        var criticalA = new CriticalFinalizable(CriticalA);
        var criticalB = new CriticalFinalizable(CriticalB);
        var ordinary = new OrdinaryFinalizable(Ordinary);
        if (GC.GetGeneration(criticalA) != 0 ||
            GC.GetGeneration(criticalB) != 0 ||
            GC.GetGeneration(ordinary) != 0)
        {
            throw new InvalidOperationException("The finalizable objects must be in the thread-local Gen0 region.");
        }

        GC.KeepAlive(criticalA);
        GC.KeepAlive(criticalB);
        GC.KeepAlive(ordinary);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void PrepareThreadLocalRegion()
    {
        const int AllocationSize = 16 * 1024;
        const int AllocationLimit = 16 * 1024 * 1024;

        for (int allocated = 0; allocated < AllocationLimit; allocated += AllocationSize)
        {
            object?[] buffer = new object?[AllocationSize / IntPtr.Size];
            if (GC.GetGeneration(buffer) == 0)
            {
                return;
            }
        }

        throw new InvalidOperationException("Could not acquire an escape-tracked Gen0 allocation region.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void TriggerThreadLocalCollection()
    {
        const int AllocationSize = 1024;
        const int AllocationLimit = 16 * 1024 * 1024;

        for (int allocated = 0; allocated < AllocationLimit && Volatile.Read(ref s_finalizedCount) == 0; allocated += AllocationSize)
        {
            object?[] buffer = new object?[AllocationSize / IntPtr.Size];
            GC.KeepAlive(buffer);
        }
    }

    private static void RecordFinalization(int index)
    {
        Volatile.Write(ref s_finalizationOrder[index], Interlocked.Increment(ref s_finalizationSequence));
        Interlocked.Increment(ref s_finalizedCount);
    }

    private sealed class OrdinaryFinalizable
    {
        private readonly int _index;

        public OrdinaryFinalizable(int index)
        {
            _index = index;
        }

        ~OrdinaryFinalizable()
        {
            RecordFinalization(_index);
        }
    }

    private sealed class CriticalFinalizable : CriticalFinalizerObject
    {
        private readonly int _index;

        public CriticalFinalizable(int index)
        {
            _index = index;
        }

        ~CriticalFinalizable()
        {
            RecordFinalization(_index);
        }
    }
}
