// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.CompilerServices;

using Internal.Runtime.Augments;

namespace Internal.StackTraceMetadata
{
    internal static partial class StackTraceMetadata
    {
        private static IntPtr ConvertFunctionPointerToStackTraceIpForNativeUnwind(IntPtr functionPointer)
        {
            return RuntimeAugments.GetBiasedWasmFunctionIndexForFunctionPointer(functionPointer);
        }

        private static unsafe int ReadMethodIpForNativeUnwind(byte* pCurrent, out void* pMethodIp)
        {
            pMethodIp = (void*)(uint)RuntimeAugments.GetBiasedWasmFunctionIndex(Unsafe.ReadUnaligned<int>(pCurrent));
            return sizeof(int);
        }
    }
}
