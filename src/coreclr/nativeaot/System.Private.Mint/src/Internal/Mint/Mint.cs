// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Reflection.Emit;
using Internal.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Internal.Mint.Abstraction;

namespace Internal.Mint;

internal static class Mint
{
    const string RuntimeLibrary = "*";

    [DllImport(RuntimeLibrary)]
    private static extern unsafe void mint_entrypoint(Abstraction.Itf* nativeAotItf, Abstraction.EEItf* eeItf);

    [DllImport(RuntimeLibrary)]
    internal static extern unsafe IntPtr mint_testing_transform_sample(Internal.Mint.Abstraction.MonoMethodInstanceAbstractionNativeAot* monoMethodPtr);

    [DllImport(RuntimeLibrary)]
    internal static extern unsafe void mint_testing_ee_interp_entry_static_0(IntPtr interpMethodPtr);

    [DllImport(RuntimeLibrary)]
    internal static extern unsafe void mint_testing_ee_interp_entry_static_ret_0(IntPtr res, IntPtr interpMethodPtr);

    [DllImport(RuntimeLibrary)]
    internal static extern unsafe void mint_testing_ee_interp_entry_static_ret_1(IntPtr res, IntPtr arg1, IntPtr interpMethodPtr);

    [DllImport(RuntimeLibrary)]
    internal static extern unsafe void mint_testing_ee_interp_entry_static_ret_2(IntPtr res, IntPtr arg1, IntPtr arg2, IntPtr interpMethodPtr);


    static readonly MemoryManager globalMemoryManager;
    static readonly MintTypeSystem globalMintTypeSystem;
    static readonly MintVTableProvider s_mintVTableProvider;

    internal static MintTypeSystem GlobalMintTypeSystem => globalMintTypeSystem;
    internal static MemoryManager GlobalMemoryManager => globalMemoryManager;

    static Mint()
    {
        globalMemoryManager = new MemoryManager();
        globalMintTypeSystem = new MintTypeSystem(globalMemoryManager);
        s_mintVTableProvider = new MintVTableProvider();
    }

    internal static void Initialize()
    {
        AppContext.SetSwitch("System.Private.Mint.Enable", true);
        InitializeGlobalTypeSystem();
        unsafe
        {
            var itf = CreateItf();
            var eeItf = EE.MintRuntime.CreateItf();
            mint_entrypoint(itf, eeItf);
        }
        InstallDynamicMethodCallbacks();
    }

    internal static void InitializeGlobalTypeSystem()
    {
        globalMintTypeSystem.GetMonoType((RuntimeType)typeof(void));
        globalMintTypeSystem.GetMonoType((RuntimeType)typeof(sbyte));
        globalMintTypeSystem.GetMonoType((RuntimeType)typeof(byte));
        globalMintTypeSystem.GetMonoType((RuntimeType)typeof(char));
        globalMintTypeSystem.GetMonoType((RuntimeType)typeof(short));
        globalMintTypeSystem.GetMonoType((RuntimeType)typeof(ushort));
        globalMintTypeSystem.GetMonoType((RuntimeType)typeof(int));
        globalMintTypeSystem.GetMonoType((RuntimeType)typeof(uint));
        globalMintTypeSystem.GetMonoType((RuntimeType)typeof(IntPtr));
        globalMintTypeSystem.GetMonoType((RuntimeType)typeof(UIntPtr));
        globalMintTypeSystem.GetMonoType((RuntimeType)typeof(float));
        globalMintTypeSystem.GetMonoType((RuntimeType)typeof(double));
        globalMintTypeSystem.GetMonoType((RuntimeType)typeof(string));
    }

    internal static unsafe Abstraction.Itf* CreateItf()
    {
        Abstraction.Itf* itf = globalMemoryManager.Allocate<Abstraction.Itf>();
        itf->get_MonoType_inst = &Abstraction.Itf.unwrapTransparentAbstraction;
        itf->get_MonoMethod_inst = &Abstraction.Itf.unwrapTransparentAbstraction;
        itf->get_MonoMethodHeader_inst = &Abstraction.Itf.unwrapTransparentAbstraction;
        itf->get_MonoMethodSignature_inst = &Abstraction.Itf.unwrapTransparentAbstraction;
        itf->get_MonoMemPool_inst = &Abstraction.Itf.unwrapTransparentAbstraction;

        itf->get_type_from_stack = &Abstraction.Itf.mintGetTypeFromStack;
        itf->get_default_byval_type_void = &mintGetDefaultByvalTypeVoid;
        itf->get_default_byval_type_int = &mintGetDefaultByvalTypeIntPtr;

        itf->imethod_alloc = &mintIMethodAlloc;
        // TODO: initialize members of itf with function pointers that implement the stuff that
        // the interpreter needs.  See mint-itf.c for the native placeholder implementation

        itf->create_mem_pool = &CreateMemPool;
        itf->m_method_get_mem_manager = &MonoMethodGetMemManager;
        return itf;
    }

