// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//
// WASM in general does not have support for dynamic code generation without access to the host.
// Browser as a platform **does**, but we have so far decided to not support the APIs requiring
// thunks there anyway, hence this stub implementaiton of the thunk pool.
//
#pragma warning disable CA1822 // Mark members as static

using Internal.Runtime.CompilerHelpers;

namespace System.Runtime
{
    internal class ThunksHeap
    {
        public static unsafe ThunksHeap? CreateThunksHeap(IntPtr commonStubAddress)
        {
            throw new PlatformNotSupportedException();
        }

        public unsafe IntPtr AllocateThunk()
        {
            throw new PlatformNotSupportedException();
        }

        public unsafe void FreeThunk(IntPtr thunkAddress)
        {
            throw new PlatformNotSupportedException();
        }

        public unsafe bool TryGetThunkData(IntPtr thunkAddress, out IntPtr context, out IntPtr target)
        {
            throw new PlatformNotSupportedException();
        }

        public unsafe void SetThunkData(IntPtr thunkAddress, IntPtr context, IntPtr target)
        {
            throw new PlatformNotSupportedException();
        }
    }
}
