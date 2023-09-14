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

public sealed class MintTypeSystem
{
    private readonly MemoryManager _memoryMananger;
    private readonly object _lock = new();
    private readonly Dictionary<MethodBase, MonoMethodPtr> _methods = new();

    public MintTypeSystem(MemoryManager memoryManager)
    {
        _memoryMananger = memoryManager;
    }

    internal MemoryManager MemoryManager => _memoryMananger;

    internal MonoMethodPtr CreateMonoMethodPointer(DynamicMethod dynamicMethod)
    {
        lock (_lock)
        {
            if (_methods.TryGetValue(dynamicMethod, out var method))
                return method;
            method = CreateMonoMethodPointerDynMethodImpl(dynamicMethod);
            _methods.Add(dynamicMethod, method);
            return method;
        }
    }

    private unsafe MonoMethodPtr CreateMonoMethodPointerDynMethodImpl(DynamicMethod dynamicMethod)
    {
        var s = new Internal.Mint.Abstraction.MonoMethodInstanceAbstractionNativeAot
        {
            name = (byte*)_memoryMananger.AllocateString(dynamicMethod.Name),
            klass = IntPtr.Zero,
            get_signature = IntPtr.Zero,
            get_header = IntPtr.Zero,
            gcHandle = GCHandle.ToIntPtr(_memoryMananger.Own(new OwnedDynamicMethod(dynamicMethod)))
        };
        var ptr = _memoryMananger.Allocate<Internal.Mint.Abstraction.MonoMethodInstanceAbstractionNativeAot>();
        *ptr = s;
        return new MonoMethodPtr(ptr);
    }

    private sealed record OwnedDynamicMethod(DynamicMethod DynamicMethod);
}
