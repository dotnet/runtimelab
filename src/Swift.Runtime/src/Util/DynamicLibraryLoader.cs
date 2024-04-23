// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace Swift.Runtime 
{
    /// <summary>
    /// Includes the native methods for loading dynamic libraries.
    /// </summary>
    class NativeMethods
    {
        [DllImport("libSystem.dylib")]
        public static extern IntPtr dlopen(string fileName, int flags);

        [DllImport("libSystem.dylib")]
        public static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport("libSystem.dylib")]
        public static extern int dlclose(IntPtr handle);

        public const int RTLD_NOW = 2;
    }

    /// <summary>
    /// Represents a dynamic library loader.
    /// </summary>
    public class DynamicLibraryLoader
    {
        delegate IntPtr MyFunctionDelegate();

        /// <summary>
        /// Executes a function from a dynamic library.
        /// </summary>
        public static IntPtr execute(string libraryPath, string functionName)
        {
            IntPtr libHandle = NativeMethods.dlopen(libraryPath, NativeMethods.RTLD_NOW);
            if (libHandle == IntPtr.Zero)
            {
                throw new Exception("Unable to load library.");
            }

            try
            {
                IntPtr funcPtr = NativeMethods.dlsym(libHandle, functionName);
                if (funcPtr == IntPtr.Zero)
                {
                    throw new Exception("Unable to find function.");
                }

                // Convert the IntPtr to a delegate
                MyFunctionDelegate func = (MyFunctionDelegate)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(MyFunctionDelegate));
                IntPtr result = func();
                return result;
            }
            finally
            {
                NativeMethods.dlclose(libHandle);
            }
        }
    }
}
