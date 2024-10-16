// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Internal.Reflection.Execution
{
    internal sealed partial class ExecutionEnvironmentImplementation
    {
        public sealed override IntPtr ConvertStackTraceIpToFunctionPointer(IntPtr methodStartAddress)
        {
            return methodStartAddress;
        }

        private static void InitializeIpToFunctionPointerMap(ref FunctionPointersToOffsets _) { }
    }
}
