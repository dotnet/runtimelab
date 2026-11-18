// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace System
{
    public partial class Buffer
    {
        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern void BulkMoveWithWriteBarrierInternal(ref byte destination, ref byte source, nuint byteCount);

        [MethodImpl(MethodImplOptions.InternalCall)]
        private static extern void BulkClearWithWriteBarrierInternal(ref byte destination, nuint byteCount);

        // Keep the managed typed-helper signatures used by the JIT. Layout discovery belongs to the collector.
        internal static unsafe void BulkMoveWithOldValueWriteBarrier(
            ref byte destination, ref byte source, MethodTable* type, nuint elementSize, nuint elementCount)
        {
            Debug.Assert(elementSize != 0 && elementCount <= nuint.MaxValue / elementSize);
            BulkMoveWithWriteBarrier(ref destination, ref source, elementSize * elementCount);
        }

        internal static unsafe void BulkMoveValueClassWithOldValueWriteBarrier(
            ref byte destination, ref byte source, MethodTable* type, nuint gcLayoutOffset, nuint elementSize)
        {
            Debug.Assert(elementSize != 0);
            BulkMoveWithWriteBarrier(ref destination, ref source, elementSize);
        }

        internal static unsafe void ClearValueClassWithOldValueWriteBarrier(
            ref byte destination, MethodTable* type, nuint gcLayoutOffset, nuint elementSize)
        {
            Debug.Assert(elementSize != 0);
            BulkClearWithWriteBarrier(ref destination, elementSize);
        }

        internal static unsafe void ClearWithOldValueWriteBarrier(
            ref byte destination, MethodTable* type, nuint elementSize, nuint elementCount)
        {
            Debug.Assert(elementSize != 0 && elementCount <= nuint.MaxValue / elementSize);
            BulkClearWithWriteBarrier(ref destination, elementSize * elementCount);
        }

        internal static unsafe void BulkClearWithWriteBarrier(ref byte destination, nuint byteCount)
        {
            while (byteCount != 0)
            {
                nuint chunk = Math.Min(BulkMoveWithWriteBarrierChunk, byteCount);
                if (byteCount > chunk)
                {
                    chunk -= (nuint)Unsafe.AsPointer(ref destination) & (nuint)(sizeof(nuint) - 1);
                }
                BulkClearWithWriteBarrierInternal(ref destination, chunk);
                Thread.FastPollGC();
                destination = ref Unsafe.AddByteOffset(ref destination, chunk);
                byteCount -= chunk;
            }
        }

        // Used by ilmarshalers.cpp
        internal static unsafe void Memcpy(byte* dest, byte* src, int len)
        {
            Debug.Assert(len >= 0, "Negative length in memcpy!");
            Memmove(ref *dest, ref *src, (nuint)(uint)len /* force zero-extension */);
        }

        // Used by ilmarshalers.cpp
        internal static unsafe void Memcpy(byte* pDest, int destIndex, byte[] src, int srcIndex, int len)
        {
            Debug.Assert((srcIndex >= 0) && (destIndex >= 0) && (len >= 0), "Index and length must be non-negative!");
            Debug.Assert(src.Length - srcIndex >= len, "not enough bytes in src");

            Memmove(ref *(pDest + (uint)destIndex), ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(src), (nint)(uint)srcIndex /* force zero-extension */), (uint)len);
        }
    }
}
