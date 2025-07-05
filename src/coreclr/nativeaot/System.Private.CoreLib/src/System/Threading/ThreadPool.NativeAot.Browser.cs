// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Threading;

public static partial class ThreadPool
{
    // Indicates whether the thread pool should yield the thread from the dispatch loop to the runtime periodically so that
    // the runtime may use the thread for processing other work
    internal static bool YieldFromDispatchLoop => true;
}
