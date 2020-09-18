// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Internal
{
    //
    // Simple limited console class for internal printf-style debugging in System.Private.CoreLib
    // and low-level tests that want to call System.Private.CoreLib directly
    //

    public static class Console
    {
#if TARGET_WINDOWS
        private static readonly SafeFileHandle _outputHandle =
            new SafeFileHandle(Interop.Kernel32.GetStdHandle(Interop.Kernel32.HandleTypes.STD_OUTPUT_HANDLE), ownsHandle: false);
#endif

        public static unsafe void Write(string s)
        {
#if TARGET_WINDOWS
            byte[] bytes = Encoding.UTF8.GetBytes(s);
            fixed (byte* pBytes = bytes)
            {
                Interop.Kernel32.WriteFile(_outputHandle, pBytes, bytes.Length, out _, IntPtr.Zero);
            }
#else
            Interop.Sys.PrintF("%s", s);
#endif
        }

        public static void WriteLine(string? s) =>
            Write(s + Environment.NewLineConst);

        public static void WriteLine() =>
            Write(Environment.NewLineConst);
    }
}
