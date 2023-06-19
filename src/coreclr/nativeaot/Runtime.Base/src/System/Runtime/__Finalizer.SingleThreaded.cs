// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

//
// Managed portion of finalization implementation for a single-threaded enviroment.
//

namespace System.Runtime
{
    internal static class __Finalizer
    {
        [UnmanagedCallersOnly(EntryPoint = "RhpProcessFinalizersAndReturn", CallConvs = new Type[] { typeof(CallConvCdecl) })]
        private static unsafe void RhpProcessFinalizersAndReturn()
        {
            // Drain the queue of finalizable objects.
            while (true)
            {
                object target = InternalCalls.RhpGetNextFinalizableObject();
                if (target == null)
                    return;

                // Call the finalizer on the current target object. If the finalizer throws we'll fail
                // fast via normal Redhawk exception semantics (since we are in an RPI method).
                ((delegate*<object, void>)target.GetMethodTable()->FinalizerCode)(target);
            }
        }
    }
}
