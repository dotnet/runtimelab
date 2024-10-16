// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Internal.StackTraceMetadata
{
    internal static partial class StackTraceMetadata
    {
        private static IntPtr ConvertFunctionPointerToStackTraceIpForNativeUnwind(IntPtr _)
        {
            return 0;
        }

        private static unsafe int ReadMethodIpForNativeUnwind(byte* _, out void* pMethodIp)
        {
            pMethodIp = null;
            return 0;
        }
    }
}
