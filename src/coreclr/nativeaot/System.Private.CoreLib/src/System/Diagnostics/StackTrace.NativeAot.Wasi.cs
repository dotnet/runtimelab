// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable IDE0060 // Remove unused parameter

using System;

namespace System.Diagnostics
{
    public partial class StackTrace
    {
        private void InitializeForCurrentThreadViaNativeUnwind(int skipSystemFrames, int skipFrames, bool needFileInfo)
        {
            InitializeForIpAddressArrayViaNativeUnwind(null, 0, needFileInfo);
        }

        private void InitializeForIpAddressArrayViaNativeUnwind(IntPtr[] ipAddresses, int skipFrames, bool needFileInfo)
        {
            _numOfFrames = 0;
            _methodsToSkip = 0;
        }
    }
}
