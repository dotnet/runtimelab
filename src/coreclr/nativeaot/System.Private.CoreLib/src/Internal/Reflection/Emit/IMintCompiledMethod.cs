// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection.Emit;

namespace Internal.Reflection.Emit;

#if FEATURE_MINT
public interface IMintCompiledMethod
{
    object? Invoke(object?[] arguments); //FIXME: no support for "this" pointer
}
#endif
