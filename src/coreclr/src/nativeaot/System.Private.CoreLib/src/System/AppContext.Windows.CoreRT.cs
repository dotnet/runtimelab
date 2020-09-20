// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Text;

namespace System
{
    public static partial class AppContext
    {
        /// <summary>
        /// Return the directory of the executable image for the current process
        /// as the default value for AppContext.BaseDirectory
        /// </summary>
        private static string GetBaseDirectoryCore()
        {
            var builder = new ValueStringBuilder(stackalloc char[Interop.Kernel32.MAX_PATH]);

            uint length;
            while ((length = Interop.Kernel32.GetModuleFileName(IntPtr.Zero, ref builder.GetPinnableReference(), (uint)builder.Capacity)) >= builder.Capacity)
            {
                builder.EnsureCapacity((int)length);
            }

            if (length == 0)
                throw Win32Marshal.GetExceptionForLastWin32Error();

            builder.Length = (int)length;

            // Return path to the executable image including the terminating slash
            string fileName = builder.ToString();
            return fileName.Substring(0, fileName.LastIndexOf('\\') + 1);
        }
    }
}
