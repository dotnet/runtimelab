// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Internal.Reflection.Emit;

using CorElementType = System.Reflection.CorElementType;

#if FEATURE_MINT
public static partial class DynamicMethodAugments {
    public static void InstallMintCallbacks(IMintDynamicMethodCallbacks callbacks) {
        s_mintCallbacks = callbacks;
    }

    internal static IMintDynamicMethodCallbacks MintCallbacks => s_mintCallbacks;

    private static IMintDynamicMethodCallbacks s_mintCallbacks;

    public static byte GetCorElementType(this Type type) {
        // see ReflectionAugments.GetRuntimeTypeCode

        EETypePtr eeType;
        if (!type.TryGetEEType(out eeType))
        {
            // Type exists in metadata only. Aside from the enums, there is no chance a type with a TypeCode would not have an MethodTable,
            // so if it's not an enum, return the default.
            if (!type.IsEnum || type.IsGenericParameter)
                return (byte)CorElementType.ELEMENT_TYPE_OBJECT;
            Type underlyingType = Enum.GetUnderlyingType(type);
            eeType = underlyingType.TypeHandle.ToEETypePtr();
        }
        return (byte)eeType.CorElementType;
    }
}

#endif
