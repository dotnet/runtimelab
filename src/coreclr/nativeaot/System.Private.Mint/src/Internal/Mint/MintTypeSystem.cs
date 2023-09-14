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
    private readonly Dictionary<MethodBase, MonoMethodSignaturePtr> _signatures = new();
    private readonly Dictionary<MethodBase, MonoMethodHeaderPtr> _headers = new();

    public MintTypeSystem(MemoryManager memoryManager)
    {
        _memoryMananger = memoryManager;
    }

    internal MemoryManager MemoryManager => _memoryMananger;

    internal MonoMethodPtr GetMonoMethodPointer(DynamicMethod dynamicMethod)
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

    private MonoMethodSignaturePtr GetMonoMethodSignature(OwnedDynamicMethod dynamicMethod)
    {
        lock (_lock)
        {
            if (_signatures.TryGetValue(dynamicMethod.DynamicMethod, out var signature))
                return signature;
            signature = CreateMonoMethodSignatureDynMethodImpl(dynamicMethod);
            _signatures.Add(dynamicMethod.DynamicMethod, signature);
            return signature;
        }
    }

    private MonoMethodHeaderPtr GetMonoMethodHeader(OwnedDynamicMethod dynamicMethod)
    {
        lock (_lock)
        {
            if (_headers.TryGetValue(dynamicMethod.DynamicMethod, out var header))
                return header;
            header = CreateMonoMethodHeaderDynMethodImpl(dynamicMethod);
            _headers.Add(dynamicMethod.DynamicMethod, header);
            return header;
        }
    }

    private unsafe MonoMethodPtr CreateMonoMethodPointerDynMethodImpl(DynamicMethod dynamicMethod)
    {
        var s = new Internal.Mint.Abstraction.MonoMethodInstanceAbstractionNativeAot
        {
            name = (byte*)_memoryMananger.AllocateString(dynamicMethod.Name),
            klass = IntPtr.Zero, // TODO
            get_signature = &VTables.methodGetSignatureImpl,
            get_header = &VTables.methodGetHeaderImpl, // TODO
            gcHandle = GCHandle.ToIntPtr(_memoryMananger.Own(new OwnedDynamicMethod(dynamicMethod, this)))
        };
        var ptr = _memoryMananger.Allocate<Internal.Mint.Abstraction.MonoMethodInstanceAbstractionNativeAot>();
        *ptr = s;
        return new MonoMethodPtr(ptr);
    }

    private unsafe MonoMethodSignaturePtr CreateMonoMethodSignatureDynMethodImpl(OwnedDynamicMethod dynamicMethod)
    {
        // N.B: the signature doesn't have the same lifetime as the MonoMethodPtr
        // as Mono can allocate and free signatures independently of the method
        var s = new Internal.Mint.Abstraction.MonoMethodSignatureInstanceAbstractionNativeAot
        {
            param_count = dynamicMethod.DynamicMethod.GetParameters().Length,
            hasthis = dynamicMethod.DynamicMethod.IsStatic ? (byte)0 : (byte)1,
            ret_ult = IntPtr.Zero, // TODO
            gcHandle = GCHandle.ToIntPtr(_memoryMananger.Own(dynamicMethod))
        };
        var ptr = _memoryMananger.Allocate<Internal.Mint.Abstraction.MonoMethodSignatureInstanceAbstractionNativeAot>();
        *ptr = s;
        return new MonoMethodSignaturePtr(ptr);
    }

    private unsafe MonoMethodHeaderPtr CreateMonoMethodHeaderDynMethodImpl(OwnedDynamicMethod ownedDynamicMethod)
    {
        var s = new Internal.Mint.Abstraction.MonoMethodHeaderInstanceAbstractionNativeAot
        {
            code_size = 0, // TODO
            max_stack = 0, // TODO
            num_locals = 0, // TODO
            num_clauses = 0, // TODO
            init_locals = 0, // TODO
            get_local_sig = IntPtr.Zero, // TODO
            get_code = IntPtr.Zero, // TODO
            get_ip_offset = IntPtr.Zero, // TODO
            gcHandle = GCHandle.ToIntPtr(_memoryMananger.Own(ownedDynamicMethod))
        };
        var ptr = _memoryMananger.Allocate<Internal.Mint.Abstraction.MonoMethodHeaderInstanceAbstractionNativeAot>();
        *ptr = s;
        return new MonoMethodHeaderPtr(ptr);
    }

    private sealed record OwnedDynamicMethod(DynamicMethod DynamicMethod, MintTypeSystem Owner);

    private static T Unpack<T>(IntPtr ptr) where T : class
    {
        return GCHandle.FromIntPtr(ptr).Target as T;
    }

    private static class VTables
    {
        [UnmanagedCallersOnly]
        public static unsafe MonoMethodSignatureInstanceAbstractionNativeAot* methodGetSignatureImpl(MonoMethodInstanceAbstractionNativeAot* method)
        {
            var owned = Unpack<OwnedDynamicMethod>(method->gcHandle);
            return owned.Owner.GetMonoMethodSignature(owned).Value;
        }

        [UnmanagedCallersOnly]
        public static unsafe MonoMethodHeaderInstanceAbstractionNativeAot* methodGetHeaderImpl(MonoMethodInstanceAbstractionNativeAot* method)
        {
            var owned = Unpack<OwnedDynamicMethod>(method->gcHandle);
            return owned.Owner.GetMonoMethodHeader(owned).Value;
        }
    }
}
