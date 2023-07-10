// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime;
using System.Runtime.InteropServices;

using Internal.NativeFormat;
using Internal.Runtime.Augments;

namespace Internal.Runtime.CompilerServices
{
    internal unsafe struct UnboxingStubTargetsMap
    {
        private static readonly object s_lock = new();
        private static UnboxingStubTargetsMap s_unboxingStubTargets;
        private static UnboxingStubTargetsMap s_unboxingAndInstantiatingStubTargets;

        private uint _count;
        private volatile UnboxingStubTargetMapping* _mappings;

        public static IntPtr GetTargetOfUnboxingStub(IntPtr functionPointer)
        {
            return GetTarget(ref s_unboxingStubTargets, functionPointer, ReflectionMapBlob.UnboxingStubMap);
        }

        public static IntPtr GetTargetOfUnboxingAndInstantiatingStub(IntPtr functionPointer)
        {
            return GetTarget(ref s_unboxingAndInstantiatingStubTargets, functionPointer, ReflectionMapBlob.UnboxingAndInstantiatingStubMap);
        }

        private static IntPtr GetTarget(ref UnboxingStubTargetsMap mapRef, IntPtr functionPointer, ReflectionMapBlob id)
        {
            UnboxingStubTargetMapping* mappings = GetOrCreateMap(ref mapRef, id);
            UnboxingStubTargetMapping mapping = new() { Stub = functionPointer };

            int index = MemoryExtensions.BinarySearch(new(mappings, (int)mapRef._count), mapping);
            if (index >= 0)
            {
                return mappings[index].Target;
            }

            return 0;
        }

        private static UnboxingStubTargetMapping* GetOrCreateMap(ref UnboxingStubTargetsMap mapRef, ReflectionMapBlob id)
        {
            UnboxingStubTargetMapping* mappings = mapRef._mappings;
            if (mappings == null)
            {
                lock (s_lock)
                {
                    if (mapRef._mappings == null)
                    {
                        mappings = CreateMap(ref mapRef, id);
                    }
                }
            }

            return mappings;
        }

        private static UnboxingStubTargetMapping* CreateMap(ref UnboxingStubTargetsMap mapRef, ReflectionMapBlob id)
        {
            // Optimize the single-module case to avoid allocating and directly use the blob.
            UnboxingStubTargetMapping* singleMappingsBlob = null;
            uint allMappingsCount = 0;

            ReadOnlySpan<TypeManagerHandle> modules = RuntimeAugments.GetLoadedModules();
            foreach (TypeManagerHandle module in modules)
            {
                byte* mappings;
                uint mappingsSize;
                if (RuntimeImports.RhFindBlob(module, (uint)id, &mappings, &mappingsSize))
                {
                    if (allMappingsCount == 0)
                    {
                        singleMappingsBlob = (UnboxingStubTargetMapping*)mappings;
                    }
                    else
                    {
                        singleMappingsBlob = null;
                    }

                    allMappingsCount += mappingsSize / (uint)sizeof(UnboxingStubTargetMapping);
                }
            }

            UnboxingStubTargetMapping* allMappings;
            if (singleMappingsBlob == null)
            {
                allMappings = (UnboxingStubTargetMapping*)NativeMemory.Alloc(allMappingsCount, (nuint)sizeof(UnboxingStubTargetMapping));

                byte* allMappingsBytes = (byte*)allMappings;
                foreach (TypeManagerHandle module in modules)
                {
                    byte* mappings;
                    uint mappingsSize;
                    if (RuntimeImports.RhFindBlob(module, (uint)id, &mappings, &mappingsSize))
                    {
                        NativeMemory.Copy(mappings, allMappingsBytes, mappingsSize);
                        allMappingsBytes += mappingsSize;
                    }
                }
            }
            else
            {
                allMappings = singleMappingsBlob;
            }

            MemoryExtensions.Sort(new Span<UnboxingStubTargetMapping>(allMappings, (int)allMappingsCount));

            mapRef._count = allMappingsCount;
            mapRef._mappings = allMappings;
            return allMappings;
        }

        private struct UnboxingStubTargetMapping : IComparable<UnboxingStubTargetMapping>
        {
            public IntPtr Stub;
            public IntPtr Target;

            public readonly int CompareTo(UnboxingStubTargetMapping other) => Stub.CompareTo(other.Stub);
        }
    }
}
