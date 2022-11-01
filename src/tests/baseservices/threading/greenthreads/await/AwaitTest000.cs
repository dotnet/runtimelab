// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

public partial class Test_greenthread_await_AwaitTest
{
    public static int Main() =>
        RunAwaitTest(inlineContinuation: false, useNonDefaultExecutionContext: false, configureAwait: false);
}
