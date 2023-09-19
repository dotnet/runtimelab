// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Reflection;
using System.Reflection.Emit;
using Internal.Reflection.Emit;
using System.Runtime.InteropServices;

using Internal.Mint.Abstraction;

namespace Internal.Mint;

public sealed class DynamicMethodCompiler : IDisposable
{
    private readonly DynamicMethod _dynamicMethod;
    private readonly MemoryManager _compilationMemoryManager = new MemoryManager();
    private readonly MemoryManager _execMemoryManager;
    private readonly MintTypeSystem _mintTypeSystem;
    public DynamicMethodCompiler(DynamicMethod dynamicMethod, MemoryManager execMemoryManager)
    {
        _dynamicMethod = dynamicMethod;
        _execMemoryManager = execMemoryManager;
        // N.B. for dynamic methods, transform.c generally allocates everything from
        // a mempool that lives as long as the dynamic method itself
        _mintTypeSystem = new MintTypeSystem(_execMemoryManager, Mint.GlobalMintTypeSystem);
    }

    internal MemoryManager CompilationMemoryManager => _compilationMemoryManager;

    public void Dispose()
    {
        // free compilation mempools
        _compilationMemoryManager.Dispose();
    }

    internal unsafe CompiledMethod Compile()
    {
        // FIXME: concurrency - we don't want to compile the same method twice
        //
        var monoMethodPtr = _mintTypeSystem.GetMonoMethodPointer(_dynamicMethod);
        var method = new InterpMethodPtr(Mint.mint_testing_transform_sample(monoMethodPtr.Value));
        return new CompiledMethod(_dynamicMethod, method, _execMemoryManager);
    }


}
