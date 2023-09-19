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


    static readonly MemoryManager globalMemoryManager = new MemoryManager();
    static readonly MintTypeSystem globalMintTypeSystem = new MintTypeSystem(globalMemoryManager);

    internal static MintTypeSystem GlobalMintTypeSystem => globalMintTypeSystem;
    internal static MemoryManager GlobalMemoryManager => globalMemoryManager;

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
        itf->mono_mint_type = &Abstraction.Itf.mintGetMintTypeFromMonoType;
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
        public IMintCompiledMethod GetFunctionPointer(DynamicMethod dm)
        {
            // FIXME: GetFunctionPointer is not the right method.
            // We probably want to return some kind of a CompiledDynamicMethodDelegate
            // object that can be invoked with the right calling convention.
            var execMemoryManager = new MemoryManager();
            using var compiler = new DynamicMethodCompiler(dm, execMemoryManager);
            return compiler.Compile();
        }
    }


    // just run the method assuming it takes no arguments and returns void or an int
    internal static object BigHackyExecCompiledMethod(MethodInfo dm, CompiledMethod compiledMethod, object[] args)
    {
        Internal.Console.Write($"Compiled method: {compiledMethod.InterpMethod.Value}{Environment.NewLine}");
        if (!TryGetKnownInvokeShape(dm, out var invokeShape))
        {
            throw new InvalidOperationException($"Can't invoke this kind of Delegate ({dm.GetType()} yet");
        }
        InvokeWithKnownShape(invokeShape, compiledMethod, args, out var result);
        switch (invokeShape)
        {
            case KnownInvokeShape.VoidVoid:
                return null;
            case KnownInvokeShape.IntReturn:
            case KnownInvokeShape.IntParamIntReturn:
            case KnownInvokeShape.IntDoubleParamsIntReturn:
                int resultVal;
                unsafe
                {
                    resultVal = *(int*)result;
                }
                return resultVal;
            default:
                throw new InvalidOperationException("Unknown invoke shape");
        }
    }

    internal enum KnownInvokeShape
    {
        VoidVoid,
        IntReturn,
        IntParamIntReturn,
        IntDoubleParamsIntReturn,
    }

    internal static bool TryGetKnownInvokeShape(MethodInfo dm, out KnownInvokeShape invokeShape)
    {
        invokeShape = default;
        if (dm.ReturnType == typeof(void))
        {
            if (dm.GetParameters().Length == 0)
            {
                invokeShape = KnownInvokeShape.VoidVoid;
                return true;
            }
        }
        else if (dm.ReturnType == typeof(int))
        {
            var parameters = dm.GetParameters();
            if (parameters.Length == 0)
            {
                invokeShape = KnownInvokeShape.IntReturn;
                return true;
            }
            else if (parameters.Length == 1)
            {
                if (parameters[0].ParameterType == typeof(int))
                {
                    invokeShape = KnownInvokeShape.IntParamIntReturn;
                    return true;
                }
            }
            else if (parameters.Length == 2)
            {
                if (parameters[0].ParameterType == typeof(int) &&
                    parameters[1].ParameterType == typeof(double))
                {
                    invokeShape = KnownInvokeShape.IntDoubleParamsIntReturn;
                    return true;
                }
            }
        }
        return false;
    }

    internal static void InvokeWithKnownShape(KnownInvokeShape invokeShape, CompiledMethod compiledMethod, object[] args, out IntPtr result)
    {
        switch (invokeShape)
        {
            case KnownInvokeShape.VoidVoid:
                {
                    result = IntPtr.Zero;
                    mint_testing_ee_interp_entry_static_0(compiledMethod.InterpMethod.Value);
                    break;
                }
            case KnownInvokeShape.IntReturn:
                {
                    result = compiledMethod.ExecMemoryManager.Allocate(8);
                    mint_testing_ee_interp_entry_static_ret_0(result, compiledMethod.InterpMethod.Value);
                    break;
                }
            case KnownInvokeShape.IntParamIntReturn:
                {
                    result = compiledMethod.ExecMemoryManager.Allocate(8);
                    int arg1 = (int)args[0];
                    unsafe
                    {
                        IntPtr arg1Ptr = (IntPtr)(void*)&arg1;
                        mint_testing_ee_interp_entry_static_ret_1(result, arg1Ptr, compiledMethod.InterpMethod.Value);
                    }
                    break;
                }
            case KnownInvokeShape.IntDoubleParamsIntReturn:
                {
                    result = compiledMethod.ExecMemoryManager.Allocate(8);
                    int arg1 = (int)args[0];
                    double arg2 = (double)args[1];
                    unsafe
                    {
                        IntPtr arg1Ptr = (IntPtr)(void*)&arg1;
                        IntPtr arg2Ptr = (IntPtr)(void*)&arg2;
                        mint_testing_ee_interp_entry_static_ret_2(result, arg1Ptr, arg2Ptr, compiledMethod.InterpMethod.Value);
                    }
                    break;
                }
            default:
                throw new InvalidOperationException("Unknown invoke shape");
        }
        Internal.Console.Write($"Compiled method returned{Environment.NewLine}");
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
