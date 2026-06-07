// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Diagnostics
{
    public partial class StackFrame
    {
        private unsafe void BuildStackFrameViaNativeUnwind(int _, bool needFileInfo)
        {
            InitializeForIpAddress(IntPtr.Zero, needFileInfo);
        }
    }
}
