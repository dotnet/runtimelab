// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

// An array that does not fit into one granule of a region, together with the filler that follows it,
// is allocated in a region of several granules, and must end inside the last of them.
// An array that would end before that granule, or exactly where it starts, is moved up.
// This allocates arrays of every size around the limits of what fits into regions of one and of two granules.
public static class HugeArrayGranuleBoundary
{
    private const int GranuleSize = 2 * 1024 * 1024;

    // arrays that must keep their contents while the others are allocated and collected
    private static readonly byte[]?[] s_live = new byte[4][];

    [Fact]
    public static int TestEntryPoint()
    {
        if (!GC.GetConfigurationVariables().TryGetValue("SatoriGC", out object? satori) || satori is not true)
        {
            return 100;
        }

        // Each limit is below a multiple of the granule size by about the size of the region header, which is some tens of KB.
        // Object sizes are multiples of 8, so a step of 8 visits every size.
        int round = 0;
        for (int granules = 1; granules <= 2; granules++)
        {
            int limit = granules * GranuleSize;
            for (int length = limit - 64 * 1024; length <= limit; length += 8)
            {
                if (!AllocateAndVerify(length, round++))
                {
                    return 1;
                }
            }
        }

        return 100;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool AllocateAndVerify(int length, int round)
    {
        byte[] array = new byte[length];
        if (array[0] != 0 || array[length / 2] != 0 || array[length - 1] != 0)
        {
            Console.WriteLine($"A new array of {length} bytes is not zeroed.");
            return false;
        }

        byte pattern = PatternFor(length);
        array[0] = pattern;
        array[length / 2] = pattern;
        array[length - 1] = pattern;
        s_live[round % s_live.Length] = array;

        if (round % 256 == 0)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        }

        foreach (byte[]? live in s_live)
        {
            if (live is not null && !HasPattern(live))
            {
                Console.WriteLine($"An array of {live.Length} bytes lost its contents after an array of {length} bytes was allocated.");
                return false;
            }
        }

        return true;
    }

    private static byte PatternFor(int length)
    {
        return (byte)(1 + (length >> 3) % 250);
    }

    private static bool HasPattern(byte[] array)
    {
        byte pattern = PatternFor(array.Length);
        return array[0] == pattern && array[array.Length / 2] == pattern && array[array.Length - 1] == pattern;
    }
}
