// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using ILCompiler.DependencyAnalysisFramework;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal abstract class LLVMMethodCodeNode : MethodCodeNode// DependencyNodeCore<NodeFactory>
    {
        protected readonly MethodDesc _method;
        protected DependencyList _dependencies;

        protected LLVMMethodCodeNode(MethodDesc method) : base(method)
        {
            Debug.Assert(!method.IsAbstract);
            _method = method;
        }

        public void SetDependencies(DependencyList dependencies)
        {
            Debug.Assert(dependencies != null);
            _dependencies = dependencies;
        }
        
        public override bool StaticDependenciesAreComputed => CompilationCompleted;

        public bool CompilationCompleted { get; set; }


        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool HasDynamicDependencies => false;
        public override bool HasConditionalStaticDependencies => false;

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
    }

    internal class LlvmMethodBodyNode : LLVMMethodCodeNode, IMethodBodyNode
    {
        public LlvmMethodBodyNode(MethodDesc method)
            : base(method)
        {
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        // public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        // {
        //     var dependencies = new DependencyList();
        //
        //     foreach (DependencyListEntry node in _dependencies)
        //         dependencies.Add(node);
        //
        //     return dependencies;
        // }
        //
        int ISortableNode.ClassCode => -1502960727;

        int ISortableNode.CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_method, ((LlvmMethodBodyNode)other)._method);
        }
    }
}
