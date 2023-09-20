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

public sealed class MintDynamicMethodTypeSystem : MintTypeSystem
{
    private static DynamicMethodVTableProvider s_VTableProvider = new();
    private readonly Dictionary<MethodBase, MonoMethodPtr> _methods = new();
    private readonly Dictionary<MethodBase, MonoMethodSignaturePtr> _signatures = new();
    private readonly Dictionary<MethodBase, MonoMethodHeaderPtr> _headers = new();
    private readonly Dictionary<DynamicMethod, OwnedIL> _ilBytes = new();

    internal MintDynamicMethodTypeSystem(MemoryManager memoryManager, MintTypeSystem parent) : base(memoryManager, parent)
    {

    }

    internal MonoMethodPtr GetMonoMethodPointer(DynamicMethod dynamicMethod)
    {
        lock (Lock)
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
        lock (Lock)
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
        lock (Lock)
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
        lock (Lock)
        {
            if (_ilBytes.TryGetValue(dynamicMethod.DynamicMethod, out var ilBytes))
                return ilBytes;
            ilBytes = OwnedIL.CreateCopy(dynamicMethod.DynamicMethod.GetIL(), Allocator);
            _ilBytes.Add(dynamicMethod.DynamicMethod, ilBytes);
            return ilBytes;
        }
    }

    private unsafe MonoMethodPtr CreateMonoMethodPointerDynMethodImpl(DynamicMethod dynamicMethod)
    {
        var vtable = s_VTableProvider.MonoMethodInstanceAbstractionVTable;
        var s = new MonoMethodInstanceAbstractionNativeAot
        {
            vtable = vtable,
            name = (byte*)Allocator.AllocateString(dynamicMethod.Name),
            klass = IntPtr.Zero, // TODO
            is_dynamic = (byte)1,
            gcHandle = GCHandle.ToIntPtr(Allocator.Own(new OwnedDynamicMethod(dynamicMethod, this)))
        };
        var ptr = Allocator.Allocate<MonoMethodInstanceAbstractionNativeAot>();
        *ptr = s;
        return new MonoMethodPtr(ptr);
    }

    private unsafe MonoTypeInstanceAbstractionNativeAot** CreateParameterTypes(ParameterInfo[] methodParameterInfos)
    {
        if (methodParameterInfos.Length > 0)
        {
            int i = 0;
            var methodParamTypes = Allocator.AllocateArray<MonoTypeInstanceAbstractionNativeAot>(methodParameterInfos.Length);
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
            gcHandle = GCHandle.ToIntPtr(Allocator.Own(dynamicMethod)),
            MethodParamsTypes = CreateParameterTypes(methodParameterInfos),
        };
        var ptr = Allocator.Allocate<MonoMethodSignatureInstanceAbstractionNativeAot>();
        *ptr = s;

        return new MonoMethodSignaturePtr(ptr);
    }

    private unsafe MonoMethodHeaderPtr CreateMonoMethodHeaderDynMethodImpl(OwnedDynamicMethod ownedDynamicMethod)
    {
        var s = new MonoMethodHeaderInstanceAbstractionNativeAot
        {
            code_size = ownedDynamicMethod.DynamicMethod.GetIL().Length,
            max_stack = 0, // TODO
            num_locals = 0, // TODO
            num_clauses = 0, // TODO
            init_locals = 0, // TODO
            get_local_sig = IntPtr.Zero, // TODO
            get_code = &VTables.methodHeaderGetCodeImpl, // TODO
            get_ip_offset = &VTables.methodHeaderGetIPOffset, // TODO
            gcHandle = GCHandle.ToIntPtr(Allocator.Own(ownedDynamicMethod))
        };
        var ptr = Allocator.Allocate<MonoMethodHeaderInstanceAbstractionNativeAot>();
        *ptr = s;
        return new MonoMethodHeaderPtr(ptr);
    }

    private sealed class OwnedDynamicMethod : IOwnedMethod
    {
        public DynamicMethod DynamicMethod { get; }
        public MintDynamicMethodTypeSystem Owner { get; }
        public OwnedDynamicMethod(DynamicMethod dynamicMethod, MintDynamicMethodTypeSystem owner)
        {
            DynamicMethod = dynamicMethod;
            Owner = owner;
        }
        public MintTypeSystem GetOwnerTypeSystem() => Owner;
    };

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

    private unsafe class DynamicMethodVTableProvider : VTableProvider, IDisposable
    {
        public DynamicMethodVTableProvider() : base() { }
        ~DynamicMethodVTableProvider()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }
        private MonoMethodInstanceAbstractionVTable* _monoMethodInstanceAbstractionVTable;
        internal MonoMethodInstanceAbstractionVTable* MonoMethodInstanceAbstractionVTable
        {
            get
            {
                return GetOrAddVTable(ref _monoMethodInstanceAbstractionVTable, static (vtable) =>
                {
                    vtable->get_signature = &VTables.methodGetSignatureImpl;
                    vtable->get_header = &VTables.methodGetHeaderImpl;
                });
            }
        }



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
