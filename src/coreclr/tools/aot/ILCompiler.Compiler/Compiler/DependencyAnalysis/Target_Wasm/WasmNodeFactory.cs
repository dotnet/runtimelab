// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace ILCompiler.DependencyAnalysis
{
    public partial class NodeFactory
    {
        private NodeCache<IMethodNode, UnboxingStubTargetMappingNode> _unboxingStubTargetMappings;

        public UnboxingStubTargetMappingsNode UnboxingStubTargetMappings { get; private set; }

        public bool UseUnboxingStubTargetMappings => UnboxingStubTargetMappings != null;

        public UnboxingStubTargetMappingNode UnboxingStubTargetMapping(IMethodNode unboxingStubNode)
        {
            return _unboxingStubTargetMappings.GetOrAdd(unboxingStubNode);
        }

        // WASM uses the shadow stack calling convention, which makes native entry points to runtime
        // exports incur a penalty of saving the shadow stack and setting up arguments. This method may
        // be overriden by a derived class to redirect from a native entry point to the managed one.
        public virtual IMethodNode RuntimeExportManagedEntrypoint(string name) => null;

        private void InitializeUnboxingStubTargetMappings()
        {
            // The unboxing map is only needed on WASM. Other targets disassemble the stub to get its target.
            if (Target.IsWasm)
            {
                UnboxingStubTargetMappings = new UnboxingStubTargetMappingsNode();
                _unboxingStubTargetMappings = new(stub => new UnboxingStubTargetMappingNode(stub));
            }
        }
    }
}
