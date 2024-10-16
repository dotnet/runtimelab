// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime;

using Internal.DeveloperExperience;
using Internal.Runtime.Augments;

namespace System.Diagnostics
{
    public partial class StackFrame
    {
        private const string UnknownStackFrameString = "<unknown>";

#pragma warning disable CA1822 // Member 'GetNativeIPAddress' does not access instance data and can be marked as static
        internal IntPtr GetNativeIPAddress()
#pragma warning restore CA1822
        {
            // Return "null" - same rationale as below.
            return 0;
        }

        private static int GetNativeOffsetImpl()
        {
            // We could in principle make this work on Browser with the native unwind scheme. However, it would involve
            // parsing the WASM binary, which is not easy to obtain, and in general, we don't want to keep it in memory,
            // and it would not be possible at all with precise virtual unwinding. So, we return OFFSET_UNKNOWN.
            return OFFSET_UNKNOWN;
        }

#pragma warning disable IDE0060 // Remove unused parameter 'includeFileInfo'
        private unsafe string CreateStackTraceString(bool includeFileInfo, out bool isStackTraceHidden)
#pragma warning restore IDE0060
        {
            if (RuntimeAugments.PreciseVirtualUnwind)
            {
                IntPtr ip = _ipAddress;
                string result = DeveloperExperience.GetMethodName(ip, out _, out isStackTraceHidden);
                if (result == null)
                {
                    // In the current setup, we hide frames without stack trace metadata on the unwinder level.
                    Debug.Fail("Unexpected null method name with precise virtual unwinding enabled");
                    return UnknownStackFrameString;
                }
                return result;
            }

            return CreateStackTraceStringForNativeUnwind(out isStackTraceHidden);
        }
    }
}
