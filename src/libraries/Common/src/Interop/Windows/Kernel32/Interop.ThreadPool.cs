// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static partial class Kernel32
    {
        internal delegate void WorkCallback(IntPtr Instance, IntPtr Context, IntPtr Work);

        [DllImport(Libraries.Kernel32)]
        internal static extern IntPtr CreateThreadpoolWork(IntPtr pfnwk, IntPtr pv, IntPtr pcbe);

        [DllImport(Libraries.Kernel32)]
        internal static extern void SubmitThreadpoolWork(IntPtr pwk);

        [DllImport(Libraries.Kernel32)]
        internal static extern void CloseThreadpoolWork(IntPtr pwk);

        internal delegate void WaitCallback(IntPtr Instance, IntPtr Context, IntPtr Wait, uint WaitResult);

        [DllImport(Libraries.Kernel32)]
        internal static extern IntPtr CreateThreadpoolWait(IntPtr pfnwa, IntPtr pv, IntPtr pcbe);

        [DllImport(Libraries.Kernel32)]
        internal static extern void SetThreadpoolWait(IntPtr pwa, IntPtr h, IntPtr pftTimeout);

        [DllImport(Libraries.Kernel32)]
        internal static extern void WaitForThreadpoolWaitCallbacks(IntPtr pwa, bool fCancelPendingCallbacks);

        [DllImport(Libraries.Kernel32)]
        internal static extern void CloseThreadpoolWait(IntPtr pwa);
    }
}
