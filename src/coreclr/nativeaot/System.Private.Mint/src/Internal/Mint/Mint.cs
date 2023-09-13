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
    private static extern void mint_entrypoint();

    internal static void Initialize()
    {
        AppContext.SetSwitch("System.Private.Mint.Enable", true);
        InstallDynamicMethodCallbacks();
        mint_entrypoint();
    }

    internal static void InstallDynamicMethodCallbacks()
    {
        DynamicMethodAugments.InstallMintCallbacks(new Callbacks());
    }

    internal class Callbacks : IMintDynamicMethodCallbacks
    {
        public IntPtr GetFunctionPointer(DynamicMethod dm)
        {
            throw new NotImplementedException("Mint.GetFunctionPointer  not implemented");
        }
    }
}
