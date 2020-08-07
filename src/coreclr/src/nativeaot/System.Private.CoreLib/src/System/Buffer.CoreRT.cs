// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;
using Internal.Runtime.CompilerServices;

namespace System
{
    partial class Buffer
    {
        // Non-inlinable wrapper around the QCall that avoids polluting the fast path
        // with P/Invoke prolog/epilog.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static unsafe void _ZeroMemory(ref byte b, nuint byteLength)
        {
            fixed (byte* bytePointer = &b)
            {
                RuntimeImports.memset(bytePointer, 0, byteLength);
            }
        }

        internal static void BulkMoveWithWriteBarrier(ref byte dmem, ref byte smem, nuint size)
            => RuntimeImports.RhBulkMoveWithWriteBarrier(ref dmem, ref smem, size);

        internal static unsafe void Memcpy(byte* dest, byte* src, int len)
        {
            Debug.Assert(len >= 0, "Negative length in memcpy!");
            Memmove(dest, src, (nuint)len);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static unsafe void __Memmove(byte* dest, byte* src, nuint len) =>
            RuntimeImports.memmove(dest, src, len);

        // This method has different signature for x64 and other platforms and is done for performance reasons.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal unsafe static void Memmove<T>(ref T destination, ref T source, nuint elementCount)
        {
            if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            {
                // Blittable memmove

                RuntimeImports.memmove(
                    (byte*)Unsafe.AsPointer(ref destination),
                    (byte*)Unsafe.AsPointer(ref source),
                    elementCount * (nuint)Unsafe.SizeOf<T>());
            }
            else
            {
                // Non-blittable memmove
                RuntimeImports.RhBulkMoveWithWriteBarrier(
                    ref Unsafe.As<T, byte>(ref destination),
                    ref Unsafe.As<T, byte>(ref source),
                    elementCount * (nuint)Unsafe.SizeOf<T>());
            }
        }
    }
}
