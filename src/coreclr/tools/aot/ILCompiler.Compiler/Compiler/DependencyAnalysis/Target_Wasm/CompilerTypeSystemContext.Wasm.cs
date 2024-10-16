// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

using ILCompiler.DependencyAnalysis.Wasm;

using Internal.TypeSystem;

namespace ILCompiler
{
    public partial class CompilerTypeSystemContext
    {
        private WasmMethodLevelVirtualUnwindModel? _wasmMethodLevelVirtualUnwindModel;
        private uint? _wasmGlobalBase;

        public WasmMethodLevelVirtualUnwindModel WasmMethodLevelVirtualUnwindModel => _wasmMethodLevelVirtualUnwindModel.Value;
        public uint WasmGlobalBase => _wasmGlobalBase.Value;

        public void InitializeWasmCompilationOptions(
            WasmMethodLevelVirtualUnwindModel? virtualUnwindModel,
            bool stackTracesEnabled,
            uint globalBase)
        {
            Debug.Assert(Target.IsWasm);

            // WASI does not have a native virtual unwind API and so the precise model needs to be used for stack trace support.
            virtualUnwindModel ??= stackTracesEnabled && Target.OperatingSystem == TargetOS.Wasi
                ? WasmMethodLevelVirtualUnwindModel.Precise
                : WasmMethodLevelVirtualUnwindModel.Native;
            _wasmMethodLevelVirtualUnwindModel = virtualUnwindModel.Value;

            if (globalBase == 0)
            {
                throw new InvalidOperationException("WASM global base cannot be 0 (null)");
            }
            _wasmGlobalBase = globalBase;
        }
    }
}
