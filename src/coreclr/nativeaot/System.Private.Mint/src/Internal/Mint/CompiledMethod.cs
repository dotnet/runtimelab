// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Reflection.Emit;
using Internal.Reflection.Emit;
using System.Runtime.InteropServices;

using Internal.Mint.Abstraction;

namespace Internal.Mint;

internal sealed class CompiledMethod : IMintCompiledMethod, IDisposable
{
    public MethodInfo MethodInfo { get; }
    public InterpMethodPtr InterpMethod { get; }
    public MemoryManager ExecMemoryManager { get; }
    public CompiledMethod(MethodInfo methodInfo, InterpMethodPtr interpMethod, MemoryManager execMemoryManager)
    {
        MethodInfo = methodInfo;
        InterpMethod = interpMethod;
        ExecMemoryManager = execMemoryManager;
    }

    ~CompiledMethod() => Dispose(false);
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

#pragma warning disable IDE0060 // Remove unused parameter
    private void Dispose(bool disposing)
    {
        ExecMemoryManager?.Dispose();
    }
#pragma warning restore IDE0060 // Remove unused parameter

    public object Invoke(object[] args)
    {
        return Mint.BigHackyExecCompiledMethod(MethodInfo, this, args);
    }

}
