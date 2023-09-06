// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable CA1822 // Member does not access instance data and can be marked as static
#pragma warning disable IDE0060 // Remove unused parameter

using System.Text;

namespace System.Diagnostics
{
    public partial class StackTrace
    {
        private unsafe void InitializeForCurrentThread(int skipFrames, bool needFileInfo)
        {
            // There is now way, currently, to get the stack trace in WASI.
        }

        internal string ToString(TraceFormat traceFormat)
        {
            return "";
        }

        internal void ToString(TraceFormat traceFormat, StringBuilder builder)
        {
        }
    }
}
