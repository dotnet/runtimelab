// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal unsafe partial class Sys
    {
        internal delegate IntPtr ThreadProc(IntPtr parameter);

        [DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_RuntimeThread_CreateThread")]
        internal static extern bool RuntimeThread_CreateThread(IntPtr stackSize, IntPtr startAddress, IntPtr parameter);
    }
}
