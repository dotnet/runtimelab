// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Threading
{
    /// <summary>
    /// Copied from mono extended - not used when "FeatureWasmThreads == true"
    /// </summary>
    public sealed class PreAllocatedOverlapped : System.IDisposable
    {
        [CLSCompliantAttribute(false)]
        public PreAllocatedOverlapped(IOCompletionCallback callback, object? state, object? pinData) { }
        [CLSCompliantAttribute(false)]
        public static PreAllocatedOverlapped UnsafeCreate(IOCompletionCallback callback, object? state, object? pinData) => new PreAllocatedOverlapped(callback, state, pinData);
        public void Dispose() { }
#pragma warning disable CA1822
        internal void AddRef() { }
        internal void Release() { }
#pragma warning restore CA1822
        internal ThreadPoolBoundHandleOverlapped _overlapped;
    }
}
