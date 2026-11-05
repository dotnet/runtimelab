// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using Xunit;

public static class LargeObjectAllocationContext
{
    // Satori allocates objects of LARGE_OBJECT_THRESHOLD (32 KB) and more in large regions when they do not fit
    // in the thread allocation context, even when they are smaller than LARGE_OBJECT_SIZE (85,000 bytes).
    // A regular allocation context can hold up to roughly PACE_BUDGET / 2 (64 KB) when extended into already-zeroed memory.
    private const int LargeObjectThreshold = 32 * 1024;
    private const long DefaultLohThreshold = 85000;
    private const int RegionSize = 2 * 1024 * 1024;
    private const int Iterations = 10000;

    private static bool IsSatori =>
        GC.GetConfigurationVariables().TryGetValue("SatoriGC", out object? value) && value is true;

    [Fact]
    public static int TestEntryPoint()
    {
        if (!IsSatori)
        {
            return 100;
        }

        int result = 100;
        bool hasConfiguredLohThreshold =
            Environment.GetEnvironmentVariable("DOTNET_GCLOHThreshold") is not null ||
            Environment.GetEnvironmentVariable("COMPlus_GCLOHThreshold") is not null ||
            AppContext.GetData("System.GC.LOHThreshold") is not null;

        if (!GC.GetConfigurationVariables().TryGetValue("LOHThreshold", out object? lohThreshold) ||
            lohThreshold is not long threshold ||
            (hasConfiguredLohThreshold ? threshold < DefaultLohThreshold : threshold != DefaultLohThreshold))
        {
            Console.WriteLine($"Unexpected LOHThreshold: {lohThreshold}");
            result = 1;
        }

        // The first array is small and refills the allocation context when the rest of the context is too small for it.
        // A refill adds up to PACE_BUDGET / 2 + INDEX_GRANULARITY (~66 KB), so at most two 32 KB arrays can fit in the
        // remaining allocation context; the third must miss the context and go to a large region.
        // An 80 KB array never fits in the context and must always go to a large region.
        foreach ((int smallLength, int largeLength, int largeCount) in new[] { (16 * 1024, LargeObjectThreshold, 3), (1024, 80 * 1024, 1) })
        {
            foreach (bool uninitialized in new[] { false, true })
            {
                int inRegularRegion = CountInRegularRegion(smallLength, largeLength, largeCount, uninitialized);
                if (inRegularRegion != 0)
                {
                    Console.WriteLine($"{inRegularRegion} of {Iterations} arrays of {largeLength} bytes (uninitialized={uninitialized}) were allocated in the regular region.");
                    result = 1;
                }
            }
        }

        return result;
    }

    // Allocates a small array, then largeCount arrays of largeLength, and counts how often the last large one
    // is in the region of the small one, that is, in the regular region of the allocation context instead of a large region.
    private static int CountInRegularRegion(int smallLength, int largeLength, int largeCount, bool uninitialized)
    {
        int count = 0;
        int smallElementCount = smallLength / IntPtr.Size;
        for (int i = 0; i < Iterations; i++)
        {
            int gcCount = GC.CollectionCount(0);
            object?[] small = new object?[smallElementCount];
            byte[] large = null!;
            for (int j = 0; j < largeCount; j++)
            {
                large = uninitialized ? GC.AllocateUninitializedArray<byte>(largeLength) : new byte[largeLength];
            }

            if (RegionFor(small) == RegionFor(large) && GC.CollectionCount(0) == gcCount)
            {
                count++;
            }
        }

        return count;
    }

    private static unsafe nuint RegionFor(Array array)
    {
        fixed (byte* address = &MemoryMarshal.GetArrayDataReference(array))
        {
            return (nuint)address & ~(nuint)(RegionSize - 1);
        }
    }
}
