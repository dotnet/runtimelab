// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using Xunit;

public class HugeObjects
{
    [Fact]
    [SkipOnCoreClr("The test measures committed memory between collections.", RuntimeTestModes.AnyGCStress)]
    public static int TestEntryPoint()
    {
        if (!GC.GetConfigurationVariables().TryGetValue("SatoriGC", out object? satori) || satori is not true)
        {
            return 100;
        }

        if (!AllocateAndVerify())
        {
            return 1;
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        return AllocateAndVerify() ? 100 : 1;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool AllocateAndVerify()
    {
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        long before = GC.GetGCMemoryInfo().TotalCommittedBytes;
        byte[][] arrays = new byte[48][];
        long payload = 0;
        for (int index = 0; index < arrays.Length; index++)
        {
            byte[] array = new byte[(index < 32 ? 2 : 4) * 1024 * 1024];
            Array.Fill(array, (byte)(index + 1));
            arrays[index] = array;
            payload += array.Length;
        }

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        long committed = GC.GetGCMemoryInfo().TotalCommittedBytes - before;
        Console.WriteLine($"Payload: {payload}, additional committed: {committed}");
        if (committed > payload + 32 * 1024 * 1024)
        {
            Console.WriteLine("Huge objects committed too much memory after their ends.");
            return false;
        }

        for (int generation = 0; generation <= GC.MaxGeneration; generation++)
        {
            GC.Collect(generation, GCCollectionMode.Forced, blocking: true);
            for (int index = 0; index < arrays.Length; index++)
            {
                byte[] array = arrays[index];
                for (int offset = 0; offset < array.Length; offset++)
                {
                    if (array[offset] != (byte)(index + 1))
                    {
                        Console.WriteLine($"Array {index} was corrupted at {offset} after gen{generation}.");
                        return false;
                    }
                }
            }
        }

        GC.KeepAlive(arrays);
        return true;
    }
}
