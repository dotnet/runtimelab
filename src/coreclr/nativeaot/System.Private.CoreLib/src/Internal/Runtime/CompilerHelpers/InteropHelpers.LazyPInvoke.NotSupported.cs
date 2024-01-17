// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;

namespace Internal.Runtime.CompilerHelpers
{
    internal static partial class InteropHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static unsafe IntPtr ResolvePInvoke(MethodFixupCell* _)
        {
            // We want to aggressively inline this method so that the fixup cell is not retained.
            return ThrowLazyPInvokeResolutionNotSupportedException();
        }

        private static IntPtr ThrowLazyPInvokeResolutionNotSupportedException()
        {
            // TODO-LLVM-Upstream: use a proper SR.* resource.
            // TOOD-LLVM-Upstream: factor the "supported" path as an additional file instead of the ifdef.
            throw new PlatformNotSupportedException("""
                Lazy PInvoke resolution is not supported when targeting WebAssembly.
                See https://github.com/dotnet/runtimelab/blob/feature/NativeAOT-LLVM/docs/using-nativeaot/wasm-interop.md.
                """);
        }
    }
}
