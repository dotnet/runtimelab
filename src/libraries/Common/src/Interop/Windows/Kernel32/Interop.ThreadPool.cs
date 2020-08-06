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
        internal extern static IntPtr CreateThreadpoolWork(IntPtr pfnwk, IntPtr pv, IntPtr pcbe);

        [DllImport(Libraries.Kernel32)]
        internal extern static void SubmitThreadpoolWork(IntPtr pwk);

        [DllImport(Libraries.Kernel32)]
        internal extern static void CloseThreadpoolWork(IntPtr pwk);

        internal delegate void WaitCallback(IntPtr Instance, IntPtr Context, IntPtr Wait, uint WaitResult);

        [DllImport(Libraries.Kernel32)]
        internal extern static IntPtr CreateThreadpoolWait(IntPtr pfnwa, IntPtr pv, IntPtr pcbe);

        [DllImport(Libraries.Kernel32)]
        internal extern static void SetThreadpoolWait(IntPtr pwa, IntPtr h, IntPtr pftTimeout);

        [DllImport(Libraries.Kernel32)]
        internal extern static void WaitForThreadpoolWaitCallbacks(IntPtr pwa, bool fCancelPendingCallbacks);

        [DllImport(Libraries.Kernel32)]
        internal extern static void CloseThreadpoolWait(IntPtr pwa);
    }
}
