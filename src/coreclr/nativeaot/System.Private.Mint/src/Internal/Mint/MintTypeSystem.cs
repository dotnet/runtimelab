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
    private readonly Dictionary<RuntimeType, MonoTypePtr> _types = new();
    private readonly Dictionary<MethodBase, MonoMethodPtr> _methods = new();
    private readonly Dictionary<MethodBase, MonoMethodSignaturePtr> _signatures = new();
    private readonly Dictionary<MethodBase, MonoMethodHeaderPtr> _headers = new();
    private readonly Dictionary<DynamicMethod, OwnedIL> _ilBytes = new();

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

    internal MonoTypePtr GetMonoType(RuntimeType runtimeType)
    {
        if (_parent != null && _parent._types.TryGetValue(runtimeType, out var type))
            return type;
        lock (_lock)
        {
            if (_types.TryGetValue(runtimeType, out type))
                return type;
            type = CreateMonoTypeRuntimeTypeImpl(runtimeType);
            _types.Add(runtimeType, type);
            return type;
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

    private OwnedIL GetILBytes(OwnedDynamicMethod dynamicMethod)
    {
        lock (_lock)
        {
            if (_ilBytes.TryGetValue(dynamicMethod.DynamicMethod, out var ilBytes))
                return ilBytes;
            ilBytes = OwnedIL.CreateCopy(dynamicMethod.DynamicMethod.GetIL(), _memoryMananger);
            _ilBytes.Add(dynamicMethod.DynamicMethod, ilBytes);
            return ilBytes;
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

    private unsafe Internal.Mint.Abstraction.MonoTypeInstanceAbstractionNativeAot** CreateParameterTypes(ParameterInfo[] methodParameterInfos)
    {
        if (methodParameterInfos.Length > 0)
        {
            int i = 0;
            var methodParamTypes = _memoryMananger.AllocateArray<Internal.Mint.Abstraction.MonoTypeInstanceAbstractionNativeAot>(methodParameterInfos.Length);
            foreach (var methodParamInfo in methodParameterInfos)
            {
                methodParamTypes[i] = GetMonoType(methodParamInfo.ParameterType as RuntimeType).Value;
                i++;
            }
            return methodParamTypes;
        }
        else
        {
            return null;
        }
    }

    private unsafe MonoMethodSignaturePtr CreateMonoMethodSignatureDynMethodImpl(OwnedDynamicMethod dynamicMethod)
    {
        // N.B: the signature doesn't have the same lifetime as the MonoMethodPtr
        // as Mono can allocate and free signatures independently of the method
        ParameterInfo[] methodParameterInfos = dynamicMethod.DynamicMethod.GetParameters();
        var s = new Internal.Mint.Abstraction.MonoMethodSignatureInstanceAbstractionNativeAot
        {
            param_count = methodParameterInfos.Length,
            hasthis = (byte)0, //FIXME: this doesn't work (returns 1): dynamicMethod.DynamicMethod.IsStatic ? (byte)0 : (byte)1,
            method_params = &VTables.monoMethodSignatureGetParameterTypesUnderlyingTypeImpl,
            ret_ult = &VTables.monoMethodSignatureGetReturnTypeUnderlyingTypeImpl,
            gcHandle = GCHandle.ToIntPtr(_memoryMananger.Own(dynamicMethod)),
            MethodParamsTypes = CreateParameterTypes(methodParameterInfos),
        };
        var ptr = _memoryMananger.Allocate<Internal.Mint.Abstraction.MonoMethodSignatureInstanceAbstractionNativeAot>();
        *ptr = s;

        return new MonoMethodSignaturePtr(ptr);
    }

    private unsafe MonoMethodHeaderPtr CreateMonoMethodHeaderDynMethodImpl(OwnedDynamicMethod ownedDynamicMethod)
    {
        var s = new Internal.Mint.Abstraction.MonoMethodHeaderInstanceAbstractionNativeAot
        {
            code_size = ownedDynamicMethod.DynamicMethod.GetIL().Length,
            max_stack = 0, // TODO
            num_locals = 0, // TODO
            num_clauses = 0, // TODO
            init_locals = 0, // TODO
            get_local_sig = IntPtr.Zero, // TODO
            get_code = &VTables.methodHeaderGetCodeImpl, // TODO
            get_ip_offset = &VTables.methodHeaderGetIPOffset, // TODO
            gcHandle = GCHandle.ToIntPtr(_memoryMananger.Own(ownedDynamicMethod))
        };
        var ptr = _memoryMananger.Allocate<Internal.Mint.Abstraction.MonoMethodHeaderInstanceAbstractionNativeAot>();
        *ptr = s;
        return new MonoMethodHeaderPtr(ptr);
    }

    private unsafe MonoTypePtr CreateMonoTypeRuntimeTypeImpl(RuntimeType runtimeType)
    {
        var s = new Internal.Mint.Abstraction.MonoTypeInstanceAbstractionNativeAot
        {
            type_code = (int)runtimeType.GetCorElementType(),
            gcHandle = GCHandle.ToIntPtr(_memoryMananger.Own(runtimeType))
        };
        var ptr = _memoryMananger.Allocate<Internal.Mint.Abstraction.MonoTypeInstanceAbstractionNativeAot>();
        *ptr = s;
        return new MonoTypePtr(ptr);
    }

    private sealed record OwnedDynamicMethod(DynamicMethod DynamicMethod, MintTypeSystem Owner);

    private sealed class OwnedIL
    {
        private unsafe OwnedIL(byte* pinnedILBytes, uint length, MemoryManager memoryManager)
        {
            PinnedILBytes = pinnedILBytes;
            Length = length;
            MemoryManager = memoryManager;
        }

        public static unsafe OwnedIL CreateCopy(ReadOnlySpan<byte> bytes, MemoryManager manager)
        {
            uint length = (uint)bytes.Length;
            var pinnedILBytes = (byte*)manager.Allocate(length);
            bytes.CopyTo(new Span<byte>(pinnedILBytes, bytes.Length));
            return new OwnedIL(pinnedILBytes, length, manager);
        }
        public unsafe byte* PinnedILBytes { get; }
        public uint Length { get; }
        public MemoryManager MemoryManager { get; }
    }
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

        [UnmanagedCallersOnly]
        public static unsafe byte* methodHeaderGetCodeImpl(MonoMethodHeaderInstanceAbstractionNativeAot* header)
        {
            var owned = Unpack<OwnedDynamicMethod>(header->gcHandle);
            return owned.Owner.GetILBytes(owned).PinnedILBytes;
        }

        [UnmanagedCallersOnly]
        public static unsafe int methodHeaderGetIPOffset(MonoMethodHeaderInstanceAbstractionNativeAot* header, byte* ip)
        {
            var owned = Unpack<OwnedDynamicMethod>(header->gcHandle);
            var ilBytes = owned.Owner.GetILBytes(owned);
            var offset = (int)(ip - ilBytes.PinnedILBytes);
            return offset;
        }

        [UnmanagedCallersOnly]
        public static unsafe MonoTypeInstanceAbstractionNativeAot* monoMethodSignatureGetReturnTypeUnderlyingTypeImpl(MonoMethodSignatureInstanceAbstractionNativeAot* signature)
        {
            var owned = Unpack<OwnedDynamicMethod>(signature->gcHandle);
            var returnType = owned.DynamicMethod.ReturnType;
            if (returnType.IsEnum)
                returnType = Enum.GetUnderlyingType(returnType);
            var type = owned.Owner.GetMonoType((RuntimeType)returnType);
            return type.Value;
        }

        [UnmanagedCallersOnly]
        public static unsafe MonoTypeInstanceAbstractionNativeAot** monoMethodSignatureGetParameterTypesUnderlyingTypeImpl(MonoMethodSignatureInstanceAbstractionNativeAot* signature)
        {
            var owned = Unpack<OwnedDynamicMethod>(signature->gcHandle);
            var monoMethodSignaturePtr = owned.Owner.GetMonoMethodSignature(owned);
            return monoMethodSignaturePtr.Value->MethodParamsTypes;
        }
    }
}
