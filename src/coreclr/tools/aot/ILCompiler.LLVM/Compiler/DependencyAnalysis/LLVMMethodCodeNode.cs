// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using ILCompiler.DependencyAnalysisFramework;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal abstract class LLVMMethodCodeNode : DependencyNodeCore<NodeFactory>, IMethodCodeNode
    {
        private bool _isStateMachineMoveNextMethod;

        protected readonly MethodDesc _method;
        protected DependencyList _dependencies;

        protected LLVMMethodCodeNode(MethodDesc method)
        {
            Debug.Assert(!method.IsAbstract);
            _method = method;
        }

        public void SetDependencies(DependencyList dependencies)
        {
            Debug.Assert(dependencies != null);
            _dependencies = dependencies;
        }
        
        public MethodDesc Method
        {
            get
            {
                return _method;
            }
        }

        public override bool StaticDependenciesAreComputed => CompilationCompleted;

        public bool CompilationCompleted { get; set; }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.GetMangledMethodName(_method));
        }
        public int Offset => 0;
        public bool RepresentsIndirectionCell => false;

        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool HasDynamicDependencies => false;
        public override bool HasConditionalStaticDependencies => false;

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory) => null;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;
        public int ClassCode { get; }

        public int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_method, ((LLVMMethodCodeNode)other)._method);
        }

        public void SetCode(ObjectNode.ObjectData data, bool isFoldable)
        {
            DependencyListEntry[] entries = new DependencyListEntry[data.Relocs.Length];
            for (int i = 0; i < data.Relocs.Length; i++)
            {
                entries[i] = new DependencyListEntry(data.Relocs[i].Target, "ObjectData Reloc");
            }

            _dependencies = new DependencyList(entries);
        }

        public void InitializeFrameInfos(FrameInfo[] frameInfos)
        {
        }

        public void InitializeDebugEHClauseInfos(DebugEHClauseInfo[] debugEhClauseInfos)
        {
        }

        public void InitializeGCInfo(byte[] gcInfo)
        {
        }

        public void InitializeEHInfo(ObjectNode.ObjectData ehInfo)
        {
        }

        public void InitializeDebugLocInfos(DebugLocInfo[] debugLocInfos)
        {
        }

        public void InitializeDebugVarInfos(DebugVarInfo[] debugVarInfos)
        {
        }

        public void InitializeNonRelocationDependencies(DependencyList additionalDependencies)
        {
            if (additionalDependencies == null) return;

            for (int i = 0; i < additionalDependencies.Count; i++)
            {
                _dependencies.Add(additionalDependencies[i]);
            }
        }

        public void InitializeIsStateMachineMoveNextMethod(bool value)
        {
            _isStateMachineMoveNextMethod = value;
        }
    }

    internal class LlvmMethodBodyNode : LLVMMethodCodeNode, IMethodBodyNode
    {
        public LlvmMethodBodyNode(MethodDesc method)
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

        int ISortableNode.ClassCode => -1502960727;

        int ISortableNode.CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_method, ((LlvmMethodBodyNode)other)._method);
        }
    }
}
