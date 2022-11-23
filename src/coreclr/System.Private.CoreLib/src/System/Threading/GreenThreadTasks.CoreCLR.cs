// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Threading.Tasks
{
    public partial class Task
    {
        private static readonly bool DontReuseGreenThreads =
            Environment.GetEnvironmentVariable("DOTNET_GreenThreads_DontReuse") == "1";

        private struct SuspendedThread
        {}

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "GreenThread_StartThread")]
        private static unsafe partial SuspendedThread* GreenThread_StartThread(delegate* unmanaged<void*, void> startFrame, void* parameter);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "GreenThread_ResumeThread")]
        private static unsafe partial SuspendedThread* GreenThread_ResumeThread(SuspendedThread* suspendedThread, void* yieldReturnValue);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "GreenThread_Yield")]
        private static unsafe partial void* GreenThread_Yield();

        [UnmanagedCallersOnly]
        private static unsafe void GreenThreadStartFunc(void* argument)
        {
            Thread.t_IsGreenThread = true;
            GreenThreadExecutorObject executorObj = Unsafe.AsRef<GreenThreadExecutorObject>(argument);
            GreenThreadStatics.t_RefToTaskToWaitFor = executorObj.refToTaskToWaitFor;

            ThreadPoolWorkQueueThreadLocals? tl = DontReuseGreenThreads ? null : ThreadPoolWorkQueueThreadLocals.Current;
            executorObj.action!();
            ExecutionContext.SendValueChangeNotificationsForResetToDefaultUnsafe();
            if (tl == null)
            {
                return;
            }

            while (true)
            {
                var resumeTcs = new TaskCompletionSource();
                tl.RegisterGreenThreadForResume(resumeTcs);
                resumeTcs.Task.Wait();
                tl.ResumeGreenThreadAction();
                ExecutionContext.SendValueChangeNotificationsForResetToDefaultUnsafe();
            }
        }

        private static class GreenThreadStatics
        {
            [ThreadStatic]
            public static unsafe void* t_RefToTaskToWaitFor;
        }

        private sealed class GreenThreadExecutorObject
        {
            public Action? action;
            public unsafe void* refToTaskToWaitFor;
        }

        internal static unsafe void GreenThreadExecutorFunc(Action action)
        {
            Debug.Assert(!Thread.IsGreenThread);
            Debug.Assert(Thread.CurrentThread._executionContext == null);

            GreenThreadExecutorObject executorObj = new GreenThreadExecutorObject();
            Task? taskToWaitFor = null;

            executorObj.action = action;
            executorObj.refToTaskToWaitFor = Unsafe.AsPointer(ref taskToWaitFor);
            var suspendedThread = GreenThread_StartThread(&GreenThreadStartFunc, Unsafe.AsPointer(ref executorObj));
            if (suspendedThread != null)
            {
                Debug.Assert(taskToWaitFor != null);
                var suspendedThreadObj = new ResumeSuspendedThread(suspendedThread);
                taskToWaitFor.GetAwaiter().OnCompleted(suspendedThreadObj.Resume);
            }
        }

        private sealed unsafe class ResumeSuspendedThread : IThreadPoolWorkItem
        {
            private SuspendedThread* _suspendedThread;
            public ResumeSuspendedThread(SuspendedThread* suspendedThread)
            {
                _suspendedThread = suspendedThread;
            }

            public void Resume()
            {
                if (Thread.IsGreenThread)
                {
                    // Currently cannot resume a green thread from another green thread
                    ThreadPool.UnsafeQueueUserWorkItem(this, preferLocal: true);
                }
                else
                {
                    ResumeFromNonGreenThread();
                }
            }

            private void ResumeFromNonGreenThread()
            {
                Debug.Assert(!Thread.IsGreenThread);

                Task? taskToWaitFor = null;

                ExecutionContext.SendValueChangeNotificationsForResetToDefaultUnsafe();
                _suspendedThread = GreenThread_ResumeThread(_suspendedThread, Unsafe.AsPointer(ref taskToWaitFor));
                ExecutionContext.SendValueChangeNotificationsForRestoreFromDefaultUnsafe();
                Debug.Assert((taskToWaitFor != null) == (_suspendedThread != null));
                if (_suspendedThread != null)
                {
                    Debug.Assert(taskToWaitFor != null);
                    taskToWaitFor.GetAwaiter().OnCompleted(Resume);
                }
            }

            void IThreadPoolWorkItem.Execute() => ResumeFromNonGreenThread();
        }

        private static readonly Action<Action> s_runAsGreenThreadInlineDelegate =
            DontReuseGreenThreads ? GreenThreadExecutorFunc : ThreadPoolWorkQueue.RunAsGreenThread;

        static partial void RunOnActualGreenThread(Action action, bool preferLocal, ref bool ranAsActualGreenThread)
        {
            ranAsActualGreenThread = true;
            ThreadPool.UnsafeQueueUserWorkItem(s_runAsGreenThreadInlineDelegate, action, preferLocal);
        }

        static partial void TryRunOnActualGreenThreadInline(Action action, ref bool success)
        {
            success = true;
            ExecutionContext.SendValueChangeNotificationsForResetToDefaultUnsafe();
            s_runAsGreenThreadInlineDelegate(action);
            ExecutionContext.SendValueChangeNotificationsForRestoreFromDefaultUnsafe();
        }

        // No-inlining is used here and on ClearTaskToWaitFor, to avoid the problem
        // of the t_TaskToWaitFor variable not being correctly handled. As we fix
        // thread static for Green threads, the need for this tweak should disappear.
        [MethodImpl(MethodImplOptions.NoInlining)]
        static partial void YieldGreenThread(Task taskToWaitForCompletion, ref bool yielded)
        {
            if (Thread.t_IsGreenThread)
            {
                unsafe
                {
                    Unsafe.AsRef<Task>(GreenThreadStatics.t_RefToTaskToWaitFor) = taskToWaitForCompletion;
                    ExecutionContext.SendValueChangeNotificationsForResetToDefaultUnsafe();
                    void* yieldReturn = GreenThread_Yield();
                    ExecutionContext.SendValueChangeNotificationsForRestoreFromDefaultUnsafe();

                    if (yieldReturn != null)
                    {
                        yielded = true;
                        GreenThreadStatics.t_RefToTaskToWaitFor = yieldReturn;
                    }
                }
            }
        }
    }
}
