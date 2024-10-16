// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

using Internal.Runtime.Augments;

namespace System
{
    public partial class Exception
    {
        private bool AppendIpForPreciseVirtualUnwind(IntPtr ip, bool isFirstFrame, bool isFirstRethrowFrame)
        {
            Debug.Assert(!isFirstRethrowFrame || isFirstFrame);
            if (RuntimeAugments.PreciseVirtualUnwind && (ip != 0))
            {
                AppendStackIP(ip, isFirstRethrowFrame);
                return true;
            }
            return false;
        }

        private StackFrame GetTargetSiteStackFrame()
        {
            if (RuntimeAugments.PreciseVirtualUnwind)
            {
                return new StackFrame(_corDbgStackTrace[0], needFileInfo: false);
            }
            return GetTargetSiteStackFrameViaNativeUnwind();
        }
    }
}
