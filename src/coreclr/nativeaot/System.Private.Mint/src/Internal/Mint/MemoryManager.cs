// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Internal.Mint.Abstraction;
using Internal.Reflection.Emit;

namespace Internal.Mint;

public sealed class MemoryManager : IDisposable
{
    private readonly List<Resource> _resources = new List<Resource>();
    private readonly List<PinnedObject> _pinnedObjects = new List<PinnedObject>();
    private readonly List<GCHandle> _ownedObjects = new List<GCHandle>();

    // TODO: I think Mono also needs a way to allocate dependent mempools for scratch work
    // during compilation.  We should probably have a way to allocate a child memory manager

    public MemoryManager()
    {
    }

    ~MemoryManager()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    public void Dispose(bool disposing)
    {
        foreach (var resource in _resources)
        {
            resource.Dispose();
        }
        _resources?.Clear();
        foreach (var pinnedObject in _pinnedObjects)
        {
            pinnedObject.Dispose();
        }
        _pinnedObjects?.Clear();
        foreach (var ownedObject in _ownedObjects)
        {
            ownedObject.Free();
        }
        _ownedObjects?.Clear();
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

    public unsafe IntPtr AllocateString(string s)
    {
        var resource = new Resource { Value = Marshal.StringToHGlobalAnsi(s) };
        _resources.Add(resource);
        return resource.Value;
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

    public unsafe T** AllocateArray<T>(int size) where T : unmanaged
    {
        return (T**)Allocate((uint)((uint)sizeof(T*) * size));
    }

    public GCHandle Own(object o)
    {
        var gch = GCHandle.Alloc(o);
        _ownedObjects.Add(gch);
        return gch;
    }
    public unsafe void* Pin(ref object o)
    {
        var pinnedObject = new PinnedObject { Value = GCHandle.Alloc(o, GCHandleType.Pinned) };
        _pinnedObjects.Add(pinnedObject);
        // FIXME: is this the right thing?
        return *(void**)Unsafe.AsPointer(ref o);
    }

    private unsafe void* PinUnsafeWithRefs(ref object o)
    {
        var pinnedObject = new PinnedObject { Value = DynamicMethodAugments.UnsafeGCHandleAlloc(o, GCHandleType.Pinned) };
        _pinnedObjects.Add(pinnedObject);
        return Unsafe.AsPointer(ref o);
    }

    public unsafe byte* AllocateStack(out uint stackSize, out uint redZoneSize, out uint initAlignment)
    {
        const uint INTERP_STACK_SIZE = 1024 * 1024;
        const uint INTERP_REDZONE_SIZE = 8 * 1024;
        uint MINT_STACK_ALIGNMENT = (uint)(2 * sizeof(Abstraction.EEstackval));
        stackSize = INTERP_STACK_SIZE;
        redZoneSize = INTERP_REDZONE_SIZE;
        initAlignment = MINT_STACK_ALIGNMENT;
        var stack = new Abstraction.EEstackvalVisible[INTERP_STACK_SIZE];
        var obj = (object)stack;
        PinUnsafeWithRefs(ref obj);
        var dataRef = MemoryMarshal.GetArrayDataReference(stack);
        var stackStart = (byte*)Unsafe.AsPointer(ref dataRef);
        return stackStart;
    }
}