    internal static void InstallDynamicMethodCallbacks()
    {
        DynamicMethodAugments.InstallMintCallbacks(new Callbacks());
    }

    internal class Callbacks : IMintDynamicMethodCallbacks
    {
        public IMintCompiledMethod CompileDynamicMethod(DynamicMethod dm)
        {
            // FIXME: GetFunctionPointer is not the right method.
            // We probably want to return some kind of a CompiledDynamicMethodDelegate
            // object that can be invoked with the right calling convention.
            var execMemoryManager = new MemoryManager();
            using var compiler = new DynamicMethodCompiler(dm, execMemoryManager);
            return compiler.Compile();
        }
    }

    private sealed unsafe class MintVTableProvider : VTableProvider
    {
        public MintVTableProvider() : base() { }
        ~MintVTableProvider()
        {
            Dispose(false);
            GC.SuppressFinalize(this);
        }

        private MonoMemPoolInstanceAbstractionVTable* _monoMemPoolInstanceAbstractionVTable;
        internal MonoMemPoolInstanceAbstractionVTable* MonoMemPoolInstanceAbstractionVTable
        {
            get => GetOrAddVTable(ref _monoMemPoolInstanceAbstractionVTable, static (vtable) =>
            {
                vtable->destroy = &DestroyMemPool;
                vtable->alloc0 = &MemPoolAlloc0;
            });
        }
    }

    private static T Unpack<T>(IntPtr ptr) where T : class
    {
        return GCHandle.FromIntPtr(ptr).Target as T;
    }

    [UnmanagedCallersOnly]
    internal static unsafe Abstraction.MonoTypeInstanceAbstractionNativeAot* mintGetDefaultByvalTypeVoid() => GlobalMintTypeSystem.GetMonoType((RuntimeType)typeof(void)).Value;

    [UnmanagedCallersOnly]
    internal static unsafe Abstraction.MonoTypeInstanceAbstractionNativeAot* mintGetDefaultByvalTypeIntPtr() => GlobalMintTypeSystem.GetMonoType((RuntimeType)typeof(IntPtr)).Value;

#pragma warning disable IDE0060
    [UnmanagedCallersOnly]
    internal static unsafe IntPtr mintIMethodAlloc(IntPtr _interpMethod, UIntPtr size)
    {
        // FIXME: don't allocate from the global memory manager, get the memory manager from the interpMethod
        // see imethod_alloc0 in transform.c and interp.c
        return globalMemoryManager.Allocate(checked((uint)size));
    }
#pragma warning restore IDE0060

    internal static unsafe MonoMemPoolInstanceAbstraction* CreateMemPoolFor(MemoryManager memoryManager)
    {
        var ptr = memoryManager.Allocate<MonoMemPoolInstanceAbstraction>();
        ptr->vtable = s_mintVTableProvider.MonoMemPoolInstanceAbstractionVTable;
        ptr->gcHandle = GCHandle.ToIntPtr(memoryManager.Own(memoryManager));
        return ptr;
    }

    [UnmanagedCallersOnly]
    private static unsafe MonoMemPoolInstanceAbstraction* CreateMemPool()
    {
        return CreateMemPoolFor(new MemoryManager());
    }

    [UnmanagedCallersOnly]
    private static unsafe void DestroyMemPool(MonoMemPoolInstanceAbstraction* ptr)
    {
        var gcHandle = GCHandle.FromIntPtr(ptr->gcHandle);
        var memoryManager = (MemoryManager)gcHandle.Target;
        memoryManager.Dispose();
    }

    [UnmanagedCallersOnly]
    private static unsafe IntPtr MemPoolAlloc0(MonoMemPoolInstanceAbstraction* ptr, uint size)
    {
        var gcHandle = GCHandle.FromIntPtr(ptr->gcHandle);
        var memoryManager = (MemoryManager)gcHandle.Target;
        return memoryManager.Allocate(size);
    }

    [UnmanagedCallersOnly]
    internal static unsafe MonoMemManagerInstanceAbstraction* MonoMethodGetMemManager(MonoMethodInstanceAbstractionNativeAot* method)
    {
        // mint mem manager
        // - for dynamic methods, route to the memory manager of the dynamic method
        // - for SRE it would route to the memory manager of the AssemblyBuilder
        // - for general interpretation it would route to the memory manager of the containing assembly
        // - for generics it would be a mempool co-owned by all the assemblies that are mentioned in a generic type
        //   instance.  (ie: List<MyType> would be co-owned by CoreLib and MyAssembly)
        var owned = Unpack<IOwnedMethod>(method->gcHandle);
        return owned.GetOwnerTypeSystem().GetMemManagerAbstraction().Value;
    }
}
