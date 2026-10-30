// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Xunit;

public static class CollectibleLoaderAllocatorCard
{
    [Fact]
    public static int TestEntryPoint()
    {
        if (!GC.GetConfigurationVariables().TryGetValue("SatoriGC", out object? satori) || satori is not true)
        {
            return 100;
        }

#if INDIVIDUAL_PROMOTION
        return VerifyIndividuallyPromotedRegion() ? 100 : 1;
#else
        return VerifyCollectibleLoaderAllocator() ? 100 : 1;
#endif
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool VerifyCollectibleLoaderAllocator()
    {
        // Establish a tenured live set so allocation pressure does not turn the test's Gen1 collections into full collections.
        byte[] ballast = new byte[32 * 1024 * 1024];
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        Array?[] arrays = CreateCollectibleArrays(512 * 1024, 1, separateAllocationRegion: false, out WeakReference loaderAllocatorReference);
        object?[][] survivors = CreateFragmentedRegions();
        int gen2Collections = GC.CollectionCount(2);

        GC.Collect(1, GCCollectionMode.Forced, blocking: true, compacting: true);
        bool compacted = GC.GetGCMemoryInfo().Compacted;
        GC.Collect(1, GCCollectionMode.Forced, blocking: true, compacting: true);
        bool loaderAllocatorSurvived = TryGetGeneration(loaderAllocatorReference, out int generation);

        GC.KeepAlive(arrays);
        GC.KeepAlive(survivors);
        GC.KeepAlive(ballast);
        Console.WriteLine(
            $"Compacted: {compacted}; additional Gen2 collections: {GC.CollectionCount(2) - gen2Collections}; " +
            $"loader allocator survived: {loaderAllocatorSurvived}; generation: {generation}.");

        if (!compacted ||
            GC.CollectionCount(2) != gen2Collections ||
            !loaderAllocatorSurvived ||
            generation >= GC.MaxGeneration)
        {
            return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool VerifyIndividuallyPromotedRegion()
    {
        const int CollectionLimit = 12;

        byte[] ballast = new byte[32 * 1024 * 1024];
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);

        // Keep the allocator in a sparse region, then fill separate regions with reference-free collectible arrays.
        Array?[] arrays = CreateCollectibleArrays(2048, 192, separateAllocationRegion: true, out WeakReference loaderAllocatorReference);
        object?[][] sparseRoots = CreateFragmentedRegions();
        int gen2Collections = GC.CollectionCount(2);
        bool promoted = false;

        for (int collection = 0; collection < CollectionLimit && !promoted; collection++)
        {
            GC.Collect(1, GCCollectionMode.Forced, blocking: true);
            promoted = HasPromotedArray(arrays);
        }

        // Unpromoted instances would mark the allocator directly and hide a missing remembered edge.
        KeepOnlyPromotedArrays(arrays);
        bool allocatorWasEphemeral = TryGetGeneration(loaderAllocatorReference, out int generation) &&
            generation < GC.MaxGeneration;
        if (!promoted || !allocatorWasEphemeral || GC.CollectionCount(2) != gen2Collections)
        {
            Console.WriteLine(
                $"Promotion setup failed: promoted={promoted}, allocator generation={generation}, " +
                $"additional Gen2 collections={GC.CollectionCount(2) - gen2Collections}.");
            return false;
        }

        GC.Collect(1, GCCollectionMode.Forced, blocking: true);
        bool loaderAllocatorSurvived = TryGetGeneration(loaderAllocatorReference, out generation);

        GC.KeepAlive(arrays);
        GC.KeepAlive(sparseRoots);
        GC.KeepAlive(ballast);
        Console.WriteLine(
            $"Individually promoted: {promoted}; additional Gen2 collections: {GC.CollectionCount(2) - gen2Collections}; " +
            $"loader allocator survived: {loaderAllocatorSurvived}; generation: {generation}.");

        return GC.CollectionCount(2) == gen2Collections &&
            loaderAllocatorSurvived &&
            generation < GC.MaxGeneration;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Array?[] CreateCollectibleArrays(int length, int count, bool separateAllocationRegion, out WeakReference loaderAllocatorReference)
    {

        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("SatoriCollectible"),
            AssemblyBuilderAccess.RunAndCollect);
        ModuleBuilder module = assembly.DefineDynamicModule("SatoriCollectible");
        TypeBuilder typeBuilder = module.DefineType(
            "Element",
            TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout,
            typeof(ValueType));
        typeBuilder.DefineField("Value", typeof(long), FieldAttributes.Public);
        Type elementType = typeBuilder.CreateType()!;
        FieldInfo keepaliveField = elementType.GetType().GetField("m_keepalive", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Cannot locate the collectible type's loader allocator field.");
        object loaderAllocator = keepaliveField.GetValue(elementType)
            ?? throw new InvalidOperationException("The collectible type has no managed loader allocator.");
        loaderAllocatorReference = new WeakReference(loaderAllocator);

        if (separateAllocationRegion)
        {
            FillAllocationRegion();
        }

        var arrays = new Array?[count];
        for (int i = 0; i < count; i++)
        {
            arrays[i] = Array.CreateInstance(elementType, length);
        }

        return arrays;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool HasPromotedArray(Array?[] arrays)
    {
        foreach (Array? array in arrays)
        {
            if (array is not null && GC.GetGeneration(array) == GC.MaxGeneration)
            {
                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void KeepOnlyPromotedArrays(Array?[] arrays)
    {
        for (int i = 0; i < arrays.Length; i++)
        {
            if (arrays[i] is Array array && GC.GetGeneration(array) != GC.MaxGeneration)
            {
                arrays[i] = null;
            }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryGetGeneration(WeakReference reference, out int generation)
    {
        object? target = reference.Target;
        if (target is null)
        {
            generation = -1;
            return false;
        }

        generation = GC.GetGeneration(target);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static object?[][] CreateFragmentedRegions()
    {
        const int AllocationSize = 8 * 1024;
        const int AllocationCount = 1024;
        const int SurvivorInterval = 4;

        var survivors = new object?[AllocationCount / SurvivorInterval][];
        for (int i = 0; i < AllocationCount; i++)
        {
            object?[] buffer = new object?[AllocationSize / IntPtr.Size];
            if (i % SurvivorInterval == 0)
            {
                survivors[i / SurvivorInterval] = buffer;
            }

            GC.KeepAlive(buffer);
        }

        return survivors;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void FillAllocationRegion()
    {
        const int AllocationSize = 8 * 1024;
        const int AllocationTotal = 4 * 1024 * 1024;

        for (int allocated = 0; allocated < AllocationTotal; allocated += AllocationSize)
        {
            GC.KeepAlive(new object?[AllocationSize / IntPtr.Size]);
        }
    }
}
