// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

//
// Managed portion of finalization implementation for a single-threaded enviroment.
//

namespace System.Runtime
{
    internal static class __Finalizer
    {
        [UnmanagedCallersOnly(EntryPoint = "RhpProcessFinalizersAndReturn")]
        private static unsafe void RhpProcessFinalizersAndReturn()
        {
            // Drain the queue of finalizable objects.
            while (true)
            {
                object target = InternalCalls.RhpGetNextFinalizableObject();
                if (target == null)
                    return;

                try
                {
                    // Call the finalizer on the current target object.
                    ((delegate*<object, void>)target.GetMethodTable()->FinalizerCode)(target);
                }
                catch (Exception ex) when (ExceptionHandling.IsHandledByGlobalHandler(ex))
                {
                    // the handler returned "true" means the exception is now "handled" and we should continue.
                }
            }
        }
    }
}
