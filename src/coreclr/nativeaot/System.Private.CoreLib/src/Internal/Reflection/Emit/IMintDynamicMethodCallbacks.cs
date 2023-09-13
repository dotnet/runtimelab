// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection.Emit;

namespace Internal.Reflection.Emit;

#if FEATURE_MINT
public interface IMintDynamicMethodCallbacks
{
    IntPtr GetFunctionPointer(DynamicMethod dm); // FIXME just a placeholder
}
#endif
