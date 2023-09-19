// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Reflection.Emit;
using Internal.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

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

    internal static MintTypeSystem GlobalMintTypeSystem => globalMintTypeSystem;
    internal static MemoryManager GlobalMemoryManager => globalMemoryManager;

    static Mint()
    {
        globalMemoryManager = new MemoryManager();
        globalMintTypeSystem = new MintTypeSystem(globalMemoryManager);
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

        itf->get_type_from_stack = &Abstraction.Itf.mintGetTypeFromStack;
        itf->get_default_byval_type_void = &mintGetDefaultByvalTypeVoid;
        itf->get_default_byval_type_int = &mintGetDefaultByvalTypeIntPtr;

        itf->imethod_alloc = &mintIMethodAlloc;
        // TODO: initialize members of itf with function pointers that implement the stuff that
        // the interpreter needs.  See mint-itf.c for the native placeholder implementation
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

}
