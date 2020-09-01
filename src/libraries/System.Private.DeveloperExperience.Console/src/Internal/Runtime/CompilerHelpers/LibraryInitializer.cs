// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Internal.Runtime.CompilerHelpers
{
    public class LibraryInitializer
    {
        public static void InitializeLibrary()
        {
            // Used by Native AOT CoreLib to write stack traces to console when an unhandled exception happens.
            // The usual approach (use reflection out of CoreLib to access things CoreLib can't statically reference)
            // is not low level enough for the purposes of stack trace printing.
            AppContext.SetData("System.Runtime.ExceptionServices.WriteStackTraceString", new Action<string>(
                // We create a lambda on purpose to prevent fetching Error here: InitializeLibrary is called
                // as part of the startup path and  we don't want it to do too much work.
                (s) => System.Console.Error.WriteLine(s)));
        }
    }
}
