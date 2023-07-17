// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Base class for the stub target mappings nodes. Represents an array of { Stub, Target }
    /// tuples, used by the runtime to implement portable mapping of stubs to their targets.
    /// </summary>
    internal abstract class StubTargetMappingsNode : ObjectNode, ISymbolDefinitionNode, INodeWithSize
    {
        private int? _size;

        int INodeWithSize.Size => _size.Value;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb) => sb.Append(nameMangler.CompilationUnitPrefix).Append(GetNodeName());

        public int Offset => 0;
        public override bool IsShareable => true;

        public override ObjectNodeSection GetSection(NodeFactory factory) => ObjectNodeSection.DataSection;

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            // This node does not trigger generation of other nodes.
            if (relocsOnly)
                return new ObjectData(Array.Empty<byte>(), Array.Empty<Relocation>(), 1, new[] { this });

            ObjectDataBuilder builder = new(factory, relocsOnly);
            builder.RequireInitialPointerAlignment();
            builder.AddSymbol(this);

            foreach (IMethodNode stub in GetStubs(factory))
            {
                if (GetTarget(factory, stub) is { Marked: true } target)
                {
                    builder.EmitPointerReloc(stub);
                    builder.EmitPointerReloc(target);
                }
            }

            _size = builder.CountBytes;

            return builder.ToObjectData();
        }

        protected abstract ReadOnlySpan<byte> GetNodeName();
        protected abstract IEnumerable<IMethodNode> GetStubs(NodeFactory factory);
        protected abstract ISymbolNode GetTarget(NodeFactory factory, IMethodNode stub);

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        protected internal override int Phase => (int)ObjectNodePhase.Ordered;
    }

    internal sealed class UnboxingStubTargetMappingsNode : StubTargetMappingsNode
    {
        public UnboxingStubTargetMappingsNode()
        {
        }

        protected override ReadOnlySpan<byte> GetNodeName() => "__unboxing_stub_targets"u8;
        protected override IEnumerable<IMethodNode> GetStubs(NodeFactory factory) => factory.MetadataManager.GetUnboxingStubs();
        protected override ISymbolNode GetTarget(NodeFactory factory, IMethodNode stub) => ((UnboxingStubNode)stub).GetUnderlyingMethodEntrypoint(factory);

        public override int ClassCode => (int)ObjectNodeOrder.UnboxingStubTargetsMapNode;
    }

    internal sealed class UnboxingAndInstantiatingStubTargetMappingsNode : StubTargetMappingsNode
    {
        public UnboxingAndInstantiatingStubTargetMappingsNode()
        {
        }

        protected override ReadOnlySpan<byte> GetNodeName() => "__unboxing_and_instantiating_stub_targets"u8;
        protected override IEnumerable<IMethodNode> GetStubs(NodeFactory factory) => factory.MetadataManager.GetUnboxingAndInstantiatingStubs();
        protected override ISymbolNode GetTarget(NodeFactory factory, IMethodNode stub) => ((ISpecialUnboxThunkNode)stub).GetUnboxingThunkTarget(factory);

        public override int ClassCode => (int)ObjectNodeOrder.UnboxingAndInstantiatingStubTargetsMapNode;
    }
}
