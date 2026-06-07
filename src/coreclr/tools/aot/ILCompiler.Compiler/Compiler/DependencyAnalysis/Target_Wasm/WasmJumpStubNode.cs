// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using ILCompiler.DependencyAnalysis.Wasm;

namespace ILCompiler.DependencyAnalysis
{
    public partial class JumpStubNode
    {
#if READYTORUN
        protected override void EmitCode(NodeFactory factory, ref WasmEmitter encoder, bool relocsOnly)
        {
            throw new NotImplementedException();
#else
        protected override void EmitCode(NodeFactory factory, ref LlvmWasmEmitter encoder, bool relocsOnly)
        {
            // Note: this node is currently never emitted on WASM.
            System.Diagnostics.Debug.Assert(relocsOnly || ShouldSkipEmittingObjectNode(factory));
            encoder.Builder.EmitPointerReloc(_target);
#endif
        }
    }
}
