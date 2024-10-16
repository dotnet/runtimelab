// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Internal.Reflection.Core.Execution;
using Internal.Runtime.Augments;

namespace Internal.Reflection.Execution
{
    internal sealed partial class ExecutionEnvironmentImplementation
    {
        public sealed override unsafe IntPtr ConvertStackTraceIpToFunctionPointer(IntPtr methodStartAddress)
        {
            if (RuntimeAugments.PreciseVirtualUnwind)
            {
                void* functionPointer;
                RuntimeAugments.ParsePreciseVirtualUnwindInfo((byte*)methodStartAddress, &functionPointer);
                return (IntPtr)functionPointer;
            }

            return ConvertStackTraceIpToFunctionPointerForNativeUnwind(methodStartAddress);
        }

        private static void InitializeIpToFunctionPointerMap(ref FunctionPointersToOffsets entry)
        {
            if (RuntimeAugments.PreciseVirtualUnwind)
            {
                return;
            }

            InitializeIpToFunctionPointerMapForNativeUnwind(ref entry);
        }
    }
}
