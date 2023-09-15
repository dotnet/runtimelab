// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

namespace Internal.Mint.Abstraction;

// keep in sync with mint-ee-abstraction-nativeaot.h
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct EEItf
{
    public delegate* unmanaged<void> tls_initialize;
}
