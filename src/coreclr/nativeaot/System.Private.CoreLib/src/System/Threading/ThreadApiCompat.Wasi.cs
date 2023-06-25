// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// TODO-LLVM: Delete this file when Wasm threads are implemented.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace System.Threading
{
    public sealed class ThreadPoolBoundHandle : IDisposable
    {
        private ThreadPoolBoundHandle() { }

        [CLSCompliant(false)]
        public unsafe NativeOverlapped* UnsafeAllocateNativeOverlapped(IOCompletionCallback callback, object? state, object? pinData) =>
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

        public SafeHandle Handle
        {
            get { throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported); }
        }

        [CLSCompliant(false)]
        public unsafe NativeOverlapped* AllocateNativeOverlapped(PreAllocatedOverlapped preAllocated) =>
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

        [CLSCompliant(false)]
        public unsafe NativeOverlapped* AllocateNativeOverlapped(IOCompletionCallback callback, object? state, object? pinData) =>
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

        public static unsafe ThreadPoolBoundHandle BindHandle(SafeHandle handle) =>
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

        [CLSCompliant(false)]
        public unsafe void FreeNativeOverlapped(NativeOverlapped* overlapped) =>
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

        [CLSCompliant(false)]
        public static unsafe object GetNativeOverlappedState(NativeOverlapped* overlapped) =>
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

        public void Dispose() { }
    }

    public sealed class PreAllocatedOverlapped : IDisposable
    {
        [CLSCompliant(false)]
        public PreAllocatedOverlapped(IOCompletionCallback callback, object? state, object? pinData)
        {
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);
        }

        [CLSCompliant(false)]
        public static PreAllocatedOverlapped UnsafeCreate(IOCompletionCallback callback, object? state, object? pinData)
        {
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);
        }

        public void Dispose() { }
    }
}
