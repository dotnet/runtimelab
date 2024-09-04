// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable CA1822 // Member does not access instance data and can be marked as static
#pragma warning disable IDE0060 // Remove unused parameter

using System;

namespace System.Diagnostics
{
    public partial class StackTrace
    {
        private void InitializeForCurrentThread(int skipFrames, bool needFileInfo)
        {
            InitializeForIpAddressArray(null, 0, 0, needFileInfo);
        }

        private void InitializeForException(Exception exception, int skipFrames, bool needFileInfo)
        {
            InitializeForIpAddressArray(null, 0, 0, needFileInfo);
        }

        private void InitializeForIpAddressArray(IntPtr[] ipAddresses, int skipFrames, int endFrameIndex, bool needFileInfo)
        {
            _numOfFrames = 0;
            _methodsToSkip = 0;
        }
    }
}
