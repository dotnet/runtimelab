// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis.Wasm;

using CombinedDependencyList = System.Collections.Generic.List<ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.CombinedDependencyListEntry>;

namespace ILCompiler.DependencyAnalysis
{
    public partial class UnboxingStubNode
    {
        protected override void EmitCode(NodeFactory factory, ref WasmEmitter encoder, bool relocsOnly)
        {
            encoder.Builder.EmitReloc(GetUnderlyingMethodEntrypoint(factory), RelocType.IMAGE_REL_BASED_REL32);
        }

        public override bool HasConditionalStaticDependencies => true;

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory)
        {
            CombinedDependencyList dependencies = null;
            UnboxingStubTargetMappingNode.AddConditionalMappingDependency(ref dependencies, factory, this);
            return dependencies ?? (IEnumerable<CombinedDependencyListEntry>)Array.Empty<CombinedDependencyListEntry>();
        }
    }
}
