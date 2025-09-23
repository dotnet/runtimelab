// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using Internal.IL.Stubs;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public partial interface IObjectDumper
    {
        // In the LLVM backend, compiled method bodies are represented by symbols of which by emission
        // time we do not yet know the size. We support dumping them by creating "relocatable" dumps.
        void DumpExternalObjectNode(NodeFactory factory, ISymbolNode node);
    }
}

namespace ILCompiler
{
    public abstract partial class ObjectDumper
    {
        void IObjectDumper.DumpExternalObjectNode(NodeFactory factory, ISymbolNode node) => DumpExternalObjectNode(factory, node);

        protected virtual void DumpExternalObjectNode(NodeFactory factory, ISymbolNode node) { }

        private sealed partial class ComposedObjectDumper : ObjectDumper
        {
            protected override void DumpExternalObjectNode(NodeFactory factory, ISymbolNode node)
            {
                foreach (var d in _dumpers)
                    d.DumpExternalObjectNode(factory, node);
            }
        }
    }

    public partial class MstatObjectDumper
    {
        private List<(MethodDesc Method, string MangledName)> _externalMethods = new();

        protected override void DumpExternalObjectNode(NodeFactory factory, ISymbolNode node)
        {
            IMethodBodyNode methodNode = (IMethodBodyNode)node; // We currently only use this for methods.
            _externalMethods.Add((methodNode.Method, methodNode.GetMangledName(factory.NameMangler)));
        }

        private void EmitRelocatableNodes(InstructionEncoder methods)
        {
            BlobBuilder relocs = new();
            foreach (var m in _externalMethods)
            {
                methods.OpCode(ILOpCode.Ldtoken);
                methods.Token(_emitter.EmitMetadataHandleForTypeSystemEntity(m.Method));
                methods.OpCode(ILOpCode.Ldc_i4);
                int sizeOffset = methods.Offset;
                methods.Token(0);
                methods.LoadConstantI4(0);
                methods.LoadConstantI4(_methodEhInfo.GetValueOrDefault(m.Method));
                int mangledNameOffset = AppendMangledName(m.MangledName);
                methods.LoadConstantI4(mangledNameOffset);

                relocs.WriteInt32(sizeOffset);
                relocs.WriteInt32(mangledNameOffset);
            }

            _emitter.AddPESection(".szreloc", relocs);
        }
    }
}
