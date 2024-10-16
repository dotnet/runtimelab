// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ILCompiler.DependencyAnalysis.Wasm;
using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.Text;
using Internal.TypeSystem;

using CombinedDependencyList = System.Collections.Generic.List<ILCompiler.DependencyAnalysisFramework.DependencyNodeCore<ILCompiler.DependencyAnalysis.NodeFactory>.CombinedDependencyListEntry>;

namespace ILCompiler.DependencyAnalysis
{
    internal sealed class LLVMMethodCodeNode : DependencyNodeCore<NodeFactory>, IMethodBodyNode, IMethodCodeNode, IWasmMethodCodeNode, ISpecialUnboxThunkNode
    {
        private readonly MethodDesc _method;
        private DependencyList _dependencies;

        public LLVMMethodCodeNode(MethodDesc method)
        {
            Debug.Assert(!method.IsAbstract);
            _method = method;
        }

        public MethodDesc Method => _method;
        public WasmMethodPreciseVirtualUnwindInfoNode PreciseVirtualUnwindInfo { get; private set; }
        public bool CompilationCompleted { get; set; }

        public int Offset => 0;
        public bool RepresentsIndirectionCell => false;

        public override bool InterestingForDynamicDependencyAnalysis => _method.HasInstantiation || _method.OwningType.HasInstantiation;
        public override bool HasDynamicDependencies => false;
        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory factory) => null;

        public override bool HasConditionalStaticDependencies => CodeBasedDependencyAlgorithm.HasConditionalDependenciesDueToMethodCodePresence(_method);

        public override IEnumerable<CombinedDependencyListEntry> GetConditionalStaticDependencies(NodeFactory factory)
        {
            CombinedDependencyList dependencies = null;
            CodeBasedDependencyAlgorithm.AddConditionalDependenciesDueToMethodCodePresence(ref dependencies, factory, _method);
            return dependencies ?? [];
        }

        public override bool StaticDependenciesAreComputed => CompilationCompleted;

        public override IEnumerable<DependencyListEntry> GetStaticDependencies(NodeFactory factory) => _dependencies;

        public void SetCode(ObjectNode.ObjectData data)
        {
            foreach (ref Relocation reloc in data.Relocs.AsSpan())
            {
                AddDependency(reloc.Target, "Referenced by code");
            }
        }

        public void InitializeFrameInfos(FrameInfo[] frameInfos) { }

        public void InitializeDebugEHClauseInfos(DebugEHClauseInfo[] debugEhClauseInfos) { }

        public void InitializeGCInfo(byte[] gcInfo) { }

        public void InitializeEHInfo(ObjectNode.ObjectData ehInfo) { }

        public void InitializeDebugLocInfos(DebugLocInfo[] debugLocInfos) { }

        public void InitializeDebugVarInfos(DebugVarInfo[] debugVarInfos) { }

        public void InitializeDebugInfo(MethodDebugInformation debugInfo) { }

        public void InitializeLocalTypes(TypeDesc[] localTypes) { }

        public void InitializeNonRelocationDependencies(DependencyList additionalDependencies)
        {
            if (additionalDependencies == null)
                return;

            for (int i = 0; i < additionalDependencies.Count; i++)
            {
                DependencyListEntry dependency = additionalDependencies[i];
                AddDependency(dependency.Node, dependency.Reason);
            }
        }

        public void InitializePreciseVirtualUnwindInfo(WasmMethodPreciseVirtualUnwindInfoNode info)
        {
            Debug.Assert(PreciseVirtualUnwindInfo is null);
            PreciseVirtualUnwindInfo = info;
        }

        public bool IsSpecialUnboxingThunk => ((CompilerTypeSystemContext)Method.Context).IsSpecialUnboxingThunk(_method);

        public ISymbolNode GetUnboxingThunkTarget(NodeFactory factory)
        {
            Debug.Assert(IsSpecialUnboxingThunk);

            MethodDesc nonUnboxingMethod = ((CompilerTypeSystemContext)Method.Context).GetTargetOfSpecialUnboxingThunk(_method);
            return factory.MethodEntrypoint(nonUnboxingMethod);
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.GetMangledMethodName(_method));
        }

        public int ClassCode => -1502960727;

        public int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return comparer.Compare(_method, ((LLVMMethodCodeNode)other)._method);
        }

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        private void AddDependency(object node, string reason)
        {
            _dependencies ??= new DependencyList();
            _dependencies.Add(new DependencyListEntry(node, reason));
        }
    }
}
