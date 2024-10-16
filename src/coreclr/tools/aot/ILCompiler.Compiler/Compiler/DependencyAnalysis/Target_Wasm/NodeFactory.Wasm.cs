// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using ILCompiler.DependencyAnalysis.Wasm;

using Internal.Runtime;

namespace ILCompiler.DependencyAnalysis
{
    public abstract partial class NodeFactory
    {
        // TODO-LLVM: factor out into the CompilerTypeSystemContext like the virtual unwind mode.
        public virtual bool TargetsEmulatedEH() => throw new NotSupportedException();

        private void AddWasmSpecificSections(ReadyToRunHeaderNode header)
        {
            if (!_context.Target.IsWasm)
                return;

            if (_context.WasmMethodLevelVirtualUnwindModel == WasmMethodLevelVirtualUnwindModel.Precise)
            {
                WasmPreciseVirtualUnwindInfoNode node = new WasmPreciseVirtualUnwindInfoNode();
                header.Add(MetadataManager.BlobIdToReadyToRunSection(ReflectionMapBlob.BlobIdWasmPreciseVirtualUnwindInfo), node);
            }
        }
    }
}
