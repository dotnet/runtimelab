// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Threading.Tasks
{
    public partial class Task
    {
        private struct SuspendedThread
        {}

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "GreenThread_StartThread")]
        private static unsafe partial SuspendedThread* GreenThread_StartThread(delegate* unmanaged<void*, void> startFrame, void* parameter);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "GreenThread_ResumeThread")]
        private static unsafe partial SuspendedThread* GreenThread_ResumeThread(SuspendedThread* suspendedThread);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "GreenThread_Yield")]
        [return: MarshalAs(UnmanagedType.I1)]
        private static unsafe partial bool GreenThread_Yield();

        [UnmanagedCallersOnly]
        private static unsafe void GreenThreadStartFunc(void* argument)
        {
            Action action = Unsafe.AsRef<Action>(argument);
            action();
        }

        private static class GreenThreadStatics
        {
            [ThreadStatic]
            public static Task? t_TaskToWaitFor;
        }

        internal static unsafe void GreenThreadExecutorFunc(object? obj)
        {
            Thread.t_IsGreenThread = true;
            try
            {
                var action = (Action?)obj;
                Thread.t_currentThread = null; // TODO Once we have Thread statics working we probably don't need to do this.
                var suspendedThread = GreenThread_StartThread(&GreenThreadStartFunc, Unsafe.AsPointer(ref action));
                Debug.Assert((GreenThreadStatics.t_TaskToWaitFor != null) == (suspendedThread != null));
                if (suspendedThread != null)
                {
                    Debug.Assert(GreenThreadStatics.t_TaskToWaitFor != null);
                    var suspendedThreadObj = new ResumeSuspendedThread(suspendedThread);
                    GreenThreadStatics.t_TaskToWaitFor.GetAwaiter().OnCompleted(suspendedThreadObj.Resume);
                }
            }
            finally
            {
                GreenThreadStatics.t_TaskToWaitFor = null;
                Thread.t_IsGreenThread = false;
                Thread.t_currentThread = null; // TODO Once we have Thread statics working we probably don't need to do this.
            }
        }

        private sealed unsafe class ResumeSuspendedThread
        {
            private SuspendedThread* _suspendedThread;
            public ResumeSuspendedThread(SuspendedThread* suspendedThread)
            {
                _suspendedThread = suspendedThread;
            }

            public void Resume()
            {
                Thread.t_IsGreenThread = true;
                try
                {
                    Thread.t_currentThread = null; // TODO Once we have Thread statics working we probably don't need to do this.
                    _suspendedThread = GreenThread_ResumeThread(_suspendedThread);
                    Debug.Assert((GreenThreadStatics.t_TaskToWaitFor != null) == (_suspendedThread != null));
                    if (_suspendedThread != null)
                    {
                        Debug.Assert(GreenThreadStatics.t_TaskToWaitFor != null);
                        GreenThreadStatics.t_TaskToWaitFor.GetAwaiter().OnCompleted(Resume);
                    }
                }
                finally
                {
                    GreenThreadStatics.t_TaskToWaitFor = null;
                    Thread.t_IsGreenThread = false;
                    Thread.t_currentThread = null; // TODO Once we have Thread statics working we probably don't need to do this.
                }
            }
        }

        static partial void RunOnActualGreenThread(Action action, ref bool ranAsActualGreenThread)
        {
            ranAsActualGreenThread = true;
            ThreadPool.QueueUserWorkItem(GreenThreadExecutorFunc, action);
        }

        static partial void YieldGreenThread(Task taskToWaitForCompletion, ref bool yielded)
        {
            if (Thread.t_IsGreenThread)
            {
                try
                {
                    GreenThreadStatics.t_TaskToWaitFor = taskToWaitForCompletion;
                    yielded = GreenThread_Yield();
                }
                finally
                {
                    GreenThreadStatics.t_TaskToWaitFor = null;
                }
            }
        }
    }
}
