// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace System
{
    public partial class Exception
    {
        // Since we track stack traces as strings, we need an equivalent to the EdiSeparator marker.
        // When set, we should treat "throws" as rethrows that do not reset the stack trace. This is
        // an imperfect emulation of the real thing, as we don't "append" frames, but rather capture
        // the full trace upfront, still, it is better than nothing.
        private bool _dispatchStateRestored;

        // TODO-LLVM: unify with "AppendExceptionStackFrame"; this is a partial copy.
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static unsafe void InitializeExceptionStackFrameLLVM(object exception, int flags)
        {
            // This method is called by the runtime's EH dispatch code and is not allowed to leak exceptions
            // back into the dispatcher.
            try
            {
                Exception? ex = exception as Exception;
                if (ex == null)
                    Environment.FailFast("Exceptions must derive from the System.Exception class");

                if (!RuntimeExceptionHelpers.SafeToPerformRichExceptionSupport)
                    return;

                bool isFirstRethrowFrame = (flags & (int)RhEHFrameType.RH_EH_FIRST_RETHROW_FRAME) != 0;

                // track count for metrics
                if (!isFirstRethrowFrame)
                    Interlocked.Increment(ref s_exceptionCount);

                // When we're throwing an exception object, we reset its stacktrace with two exceptions:
                // 1. Don't clear if we're rethrowing with `throw;`.
                // 2. Don't clear if we're throwing through ExceptionDispatchInfo.
                //    This is done through invoking RestoreDispatchState which sets "_dispatchStateRestored" followed by throwing normally using `throw ex;`.
                bool doSetTheStackTrace = !isFirstRethrowFrame && !ex._dispatchStateRestored;

                // If out of memory, avoid any calls that may allocate.  Otherwise, they may fail
                // with another OutOfMemoryException, which may lead to infinite recursion.
                bool fatalOutOfMemory = ex == PreallocatedOutOfMemoryException.Instance;

                if (doSetTheStackTrace && !fatalOutOfMemory)
                    ex._stackTraceString = new StackTrace(1).ToString().Replace("__", ".").Replace("_", ".");

#if FEATURE_PERFTRACING
                string typeName = !fatalOutOfMemory ? ex.GetType().ToString() : "System.OutOfMemoryException";
                string message = !fatalOutOfMemory ? ex.Message : "Insufficient memory to continue the execution of the program.";

                fixed (char* exceptionTypeName = typeName, exceptionMessage = message)
                    Runtime.RuntimeImports.NativeRuntimeEventSource_LogExceptionThrown(exceptionTypeName, exceptionMessage, 0, ex.HResult);
#endif
            }
            catch
            {
                // We may end up with a confusing stack trace or a confusing ETW trace log, but at least we
                // can continue to dispatch this exception.
            }
        }

        //==================================================================================================================
        // Support for ExceptionDispatchInfo class - imports and exports the stack trace.
        //==================================================================================================================

        internal DispatchState CaptureDispatchState()
        {
            return new DispatchState(_stackTraceString);
        }

        internal void RestoreDispatchState(DispatchState DispatchState)
        {
            // Since EDI can be created at various points during exception dispatch (e.g. at various frames on the stack) for the same exception instance,
            // they can have different data to be restored. Thus, to ensure atomicity of restoration from each EDI, perform the restore under a lock.
            lock (s_DispatchStateLock)
            {
                _stackTraceString = DispatchState.StackTrace;
                _dispatchStateRestored = true;
            }
        }

        internal readonly struct DispatchState
        {
            public readonly string StackTrace;

            public DispatchState(string stackTrace)
            {
                StackTrace = stackTrace;
            }
        }
    }
}
