// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Internal.StackTraceMetadata
{
    internal static partial class StackTraceMetadata
    {
        private static IntPtr ConvertFunctionPointerToStackTraceIp(IntPtr functionPointer)
        {
            return functionPointer;
        }

        private static unsafe int ReadMethodIp(byte* pCurrent, out void* pMethodIp)
        {
            pMethodIp = pCurrent + *(int*)pCurrent;
            return sizeof(int);
        }
    }
}
