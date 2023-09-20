// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Internal.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Internal.Mint.Abstraction;

internal abstract unsafe class VTableProvider : IDisposable
{
    private object _lock = new();
    private readonly List<IntPtr> _vtables = new();
    public VTableProvider() { }

    internal object Lock => _lock;

    private T* AllocVT<T>() where T : unmanaged
    {
        lock (Lock)
        {
            var ptr = (T*)Marshal.AllocHGlobal(sizeof(T));
            _vtables.Add((IntPtr)ptr);
            return ptr;
        }
    }

    ~VTableProvider()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_vtables != null)
        {
            foreach (var vtable in _vtables)
            {
                Marshal.FreeHGlobal(vtable);
            }
            _vtables.Clear();
        }
    }

    protected delegate void VTableFillFunc<T>(T* vtable) where T : unmanaged;

    protected T* GetOrAddVTable<T>(ref T* vtable, VTableFillFunc<T> filler) where T : unmanaged
    {
        if (vtable != null)
            return vtable;
        lock (Lock)
        {
            if (vtable != null)
                return vtable;
            vtable = AllocVT<T>();
            filler(vtable);
            return vtable;
        }
    }
}
