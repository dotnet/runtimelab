// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal class LlvmUnboxingThunkNode : LLVMMethodCodeNode, IMethodNode
    {
        public LlvmUnboxingThunkNode(MethodDesc method)
            : base(method)
        {
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            var dependencies = new DependencyList();

            foreach (DependencyListEntry node in _dependencies)
                dependencies.Add(node);

            return dependencies;
        }

        int ISortableNode.ClassCode => -18942467;

        int ISortableNode.CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_method, ((LlvmUnboxingThunkNode)other)._method);
        }
    }
}
