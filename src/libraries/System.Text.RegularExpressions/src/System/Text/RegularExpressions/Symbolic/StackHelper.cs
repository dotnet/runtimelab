// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace System.Text.RegularExpressions.Symbolic
{
    /// <summary>
    /// Provides tools for avoiding stack overflows. The approach follows that of System.Linq.Expressions.StackGuard.
    /// </summary>
    internal static class StackHelper
    {
        /// <summary>
        /// Calls the provided function on the stack of a new thread if stack space is close to running out (as indicated
        /// by RuntimeHelpers.TryEnsureSufficientExecutionStack). Does nothing otherwise.
        /// </summary>
        /// <typeparam name="T">the return type of the function</typeparam>
        /// <param name="func">the function to possibly call</param>
        /// <param name="result">the return value of the function if it was called</param>
        /// <returns>whether the function was called</returns>
        public static bool CallOnEmptyStackIfNecessary<T>(Func<T> func, out T? result)
        {
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            {
                // Using default scheduler rather than picking up the current scheduler.
                Task<T> task = Task.Factory.StartNew(func, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
                // Task.Wait has the potential of inlining the task's execution on the current thread; avoid this.
                ((IAsyncResult)task).AsyncWaitHandle.WaitOne();
                // Using awaiter here to propagate original exception
                result = task.GetAwaiter().GetResult();
                return true;
            }
            else
            {
                result = default(T);
                return false;
            }
        }

        /// <summary>
        /// Calls the provided action on the stack of a new thread if stack space is close to running out (as indicated
        /// by RuntimeHelpers.TryEnsureSufficientExecutionStack). Does nothing otherwise.
        /// </summary>
        /// <param name="action">the action to possibly call</param>
        /// <returns>whether the action was called</returns>
        public static bool CallOnEmptyStackIfNecessary(Action action)
        {
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            {
                // Using default scheduler rather than picking up the current scheduler.
                Task task = Task.Factory.StartNew(action, CancellationToken.None, TaskCreationOptions.DenyChildAttach, TaskScheduler.Default);
                // Task.Wait has the potential of inlining the task's execution on the current thread; avoid this.
                ((IAsyncResult)task).AsyncWaitHandle.WaitOne();
                // Using awaiter here to propagate original exception
                task.GetAwaiter().GetResult();
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
