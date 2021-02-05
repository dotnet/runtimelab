// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ILCompiler.DependencyAnalysis.ARM;

namespace ILCompiler.DependencyAnalysis
{
    public partial class TentativeInstanceMethodNode
    {
        protected override void EmitCode(NodeFactory factory, ref ARMEmitter encoder, bool relocsOnly)
        {
            ISymbolNode target = GetTarget(factory);
            if (!target.RepresentsIndirectionCell)
            {
                encoder.EmitJMP(target); // b methodEntryPoint
            }
            else
            {
                encoder.EmitMOV(encoder.TargetRegister.InterproceduralScratch, target);
                encoder.EmitJMP(encoder.TargetRegister.InterproceduralScratch);
            }
        }
    }
}
