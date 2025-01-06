// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.JitInterface;
using Internal.Text;
using Internal.TypeSystem;
using Internal.ReadyToRunConstants;
using ILCompiler.DependencyAnalysisFramework;
using System.Collections.Generic;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    // An InterpreterImport simply a pointer sized variable that the 
    // interpreter stub can use to reference the InterpreterMethodInfo
    public class InterpreterImport : ObjectNode, ISymbolDefinitionNode
    {
        public int _id;
        public static int id = 1;

        public InterpreterImport()
        {
            // TODO, andrewau, we need a mechanism to identify the call sites
            // using a ID is not going to be deterministic 
            _id = id++;
        }

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return this._id - ((InterpreterImport)(other))._id;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.AddSymbol(this);
            builder.EmitZeroPointer();
            return builder.ToObjectData();
        }

        public int Offset { get; set; }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
            => sb.Append(nameMangler.CompilationUnitPrefix).Append("InterpreterImport").Append(_id.ToString());

        public override ObjectNodeSection GetSection(NodeFactory factory)
        {
            return ObjectNodeSection.DataSection;
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context)
        {
            yield break;
        }

        protected override string GetName(NodeFactory context) => "InterpreterImport";

        public override int ClassCode => 46709394;

        public override bool IsShareable => false;

        public override bool StaticDependenciesAreComputed => true;
    }
}
