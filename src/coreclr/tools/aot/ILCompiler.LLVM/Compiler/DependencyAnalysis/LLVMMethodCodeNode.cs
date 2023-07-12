// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.Text;
using Internal.TypeSystem;

using CombinedDependencyList = System.Collections.Generic.List<ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.CombinedDependencyListEntry>;

namespace ILCompiler.DependencyAnalysis
{
    internal sealed class LLVMMethodCodeNode : DependencyNodeCore<NodeFactory>, IMethodBodyNode, IMethodCodeNode, ISpecialUnboxThunkNode
    {
        private readonly MethodDesc _method;
        private DependencyList _dependencies;

        public LLVMMethodCodeNode(MethodDesc method)
        {
            Debug.Assert(!method.IsAbstract);
            _method = method;
        }

        public MethodDesc Method => _method;

        public bool CompilationCompleted { get; set; }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.GetMangledMethodName(_method));
        }

        public int Offset => 0;
        public bool RepresentsIndirectionCell => false;

        public override bool InterestingForDynamicDependencyAnalysis => false;
        public override bool HasDynamicDependencies => false;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;

        public override bool HasConditionalStaticDependencies => CodeBasedDependencyAlgorithm.HasConditionalDependenciesDueToMethodCodePresence(_method);

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory)
        {
            CombinedDependencyList dependencies = null;
            CodeBasedDependencyAlgorithm.AddConditionalDependenciesDueToMethodCodePresence(ref dependencies, factory, _method);
            return dependencies ?? (IEnumerable<CombinedDependencyListEntry>)Array.Empty<CombinedDependencyListEntry>();
        }

        public override bool StaticDependenciesAreComputed => CompilationCompleted;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory)
        {
            DependencyList dependencies = null;

            if (_dependencies != null)
            {
                dependencies = new DependencyList();
                foreach (DependencyListEntry node in _dependencies)
                {
                    dependencies.Add(node);
                }
            }

            return dependencies;
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

        public void InitializeDebugInfo(MethodDebugInformation debugInfo)
        {
        }

        public void InitializeLocalTypes(TypeDesc[] localTypes)
        {
        }

        public bool IsSpecialUnboxingThunk => ((CompilerTypeSystemContext)Method.Context).IsSpecialUnboxingThunk(_method);

        public ISymbolNode GetUnboxingThunkTarget(NodeFactory factory)
        {
            Debug.Assert(IsSpecialUnboxingThunk);

            MethodDesc nonUnboxingMethod = ((CompilerTypeSystemContext)Method.Context).GetTargetOfSpecialUnboxingThunk(_method);
            return factory.MethodEntrypoint(nonUnboxingMethod);
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public int ClassCode => -1502960727;

        public int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_method, ((LLVMMethodCodeNode)other)._method);
        }
    }
}
