// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection.Emit;
using Internal.Reflection.Emit;
using System.Runtime.InteropServices;

namespace Internal.Mint.Abstraction;

internal readonly record struct InterpMethodPtr(IntPtr Value);

internal readonly unsafe struct MonoMethodPtr
{
    public readonly MonoMethodInstanceAbstractionNativeAot* Value;

    public MonoMethodPtr(MonoMethodInstanceAbstractionNativeAot* value)
    {
        Value = value;
    }
};
