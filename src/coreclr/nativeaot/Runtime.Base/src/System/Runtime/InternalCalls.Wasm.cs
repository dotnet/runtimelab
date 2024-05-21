// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// Internal calls specific to the WASM target.
//
using System.Runtime.CompilerServices;

using Internal.Runtime;

namespace System.Runtime
{
    internal static partial class InternalCalls
    {
        [RuntimeImport(Redhawk.BaseName, "RhpGetRawLastVirtualUnwindFrameRef")]
        [MethodImpl(MethodImplOptions.InternalCall)]
        internal static extern unsafe void* RhpGetRawLastVirtualUnwindFrameRef();

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
