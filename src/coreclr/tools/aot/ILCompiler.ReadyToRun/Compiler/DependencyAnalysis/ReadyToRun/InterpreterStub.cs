// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Internal.JitInterface;
using Internal.Text;
using Internal.TypeSystem;
using Internal.ReadyToRunConstants;
using ILCompiler.DependencyAnalysisFramework;
using System.Collections.Generic;

// TODO, andrewau, other architectures
using ILCompiler.DependencyAnalysis.X64;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    // An InterpreterStub is a small trampoline that 
    // 1.) Copy the pointer to the pointer to the InterpreterMethodInfo into RAX
    // 2.) Jump to the InterpreterRoutine
    //
    public class InterpreterStub : ObjectNode, ISymbolDefinitionNode
    {
        public InterpreterImport _interpreterImport;
        public bool _virtual;

        public InterpreterStub(InterpreterImport interpreterImport, bool isVirtual)
        {
            _interpreterImport = interpreterImport;
            _virtual = isVirtual;
        }

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return _interpreterImport._id - ((InterpreterStub)(other))._interpreterImport._id;
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            X64Emitter x64Emitter = new X64Emitter(factory, relocsOnly);
            x64Emitter.Builder.AddSymbol(this);
            x64Emitter.EmitMOV(Register.RAX, _interpreterImport);
            if (_virtual)
            {
                x64Emitter.EmitJMP(factory.InterpreterVirtualRoutineImport);
            }
            else
            {
                x64Emitter.EmitJMP(factory.InterpreterRoutineImport);
            }
            return x64Emitter.Builder.ToObjectData();
        }

        public int Offset { get; set; }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
            => sb.Append(nameMangler.CompilationUnitPrefix).Append("InterpreterStub").Append(_interpreterImport._id.ToString());

        public override ObjectNodeSection GetSection(NodeFactory factory)
        {
            return ObjectNodeSection.TextSection;
        }

        public override IEnumerable<CombinedDependencyListEntry> SearchDynamicDependencies(List<DependencyNodeCore<NodeFactory>> markedNodes, int firstNode, NodeFactory context)
        {
            yield break;
        }

        protected override string GetName(NodeFactory context) => "InterpreterStub";

        public override int ClassCode => 95566084;

        public override bool IsShareable => false;

        public override bool StaticDependenciesAreComputed => true;
    }
}
