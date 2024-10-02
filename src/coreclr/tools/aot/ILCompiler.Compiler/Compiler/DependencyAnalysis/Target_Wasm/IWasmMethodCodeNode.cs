// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace ILCompiler.DependencyAnalysis.Wasm
{
    public interface IWasmMethodCodeNode : IMethodBodyNode
    {
        WasmMethodPreciseVirtualUnwindInfoNode PreciseVirtualUnwindInfo { get; }

        void InitializePreciseVirtualUnwindInfo(WasmMethodPreciseVirtualUnwindInfoNode info);
    }
}
