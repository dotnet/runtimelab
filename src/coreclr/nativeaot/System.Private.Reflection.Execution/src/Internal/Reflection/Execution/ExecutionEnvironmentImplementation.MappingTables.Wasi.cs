// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

namespace Internal.Reflection.Execution
{
    internal sealed partial class ExecutionEnvironmentImplementation
    {
        private static IntPtr ConvertStackTraceIpToFunctionPointerForNativeUnwind(IntPtr methodStartAddress)
        {
            Debug.Assert(methodStartAddress == 0);
            return 0;
        }

        private static void InitializeIpToFunctionPointerMapForNativeUnwind(ref FunctionPointersToOffsets _) { }
    }
}
