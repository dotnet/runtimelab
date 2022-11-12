// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Threading.Tasks
{
    /// <summary>
    /// An implementation of TaskScheduler that tries to schedule tasks on green threads
    /// </summary>
    internal sealed class GreenThreadTaskScheduler : ThreadPoolTaskScheduler
    {
        public static readonly GreenThreadTaskScheduler Instance = new();

        private GreenThreadTaskScheduler() : base(false) { }

        /// <summary>
        /// Schedules a task to run as a green thread if possible.
        /// </summary>
        /// <param name="task">The task to schedule.</param>
        protected internal override void QueueTask(Task task)
        {
            TaskCreationOptions options = task.Options;
            if (Thread.IsThreadStartSupported && (options & TaskCreationOptions.LongRunning) != 0)
            {
                // Use the base task scheduler for LongRunning tasks.
                base.QueueTask(task);
            }
            else
            {
                // Normal handling for non-LongRunning tasks.
                Task.UnsafeRunAsGreenThread(task, preferLocal: (options & TaskCreationOptions.PreferFairness) == 0);
            }
        }

        /// <summary>
        /// This internal function will do this:
        ///   (1) If the task had previously been queued, attempt to pop it and return false if that fails.
        ///   (2) Return whether the task is executed
        ///
        /// IMPORTANT NOTE: TryExecuteTaskInline will NOT throw task exceptions itself. Any wait code path using this function needs
        /// to account for exceptions that need to be propagated, and throw themselves accordingly.
        /// </summary>
        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            if (Thread.IsGreenThread)
            {
                return base.TryExecuteTaskInline(task, taskWasPreviouslyQueued);
            }

            // If the task was previously scheduled, and we can't pop it, then return false.
            if (taskWasPreviouslyQueued && !ThreadPool.TryPopCustomWorkItem(task))
                return false;

            bool success = Task.UnsafeTryRunAsGreenThreadInline(task);

            // Only call NWIP() if task was previously queued
            if (success && taskWasPreviouslyQueued) NotifyWorkItemProgress();
            return success;
        }
    }
}
