// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace SwiftRuntimeLibrary
{
    public unsafe interface ISwiftObject : IDisposable
    {
        void* SwiftObject { get; }
    }
}

