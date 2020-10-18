// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.CompilerServices;

namespace System.Diagnostics
{
    public static class Eval
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        [RuntimeExport("RhpDebugFuncEvalHelper")]
        public static unsafe void RhpDebugFuncEvalHelper(IntPtr unusedTransitionBlock, IntPtr classlibAddress)
        {
            IntPtr pDebugFuncEvalHelper = (IntPtr)InternalCalls.RhpGetClasslibFunctionFromCodeAddress(classlibAddress, ClassLibFunctionId.DebugFuncEvalHelper);
            Debug.Assert(pDebugFuncEvalHelper != IntPtr.Zero);
            ((delegate*<void>)pDebugFuncEvalHelper)();
        }
    }
}
