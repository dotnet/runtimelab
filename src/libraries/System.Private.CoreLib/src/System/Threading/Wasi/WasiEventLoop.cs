// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading.Tasks;
using WasiPollWorld.wit.imports.wasi.io.v0_2_1;

namespace System.Threading
{
    internal static class WasiEventLoop
    {
        private static List<TaskCompletionSource> s_pollables = new();

        internal static Task RegisterWasiPollableHandle(
            int handle,
            CancellationToken cancellationToken
        )
        {
            // note that this is duplicate of the original Pollable
            // the original should be neutralized without disposing the handle
            var pollableCpy = new IPoll.Pollable(new IPoll.Pollable.THandle(handle));
            return RegisterWasiPollable(pollableCpy, cancellationToken);
        }

        internal static Task RegisterWasiPollable(
            IPoll.Pollable pollable,
            CancellationToken cancellationToken
        )
        {
            var tcs = new TaskCompletionSource((pollable, cancellationToken));
            s_pollables.Add(tcs);
            return tcs.Task;
        }

        internal static void DispatchWasiEventLoop()
        {
            ThreadPoolWorkQueue.Dispatch();

            if (s_pollables.Count > 0)
            {
                var pollables = s_pollables;
                s_pollables = new List<TaskCompletionSource>(pollables.Count);
                var arguments = new List<IPoll.Pollable>(pollables.Count);
                var indexes = new List<int>(pollables.Count);
                var tasksCanceled = false;
                for (var i = 0; i < pollables.Count; i++)
                {
                    var tcs = pollables[i];
                    var (pollable, cancellationToken) = ((IPoll.Pollable, CancellationToken))
                        tcs.Task.AsyncState!;
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.SetCanceled(cancellationToken);
                        tasksCanceled = true;
                    }
                    else
                    {
                        arguments.Add(pollable);
                        indexes.Add(i);
                    }
                }

                if (arguments.Count > 0)
                {
                    var ready = new bool[arguments.Count];

                    // If at least one task was canceled, we'll return without
                    // calling `poll` (i.e. delay calling `poll` until the next
                    // call to this function) to give any dependent tasks a
                    // chance to make progress before we block.
                    if (!tasksCanceled)
                    {
                        // this is blocking until at least one pollable resolves
                        var readyIndexes = PollInterop.Poll(arguments);

                        foreach (int readyIndex in readyIndexes)
                        {
                            ready[readyIndex] = true;
                            arguments[readyIndex].Dispose();
                            var tcs = pollables[indexes[readyIndex]];
                            tcs.SetResult();
                        }
                    }

                    for (var i = 0; i < arguments.Count; ++i)
                    {
                        if (!ready[i])
                        {
                            s_pollables.Add(pollables[indexes[i]]);
                        }
                    }
                }
            }
        }
    }
}
