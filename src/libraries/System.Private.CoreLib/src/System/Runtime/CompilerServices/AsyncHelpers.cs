// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Runtime.CompilerServices
{
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [System.Diagnostics.CodeAnalysis.ExperimentalAttribute("SYSLIB5007", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
    public static partial class AsyncHelpers
    {
#if !NATIVEAOT && !MONO
        [BypassReadyToRun]
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.Async)]
        public static void AwaitAwaiter<TAwaiter>(TAwaiter awaiter) where TAwaiter : INotifyCompletion
        {
            ref AsyncHelpers.RuntimeAsyncAwaitState state = ref AsyncHelpers.t_runtimeAsyncAwaitState;
            Continuation? sentinelContinuation = state.SentinelContinuation;
            if (sentinelContinuation == null)
                state.SentinelContinuation = sentinelContinuation = new Continuation();

            state.Notifier = awaiter;
            AsyncHelpers.AsyncSuspend(sentinelContinuation);
        }

        [BypassReadyToRun]
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.Async)]
        public static void UnsafeAwaitAwaiter<TAwaiter>(TAwaiter awaiter) where TAwaiter : ICriticalNotifyCompletion
        {
            ref AsyncHelpers.RuntimeAsyncAwaitState state = ref AsyncHelpers.t_runtimeAsyncAwaitState;
            Continuation? sentinelContinuation = state.SentinelContinuation;
            if (sentinelContinuation == null)
                state.SentinelContinuation = sentinelContinuation = new Continuation();

            state.Notifier = awaiter;
            AsyncHelpers.AsyncSuspend(sentinelContinuation);
        }

        // Marked intrinsic since JIT recognises the helper by name when doing optimizations.
        [Intrinsic]
        [BypassReadyToRun]
        [MethodImpl(MethodImplOptions.Async)]
        public static T Await<T>(Task<T> task)
        {
            TaskAwaiter<T> awaiter = task.GetAwaiter();
            if (!awaiter.IsCompleted)
            {
                UnsafeAwaitAwaiter(awaiter);
            }

            return awaiter.GetResult();
        }

        // Marked intrinsic since JIT recognises the helper by name when doing optimizations.
        [Intrinsic]
        [BypassReadyToRun]
        [MethodImpl(MethodImplOptions.Async)]
        public static void Await(Task task)
        {
            TaskAwaiter awaiter = task.GetAwaiter();
            if (!awaiter.IsCompleted)
            {
                UnsafeAwaitAwaiter(awaiter);
            }

            awaiter.GetResult();
        }

        // Marked intrinsic since JIT recognises the helper by name when doing optimizations.
        [Intrinsic]
        [BypassReadyToRun]
        [MethodImpl(MethodImplOptions.Async)]
        public static T Await<T>(ValueTask<T> task)
        {
            ValueTaskAwaiter<T> awaiter = task.GetAwaiter();
            if (!awaiter.IsCompleted)
            {
                UnsafeAwaitAwaiter(awaiter);
            }

            return awaiter.GetResult();
        }

        // Marked intrinsic since JIT recognises the helper by name when doing optimizations.
        [Intrinsic]
        [BypassReadyToRun]
        [MethodImpl(MethodImplOptions.Async)]
        public static void Await(ValueTask task)
        {
            ValueTaskAwaiter awaiter = task.GetAwaiter();
            if (!awaiter.IsCompleted)
            {
                UnsafeAwaitAwaiter(awaiter);
            }

            awaiter.GetResult();
        }

        // Marked intrinsic since JIT recognises the helper by name when doing optimizations.
        [Intrinsic]
        [BypassReadyToRun]
        [MethodImpl(MethodImplOptions.Async)]
        public static void Await(ConfiguredTaskAwaitable configuredAwaitable)
        {
            ConfiguredTaskAwaitable.ConfiguredTaskAwaiter awaiter = configuredAwaitable.GetAwaiter();
            if (!awaiter.IsCompleted)
            {
                UnsafeAwaitAwaiter(awaiter);
            }

            awaiter.GetResult();
        }

        // Marked intrinsic since JIT recognises the helper by name when doing optimizations.
        [Intrinsic]
        [BypassReadyToRun]
        [MethodImpl(MethodImplOptions.Async)]
        public static void Await(ConfiguredValueTaskAwaitable configuredAwaitable)
        {
            ConfiguredValueTaskAwaitable.ConfiguredValueTaskAwaiter awaiter = configuredAwaitable.GetAwaiter();
            if (!awaiter.IsCompleted)
            {
                UnsafeAwaitAwaiter(awaiter);
            }

            awaiter.GetResult();
        }

        // Marked intrinsic since JIT recognises the helper by name when doing optimizations.
        [Intrinsic]
        [BypassReadyToRun]
        [MethodImpl(MethodImplOptions.Async)]
        public static T Await<T>(ConfiguredTaskAwaitable<T> configuredAwaitable)
        {
            ConfiguredTaskAwaitable<T>.ConfiguredTaskAwaiter awaiter = configuredAwaitable.GetAwaiter();
            if (!awaiter.IsCompleted)
            {
                UnsafeAwaitAwaiter(awaiter);
            }

            return awaiter.GetResult();
        }

        // Marked intrinsic since JIT recognises the helper by name when doing optimizations.
        [Intrinsic]
        [BypassReadyToRun]
        [MethodImpl(MethodImplOptions.Async)]
        public static T Await<T>(ConfiguredValueTaskAwaitable<T> configuredAwaitable)
        {
            ConfiguredValueTaskAwaitable<T>.ConfiguredValueTaskAwaiter awaiter = configuredAwaitable.GetAwaiter();
            if (!awaiter.IsCompleted)
            {
                UnsafeAwaitAwaiter(awaiter);
            }

            return awaiter.GetResult();
        }
#endif
    }
}
