// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection.Emit;
using Internal.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Internal.Mint.Abstraction;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct MonoMethodInstanceAbstractionNativeAot
{
    public byte* name;
    public IntPtr /*MonoClass* */ klass;

    public IntPtr get_signature; // MonoMethodSignature* (* get_signature) (MonoMethod* self);
    public IntPtr get_header; // MonoMethodHeader* (* get_header) (MonoMethod* self);

    public IntPtr gcHandle; // FIXME
}
