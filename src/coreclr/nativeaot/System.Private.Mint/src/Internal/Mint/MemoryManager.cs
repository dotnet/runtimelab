// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Internal.Reflection.Emit;

namespace Internal.Mint;

public sealed class MemoryManager : IDisposable
{
    private readonly List<Resource> _resources = new List<Resource>();
    private readonly List<PinnedObject> _pinnedObjects = new List<PinnedObject>();

    // TODO: I think Mono also needs a way to allocate dependent mempools for scratch work
    // during compilation.  We should probably have a way to allocate a child memory manager

    public MemoryManager()
    {
    }

    public void Dispose()
    {
        foreach (var resource in _resources)
        {
            resource.Dispose();
        }
        foreach (var pinnedObject in _pinnedObjects)
        {
            pinnedObject.Dispose();
        }
    }

    internal struct Resource
    {
        public IntPtr Value;
        public void Dispose()
        {
            Marshal.FreeHGlobal(Value);
        }
    }

    internal sealed class PinnedObject
    {
        public GCHandle Value;
        public void Dispose()
        {
            Value.Free();
        }
    }

    public IntPtr Allocate(uint size)
    {
        var resource = new Resource { Value = Marshal.AllocHGlobal((int)size) };
        _resources.Add(resource);
        return resource.Value;
    }

    public unsafe T* Allocate<T>() where T : unmanaged
    {
        return (T*)Allocate((uint)sizeof(T));
    }

    public unsafe void* Pin(ref object o)
    {
        var pinnedObject = new PinnedObject { Value = GCHandle.Alloc(o, GCHandleType.Pinned) };
        _pinnedObjects.Add(pinnedObject);
        // FIXME: is this the right thing?
        return *(void**)Unsafe.AsPointer(ref o);
    }
}
