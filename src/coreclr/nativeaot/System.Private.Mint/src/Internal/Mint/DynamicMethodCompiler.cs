// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection.Emit;
using Internal.Reflection.Emit;
using System.Runtime.InteropServices;

using Internal.Mint.Abstraction;

namespace Internal.Mint;

public sealed class DynamicMethodCompiler : IDisposable
{
    private readonly DynamicMethod _dynamicMethod;
    private readonly MemoryManager _compilationMemoryManager = new MemoryManager();
    private readonly MintTypeSystem _mintTypeSystem;
    public DynamicMethodCompiler(DynamicMethod dynamicMethod)
    {
        _dynamicMethod = dynamicMethod;
        _mintTypeSystem = new MintTypeSystem(_compilationMemoryManager);
    }

    internal MemoryManager CompilationMemoryManager => _compilationMemoryManager;

    public void Dispose()
    {
        // free compilation mempools
        _compilationMemoryManager.Dispose();
    }

    internal readonly record struct CompiledMethod(InterpMethodPtr InterpMethod, MemoryManager ExecMemoryManager);

    internal unsafe CompiledMethod Compile()
    {
        // FIXME: concurrency - we don't want to compile the same method twice
        //
        var execMemoryManager = new MemoryManager();
        // We want to tell the current thread's transform.c to use this set of memory managers for allocations.
        // We can do this by setting a thread local variable, but we need to make sure to unset it when we're done.
        // Also we need to be sure that the current thread is the one that will end up compiling
        // the current method, otherwise we might associate the wrong dynamic memory manager with the InterpMethod.

        // using var _unsetMemoryManagers = Mint.SetThreadLocalMemoryManagers (CompilationMemoryManager, dynamicMemoryManager)
        var monoMethodPtr = _mintTypeSystem.CreateMonoMethodPointer(_dynamicMethod);
        var method = new InterpMethodPtr(Mint.mint_testing_transform_sample(monoMethodPtr.Value));
        return new CompiledMethod
        {
            InterpMethod = method,
            ExecMemoryManager = execMemoryManager,
        };
    }


}
