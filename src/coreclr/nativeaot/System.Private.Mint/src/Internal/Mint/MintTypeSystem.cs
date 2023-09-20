// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Internal.Reflection.Emit;
using System.Runtime.InteropServices;

using Internal.Mint.Abstraction;

namespace Internal.Mint;

public class MintTypeSystem
{
    private readonly MemoryManager _memoryMananger;
    private readonly object _lock = new();
    private readonly Dictionary<RuntimeType, MonoTypePtr> _types = new();

    private MonoMemManagerPtr _memManagerPtr;

    private readonly MintTypeSystem _parent;

    public MintTypeSystem(MemoryManager memoryManager)
    {
        _memoryMananger = memoryManager;
    }

    public MintTypeSystem(MemoryManager memoryManager, MintTypeSystem parent)
    {
        _memoryMananger = memoryManager;
        _parent = parent;
    }

    internal MemoryManager MemoryManager => _memoryMananger;

    protected MemoryManager Allocator => _memoryMananger;

    protected object Lock => _lock;

    internal MonoTypePtr GetMonoType(RuntimeType runtimeType)
    {
        if (_parent != null && _parent._types.TryGetValue(runtimeType, out var type))
            return type;
        lock (Lock)
        {
            if (_types.TryGetValue(runtimeType, out type))
                return type;
            type = CreateMonoTypeRuntimeTypeImpl(runtimeType);
            _types.Add(runtimeType, type);
            return type;
        }
    }

    private unsafe MonoTypePtr CreateMonoTypeRuntimeTypeImpl(RuntimeType runtimeType)
    {
        var s = new Internal.Mint.Abstraction.MonoTypeInstanceAbstractionNativeAot
        {
            type_code = (int)runtimeType.GetCorElementType(),
            is_byref = (byte)(runtimeType.IsByRef ? 1 : 0),
            gcHandle = GCHandle.ToIntPtr(_memoryMananger.Own(runtimeType))
        };
        var ptr = _memoryMananger.Allocate<Internal.Mint.Abstraction.MonoTypeInstanceAbstractionNativeAot>();
        *ptr = s;
        return new MonoTypePtr(ptr);
    }

    internal unsafe MonoMemManagerPtr GetMemManagerAbstraction()
    {
        if (_memManagerPtr.Value != null)
            return _memManagerPtr;
        lock (Lock)
        {
            var pool = Mint.CreateMemPoolFor(_memoryMananger);
            _memManagerPtr = new MonoMemManagerPtr((MonoMemManagerInstanceAbstraction*)pool);
            return _memManagerPtr;
        }
    }

}
