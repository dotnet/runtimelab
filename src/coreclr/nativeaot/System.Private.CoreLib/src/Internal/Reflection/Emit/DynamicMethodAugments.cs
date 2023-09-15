// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Internal.Reflection.Emit;

using CorElementType = System.Reflection.CorElementType;

#if FEATURE_MINT
public static partial class DynamicMethodAugments {
    public static void InstallMintCallbacks(IMintDynamicMethodCallbacks callbacks) {
        s_mintCallbacks = callbacks;
    }

    public static ReadOnlySpan<byte> GetIL(this DynamicMethod dynamicMethod) => dynamicMethod.GetILBytes();

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

    /// THIS IS A VERY BAD THING
    public static GCHandle UnsafeGCHandleAlloc(object o, GCHandleType type)
    {
        if (type != GCHandleType.Pinned)
            return GCHandle.Alloc(o, type);
        else
        {
            // FIXME: I'm doing a very very bad thing
            IntPtr handle = GCHandle.InternalAlloc(o, type);
            handle = (IntPtr)((nint)handle | 1); // see GCHandle.Alloc in src/libraries
            return GCHandle.FromIntPtr(handle);
        }
    }

}

#endif
