// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;

namespace Internal
{
    public static partial class Console
    {
        public static void Write(string s)
        {
            WriteCore(Interop.Kernel32.GetStdHandle(Interop.Kernel32.HandleTypes.STD_OUTPUT_HANDLE), s);
        }

        public static partial class Error
        {
            public static void Write(string s)
            {
                WriteCore(Interop.Kernel32.GetStdHandle(Interop.Kernel32.HandleTypes.STD_ERROR_HANDLE), s);
            }
        }

        private static Encoding s_encoding;

        private static unsafe void WriteCore(IntPtr handle, string s)
        {
            if (IsHandleRedirected(handle))
            {
                if (s_encoding == null)
                {
                    s_encoding =
                        EncodingHelper.GetSupportedConsoleEncoding((int)Interop.Kernel32.GetConsoleOutputCP());
                }

                byte[] bytes = s_encoding.GetBytes(s);
                fixed (byte* pBytes = bytes)
                {
                    Interop.Kernel32.WriteFile(handle, pBytes, bytes.Length, out _, IntPtr.Zero);
                }
            }
            else
            {
                fixed (char* pChars = s)
                {
                    Interop.Kernel32.WriteConsole(handle, (byte*)pChars, s.Length, out _, IntPtr.Zero);
                }
            }
        }

        private static bool IsHandleRedirected(IntPtr handle)
        {
            // If handle is not to a character device, we must be redirected:
            uint fileType = Interop.Kernel32.GetFileType(handle);
            if ((fileType & Interop.Kernel32.FileTypes.FILE_TYPE_CHAR) != Interop.Kernel32.FileTypes.FILE_TYPE_CHAR)
                return true;

            // We are on a char device if GetConsoleMode succeeds and so we are not redirected.
            return (!Interop.Kernel32.IsGetConsoleModeCallSuccessful(handle));
        }
    }
}
