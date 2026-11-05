// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Xunit;

// An array that can never fit in the address space must fail with OutOfMemoryException,
// without scanning the whole page map, and must leave the heap usable.
public static class HugeArrayOutOfMemory
{
    // The largest element size allowed in an array.
    [InlineArray(0xFFFF)]
    private struct MaxSizeElement
    {
        private byte _element0;
    }

    [Fact]
    public static int TestEntryPoint()
    {
        if (!GC.GetConfigurationVariables().TryGetValue("SatoriGC", out object? satori) || satori is not true)
        {
            return 100;
        }

        for (int iteration = 0; iteration < 3; iteration++)
        {
            // 3 * 0x55555555 = 0xFFFFFFFF elements, ~256 TiB.
            // The page for it would need more 1 GiB entries than the page map has.
            Stopwatch stopwatch = Stopwatch.StartNew();
            if (!ThrowsOutOfMemory())
            {
                return 1;
            }

            Console.WriteLine($"Iteration {iteration}: OutOfMemoryException after {stopwatch.ElapsedMilliseconds} ms.");
        }

        // The heap is still usable.
        object[] objects = new object[1024];
        for (int i = 0; i < objects.Length; i++)
        {
            objects[i] = new byte[i * 64];
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        for (int i = 0; i < objects.Length; i++)
        {
            if (((byte[])objects[i]).Length != i * 64)
            {
                Console.WriteLine($"Object {i} is corrupted.");
                return 2;
            }
        }

        GC.KeepAlive(objects);
        return 100;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool ThrowsOutOfMemory()
    {
        try
        {
            MaxSizeElement[,] array = new MaxSizeElement[3, 0x55555555];
            Console.WriteLine($"Unexpectedly allocated an array of {array.LongLength} elements.");
            GC.KeepAlive(array);
            return false;
        }
        catch (OutOfMemoryException)
        {
            return true;
        }
    }
}
