// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace System
{
    public partial class Exception
    {
        private void AppendStack(IntPtr ip, bool isFirstFrame, bool isFirstRethrowFrame)
        {
            if (AppendIpForPreciseVirtualUnwind(ip, isFirstFrame, isFirstRethrowFrame))
            {
                return;
            }

            // No native virtual unwind mechanism on WASI.
            Debug.Assert(ip == 0);
        }

#pragma warning disable CA1822 // Member does not access instance data and can be marked as static
        private StackFrame GetTargetSiteStackFrameViaNativeUnwind() => new StackFrame(0, needFileInfo: false);
#pragma warning restore CA1822 // Member does not access instance data and can be marked as static
    }
}
