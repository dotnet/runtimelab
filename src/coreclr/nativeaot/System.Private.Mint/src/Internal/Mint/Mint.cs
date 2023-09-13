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

    [DllImport(RuntimeLibrary)]
    private static extern void mint_testing_transform_sample(IntPtr gcHandle);

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
            GCHandle gch = GCHandle.Alloc(dm);
            try
            {
                mint_testing_transform_sample(GCHandle.ToIntPtr(gch));
            }
            finally
            {
                gch.Free();
            }
            return IntPtr.Zero;
        }
    }
}
