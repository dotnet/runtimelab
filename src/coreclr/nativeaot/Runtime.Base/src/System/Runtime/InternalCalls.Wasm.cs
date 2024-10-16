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
        [RuntimeImport(Redhawk.BaseName, "RhpGetCurrentThreadShadowStackBottom")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern unsafe void* RhpGetCurrentThreadShadowStackBottom();

        [RuntimeImport(Redhawk.BaseName, "RhpGetLastSparseVirtualUnwindFrameRef")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern unsafe void* RhpGetLastSparseVirtualUnwindFrameRef();

        [RuntimeImport(Redhawk.BaseName, "RhpGetLastPreciseVirtualUnwindFrame")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern unsafe void* RhpGetLastPreciseVirtualUnwindFrame();

        [RuntimeImport(Redhawk.BaseName, "RhpThrowNativeException")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void RhpThrowNativeException();

        [RuntimeImport(Redhawk.BaseName, "RhpReleaseNativeException")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void RhpReleaseNativeException();

        [RuntimeImport(Redhawk.BaseName, "RhpAssignRefWithShadowStack")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern void RhpAssignRef(ref object? address, object? obj);
    }
}
