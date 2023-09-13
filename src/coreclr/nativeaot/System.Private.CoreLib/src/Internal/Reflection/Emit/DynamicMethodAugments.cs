// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Internal.Reflection.Emit;

#if FEATURE_MINT
public static partial class DynamicMethodAugments {
    public static void InstallMintCallbacks(IMintDynamicMethodCallbacks callbacks) {
        s_mintCallbacks = callbacks;
    }

    internal static IMintDynamicMethodCallbacks MintCallbacks => s_mintCallbacks;

    private static IMintDynamicMethodCallbacks s_mintCallbacks;
}

#endif
