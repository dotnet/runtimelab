// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

using ILCompiler.DependencyAnalysis.Wasm;

namespace ILCompiler.DependencyAnalysis
{
    public partial class TentativeMethodNode
    {
        protected override void EmitCode(NodeFactory factory, ref WasmEmitter encoder, bool relocsOnly)
        {
            IMethodNode target = GetTarget(factory);
            if (relocsOnly)
            {
                encoder.Builder.EmitPointerReloc(target);
                return;
            }

            encoder.DefineLocals();
            encoder.EmitLocalGet(0);
            encoder.EmitCall(GetTarget(factory));

            WasmValueType callerReturnType = WasmAbi.GetWasmReturnType(Method, out _);
            WasmValueType calleeReturnType = WasmValueType.Invalid;
            Debug.Assert(calleeReturnType == WasmAbi.GetWasmReturnType(target.Method, out _));
            encoder.EmitReturnAfterAlwaysThrowCall(callerReturnType, calleeReturnType, isEnd: true);
        }

        public override WasmFunctionType GetWasmFunctionType(NodeFactory factory) => WasmAbi.GetWasmFunctionType(Method);
    }
}
