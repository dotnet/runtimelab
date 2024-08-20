// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using ObjectData = ILCompiler.DependencyAnalysis.ObjectNode.ObjectData;

namespace ILCompiler.ObjectWriter
{
    internal partial class WasmObjectWriter
    {
        public static void EmitObject(string objectFilePath, IEnumerable<DependencyNode> nodes, LLVMCodegenCompilation compilation, IObjectDumper dumper)
        {
            // External accessors must be kept separate from the rest of the code to keep the linker's
            // logic that allows signature mismatches for functions that are not called directly working.
            WasmObjectWriter externalObjectWriter = new WasmObjectWriter(compilation);
            WasmObjectWriter mainObjectWriter = new WasmObjectWriter(compilation);
            NodeFactory factory = compilation.NodeFactory;

            foreach (DependencyNode depNode in nodes)
            {
                ObjectNode node = depNode as ObjectNode;
                if (node == null)
                    continue;

                if (node.ShouldSkipEmittingObjectNode(factory))
                    continue;

                ObjectData nodeContents = node.GetData(factory);
                dumper?.DumpObjectNode(factory, node, nodeContents);

                ISymbolDefinitionNode sectionSymbol = null;
                ObjectNodeSection section = node.GetSection(factory);
                if (section.IsStandardSection && node is ISymbolDefinitionNode definingSymbol)
                {
                    // We **could** emit everything into one huge section, which is also how other targets do it.
                    // However, that would hinder linker GC and diagnosability. We therefore choose to split
                    // the data up into sections, one for each object node. Note that this choice only exists
                    // for data sections. We do not have control over how code is treated by the linker - it is
                    // always processed on a function granularity.
                    sectionSymbol = definingSymbol;
                }

                WasmObjectWriter writer;
                if (node is ExternMethodAccessorNode accessor)
                {
                    accessor.EmitWarnings(compilation);
                    writer = externalObjectWriter;
                }
                else
                {
                    writer = mainObjectWriter;
                }

                writer.Emit(section, sectionSymbol, nodeContents);
            }

            LLVMCompilationResults compilationResults = compilation.GetCompilationResults();
            mainObjectWriter.WriteObject(objectFilePath);
            compilationResults.Add(objectFilePath);

            string externalObjectPath = Path.ChangeExtension(objectFilePath, "external.o");
            externalObjectWriter.WriteObject(externalObjectPath);
            compilationResults.Add(externalObjectPath);

            compilationResults.SerializeToFile(Path.ChangeExtension(objectFilePath, "results.txt"));
        }
    }
}
