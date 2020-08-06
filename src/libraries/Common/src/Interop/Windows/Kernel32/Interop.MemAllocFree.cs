// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

internal static partial class Interop
{
    internal static unsafe partial class Kernel32
    {
        [DllImport(Libraries.Kernel32)]
        internal static extern IntPtr GetProcessHeap();

        [DllImport(Libraries.Kernel32)]
        internal static extern IntPtr HeapAlloc(IntPtr hHeap, uint dwFlags, UIntPtr dwBytes);

        [DllImport(Libraries.Kernel32)]
        internal static extern int HeapFree(IntPtr hHeap, uint dwFlags, IntPtr lpMem);
    }

    internal static IntPtr MemAlloc(UIntPtr sizeInBytes)
    {
        IntPtr allocatedMemory = Interop.Kernel32.HeapAlloc(Interop.Kernel32.GetProcessHeap(), 0, sizeInBytes);
        if (allocatedMemory == IntPtr.Zero)
        {
            throw new OutOfMemoryException();
        }
        return allocatedMemory;
    }

    internal static void MemFree(IntPtr allocatedMemory)
    {
        Interop.Kernel32.HeapFree(Interop.Kernel32.GetProcessHeap(), 0, allocatedMemory);
    }
}
