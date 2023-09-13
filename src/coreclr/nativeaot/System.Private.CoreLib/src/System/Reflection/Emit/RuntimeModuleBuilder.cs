// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Reflection.Emit
{
    // FIXME: Not an actual implementation just a workaround
    static class RuntimeModuleBuilder
    {
        internal static Module GetRuntimeModuleFromModule(Module? m) => m as System.Reflection.Runtime.Modules.RuntimeModule;
    }
}
