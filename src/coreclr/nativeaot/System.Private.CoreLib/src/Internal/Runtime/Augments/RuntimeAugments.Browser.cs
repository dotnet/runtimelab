// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime;

namespace Internal.Runtime.Augments
{
    public partial class RuntimeAugments
    {
        public static int GetBiasedWasmFunctionIndex(int wasmFunctionIndex)
        {
            return Exception.GetBiasedWasmFunctionIndex(wasmFunctionIndex);
        }

        public static unsafe void InitializeStackTraceIpMap(StackTraceIpAndFunctionPointer[] stackTraceIpMap)
        {
            fixed (void* pEntries = stackTraceIpMap)
                RuntimeImports.RhpInitializeStackTraceIpMap((nint)pEntries, stackTraceIpMap.Length);
        }

        public struct StackTraceIpAndFunctionPointer : IComparable<StackTraceIpAndFunctionPointer>
        {
            public IntPtr FunctionPointer; // Indirect table index.
            public IntPtr StackTraceIp; // Biased WASM function index.

            public readonly int CompareTo(StackTraceIpAndFunctionPointer other) => StackTraceIp.CompareTo(other.StackTraceIp);
        }
    }
}
