// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#pragma warning disable CA1822 // Member does not access instance data and can be marked as static
#pragma warning disable IDE0060 // Remove unused parameter

namespace System
{
    public partial class Exception
    {
        private void AppendStack(IntPtr ip, bool isFirstFrame, bool isFirstRethrowFrame)
        {
            // TODO-LLVM: implement managed stack traces on WASI.
        }
    }
}
