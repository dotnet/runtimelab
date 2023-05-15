// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// TODO-LLVM: Delete this file when Wasm threads are implemented.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace System.Threading
{
    public static partial class ThreadPool
    {
        internal const bool IsWorkerTrackingEnabledInConfig = false;

        internal static bool YieldFromDispatchLoop => false;

#pragma warning disable IDE0060
        internal static bool NotifyWorkItemComplete(object? threadLocalCompletionCountObject, int _ /*currentTimeMs*/)
#pragma warning restore IDE0060
        {
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);
        }

        internal static void NotifyWorkItemProgress()
        {
        }

        internal static bool NotifyThreadBlocked()
        {
            return false;
        }

        internal static void NotifyThreadUnblocked()
        {
        }

        internal static object GetOrCreateThreadLocalCompletionCountObject()
        {
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);
        }

        public static int ThreadCount => throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

#pragma warning disable IDE0060
        private static RegisteredWaitHandle RegisterWaitForSingleObject(
            WaitHandle waitObject,
            WaitOrTimerCallback callBack,
            object state,
            uint millisecondsTimeOutInterval,
            bool executeOnlyOnce,
            bool flowExecutionContext) =>
#pragma warning restore IDE0060
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

        internal static unsafe void RequestWorkerThread() =>
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

        [CLSCompliant(false)]
        [SupportedOSPlatform("windows")]
        public static unsafe bool UnsafeQueueNativeOverlapped(NativeOverlapped* overlapped)
        {
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);
        }

        public static long CompletedWorkItemCount =>
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

        public static bool SetMaxThreads(int workerThreads, int completionPortThreads) =>
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

        public static void GetMaxThreads(out int workerThreads, out int completionPortThreads) =>
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

        public static bool SetMinThreads(int workerThreads, int completionPortThreads) =>
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

        public static void GetMinThreads(out int workerThreads, out int completionPortThreads) =>
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

        public static void GetAvailableThreads(out int workerThreads, out int completionPortThreads) =>
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

        [Obsolete("ThreadPool.BindHandle(IntPtr) has been deprecated. Use ThreadPool.BindHandle(SafeHandle) instead.")]
        [SupportedOSPlatform("windows")]
        public static bool BindHandle(IntPtr osHandle)
        {
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported); // Replaced by ThreadPoolBoundHandle.BindHandle
        }

        [SupportedOSPlatform("windows")]
        public static bool BindHandle(SafeHandle osHandle)
        {
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported); // Replaced by ThreadPoolBoundHandle.BindHandle
        }
    }

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

    internal partial class TimerQueue
    {
#pragma warning disable IDE0060
        private TimerQueue(int id) =>
#pragma warning restore IDE0060
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

#pragma warning disable CA1822
#pragma warning disable IDE0060
        private unsafe bool SetTimer(uint actualDuration) =>
#pragma warning restore IDE0060
#pragma warning restore CA1822
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);
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

    [System.Runtime.Versioning.UnsupportedOSPlatformAttribute("browser")]
    public sealed class RegisteredWaitHandle : MarshalByRefObject
    {
#pragma warning disable IDE0060
        internal RegisteredWaitHandle(SafeWaitHandle waitHandle, _ThreadPoolWaitOrTimerCallback callbackHelper,
            uint millisecondsTimeout, bool repeating) =>
#pragma warning restore IDE0060
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);

        public bool Unregister(WaitHandle waitObject) =>
            throw new PlatformNotSupportedException(SR.Arg_PlatformNotSupported);
    }
}
