// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

using Internal.Reflection.Core.Execution;
using Internal.Runtime.Augments;

namespace Internal.Reflection.Execution
{
    internal sealed partial class ExecutionEnvironmentImplementation
    {
        private IntPtr ConvertStackTraceIpToFunctionPointerForNativeUnwind(IntPtr methodStartAddress)
        {
            foreach ((_, FunctionPointersToOffsets entry) in GetLdFtnReverseLookups_InvokeMap())
            {
                if (entry.TryGetFunctionPointer(methodStartAddress, out nint functionPointer))
                {
                    return functionPointer;
                }
            }

            // Not in the InvokeMap, return null.
            return 0;
        }

        private static void InitializeIpToFunctionPointerMapForNativeUnwind(ref FunctionPointersToOffsets entry)
        {
            FunctionPointerOffsetPair[] data = entry.Data;
            var stackTraceIpMap = new RuntimeAugments.StackTraceIpAndFunctionPointer[data.Length];
            for (int i = 0; i < data.Length; i++)
            {
                stackTraceIpMap[i].FunctionPointer = data[i].FunctionPointer;
            }

            RuntimeAugments.InitializeStackTraceIpMap(stackTraceIpMap);
            Array.Sort(stackTraceIpMap);
            entry.StackTraceIpMap = stackTraceIpMap;
        }

        private partial struct FunctionPointersToOffsets
        {
            public bool TryGetFunctionPointer(IntPtr methodStartAddress, out IntPtr functionPointer)
            {
                if (StackTraceIpMap != null)
                {
                    var item = new RuntimeAugments.StackTraceIpAndFunctionPointer() { StackTraceIp = methodStartAddress };
                    int index = Array.BinarySearch(StackTraceIpMap, item);
                    if (index >= 0)
                    {
                        functionPointer = StackTraceIpMap[index].FunctionPointer;
                        Debug.Assert(functionPointer != 0);
                        return true;
                    }
                }

                functionPointer = 0;
                return false;
            }
        }
    }
}
