// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection.Emit;
using Internal.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Internal.Mint;

internal static class Mint
{
    const string RuntimeLibrary = "*";

    [DllImport(RuntimeLibrary)]
    private static extern unsafe void mint_entrypoint(Internal.Mint.Abstraction.Itf* nativeAotItf);

    [DllImport(RuntimeLibrary)]
    internal static extern unsafe IntPtr mint_testing_transform_sample(Internal.Mint.Abstraction.MonoMethodInstanceAbstractionNativeAot* monoMethodPtr);

    static readonly MemoryManager globalMemoryManager = new MemoryManager();

    internal static void Initialize()
    {
        AppContext.SetSwitch("System.Private.Mint.Enable", true);
        unsafe
        {
            var itf = CreateItf();
            mint_entrypoint(itf);
        }
        InstallDynamicMethodCallbacks();
    }

    internal static unsafe Internal.Mint.Abstraction.Itf* CreateItf()
    {
        Internal.Mint.Abstraction.Itf* itf = globalMemoryManager.Allocate<Internal.Mint.Abstraction.Itf>();
        itf->get_MonoType_inst = &Internal.Mint.Abstraction.Itf.unwrapTransparentAbstraction;
        itf->get_MonoMethod_inst = &Internal.Mint.Abstraction.Itf.unwrapTransparentAbstraction;
        itf->get_MonoMethodHeader_inst = &Internal.Mint.Abstraction.Itf.unwrapTransparentAbstraction;
        itf->get_MonoMethodSignature_inst = &Internal.Mint.Abstraction.Itf.unwrapTransparentAbstraction;
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
        public IntPtr GetFunctionPointer(DynamicMethod dm)
        {
            // FIXME: GetFunctionPointer is not the right method.
            // We probably want to return some kind of a CompiledDynamicMethodDelegate
            // object that can be invoked with the right calling convention.
            using var compiler = new DynamicMethodCompiler(dm);
            var compiledMethod = compiler.Compile();
            compiledMethod.ExecMemoryManager.Dispose();// FIXME: this is blatantly wrong
            return compiledMethod.InterpMethod.Value;
        }
    }
}
