// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Internal calls specific to the WASM target.
//
using System.Runtime.CompilerServices;

namespace System.Runtime
{
    internal static partial class InternalCalls
    {
        [RuntimeImport(RuntimeLibrary, "RhpGetCurrentThreadShadowStackBottom")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern unsafe void* RhpGetCurrentThreadShadowStackBottom();

        [RuntimeImport(RuntimeLibrary, "RhpGetLastSparseVirtualUnwindFrameRef")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern unsafe void* RhpGetLastSparseVirtualUnwindFrameRef();

        [RuntimeImport(RuntimeLibrary, "RhpGetLastPreciseVirtualUnwindFrame")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern unsafe void* RhpGetLastPreciseVirtualUnwindFrame();

        [RuntimeImport(RuntimeLibrary, "RhpThrowNativeException")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void RhpThrowNativeException();

        [RuntimeImport(RuntimeLibrary, "RhpReleaseNativeException")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void RhpReleaseNativeException();

        [RuntimeImport(RuntimeLibrary, "RhpAssignRefWithShadowStack")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void RhpAssignRef(ref object? address, object? obj);
    }
}
