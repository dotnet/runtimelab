// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ILCompiler.DependencyAnalysisFramework;

using CombinedDependencyList = System.Collections.Generic.List<ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.CombinedDependencyListEntry>;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// WASM cannot disassemble the stubs to get their targets so instead, the compiler generates an array of the
    /// these nodes, which represent a tuple of { UnboxingStub, UnboxingStub.Target }), to be used by the runtime
    /// to populate a hashtable that maps stubs to their targets. Note that we use this mechanism for both flavors
    /// of unboxing stubs: ordinary and "special" (instantiating). The latter would ordinarily be stored alongside
    /// the unwind info as "associated method data".
    /// </summary>
    public sealed class UnboxingStubTargetMappingNode : SortableDependencyNode
    {
        private readonly IMethodNode _unboxingStubNode;

        public UnboxingStubTargetMappingNode(IMethodNode unboxingStubNode)
        {
            Debug.Assert(unboxingStubNode is UnboxingStubNode or ISpecialUnboxThunkNode { IsSpecialUnboxingThunk: true });
            _unboxingStubNode = unboxingStubNode;
        }

        public IMethodNode Stub => _unboxingStubNode;
        public ISymbolNode GetTarget(NodeFactory factor) => GetUnboxingStubTarget(factor, Stub);

        protected override string GetName(NodeFactory factory) => $"Target mapping for {_unboxingStubNode.GetMangledName(factory.NameMangler)}";

        protected override void OnMarked(NodeFactory factory) => factory.UnboxingStubTargetMappings.AddMapping(this);

        public override bool StaticDependenciesAreComputed => true;
        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory context) => null;

        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool HasDynamicDependencies => false;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context) => null;

        public override bool HasConditionalStaticDependencies => false;
        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory context) => null;

        public override int ClassCode => 1371471006;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            // We rely on sorting regular unboxing stubs before instantiating ones.
            IMethodNode stub = Stub;
            IMethodNode otherStub = ((UnboxingStubTargetMappingNode)other).Stub;
            if (stub.GetType() != otherStub.GetType())
            {
                return stub is UnboxingStubNode ? -1 : 1;
            }

            return comparer.Compare(stub, otherStub);
        }

        public static void AddConditionalMappingDependency(ref CombinedDependencyList dependencies, NodeFactory factory, IMethodNode unboxingStubNode)
        {
            // The mapping is only needed if the target will actually be present in the output.
            if (factory.UseUnboxingStubTargetMappings && GetUnboxingStubTarget(factory, unboxingStubNode) is ISymbolNode target)
            {
                dependencies ??= new();
                UnboxingStubTargetMappingNode mapping = factory.UnboxingStubTargetMapping(unboxingStubNode);
                dependencies.Add(new(mapping, target, "Unboxing stub target mapping"));
            }
        }

        private static ISymbolNode GetUnboxingStubTarget(NodeFactory factory, IMethodNode stub) => stub switch
        {
            UnboxingStubNode simple => simple.GetUnderlyingMethodEntrypoint(factory),
            ISpecialUnboxThunkNode { IsSpecialUnboxingThunk: true } instantiating => instantiating.GetUnboxingThunkTarget(factory),
            _ => null
        };
    }

    public sealed class UnboxingStubTargetMappingsNode : EmbeddedDataContainerNode
    {
        private readonly List<UnboxingStubTargetMappingNode> _mappings = new();

        public UnboxingStubTargetMappingsNode() : base("__UnboxingStubTargetsMappings")
        {
        }

        public override bool IsShareable => true;
        public override bool StaticDependenciesAreComputed => true;

        public override ObjectNodeSection GetSection(NodeFactory factory) => factory.Target.IsWindows
            ? ObjectNodeSection.UnboxingStubWindowsContentSection
            : ObjectNodeSection.UnboxingStubUnixContentSection;

        public override bool ShouldSkipEmittingObjectNode(NodeFactory factory) => _mappings.Count == 0;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            if (relocsOnly)
            {
                // This node does not trigger generation of other nodes.
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new ISymbolDefinitionNode[] { this });
            }

            ObjectDataBuilder builder = new(factory, relocsOnly);
            builder.RequireInitialPointerAlignment();
            builder.AddSymbol(this);

            _mappings.Sort(CompilerComparer.Instance);

            int simpleUnboxingStubCount = 0;
            ObjectDataBuilder.Reservation simpleUnboxingStubCountSlot = builder.ReserveInt();
            foreach (UnboxingStubTargetMappingNode mapping in _mappings)
            {
                if (mapping.Stub is UnboxingStubNode)
                {
                    simpleUnboxingStubCount++;
                }

                builder.EmitPointerReloc(mapping.Stub);
                builder.EmitPointerReloc(mapping.GetTarget(factory));
            }

            Debug.Assert(simpleUnboxingStubCount == 0 || _mappings[0].Stub is UnboxingStubNode);
            builder.EmitInt(simpleUnboxingStubCountSlot, simpleUnboxingStubCount);

            return builder.ToObjectData();
        }

        public void AddMapping(UnboxingStubTargetMappingNode mapping)
        {
            lock (_mappings)
            {
                _mappings.Add(mapping);
            }
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);
    }
}
