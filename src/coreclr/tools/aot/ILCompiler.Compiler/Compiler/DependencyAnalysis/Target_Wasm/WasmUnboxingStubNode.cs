// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ILCompiler.DependencyAnalysis.Wasm;

namespace ILCompiler.DependencyAnalysis
{
    public partial class UnboxingStubNode
    {
        protected override void EmitCode(NodeFactory factory, ref WasmEmitter encoder, bool relocsOnly)
        {
            IMethodNode target = GetUnderlyingMethodEntrypoint(factory);
            if (relocsOnly)
            {
                encoder.Builder.EmitPointerReloc(target);
                return;
            }

            encoder.DefineLocals();

            uint argCount = (uint)WasmAbi.GetWasmFunctionType(target.Method).Parameters.Length;
            for (uint argIndex = 0; argIndex < argCount; argIndex++)
            {
                encoder.EmitLocalGet(argIndex);
                if (argIndex == 1)
                {
                    // Adjust "this" by the method table offset.
                    encoder.EmitNaturalAddConst(factory.Target.PointerSize);
                }
            }

            encoder.EmitCall(target);
            encoder.EmitEnd();
        }

        public override WasmFunctionType GetWasmFunctionType(NodeFactory factory) => WasmAbi.GetWasmFunctionType(Method);
    }
}
