// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable IDE0060 // Remove unused parameter

using System.Runtime.InteropServices;

namespace Internal.Runtime
{
    internal unsafe partial class FrozenObjectHeapManager
    {
        private static void* ClrVirtualReserve(nuint size)
        {
            void* alloc = Interop.Sys.AlignedAlloc(8, size);
            if (alloc != null)
            {
                NativeMemory.Clear(alloc, size);
            }
            return alloc;
        }

        private static void* ClrVirtualCommit(void* pBase, nuint size)
        {
            // Already 'commited'.
            return pBase;
        }

        private static void ClrVirtualFree(void* pBase, nuint size)
        {
            // This will only be called before an OOM. We have no way to do a partial unmap, so do not try.
        }
    }
}
