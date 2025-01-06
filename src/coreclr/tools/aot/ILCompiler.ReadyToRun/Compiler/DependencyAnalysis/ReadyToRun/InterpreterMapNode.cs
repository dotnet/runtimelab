// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Internal.Text;
using System.Collections.Generic;
using System.Diagnostics;

namespace ILCompiler.DependencyAnalysis.ReadyToRun
{
    public class InterpreterMapNode : HeaderTableNode
    {
        public override int ClassCode => 25687179;

        private List<DelayLoadMethodImport> leftItems = new List<DelayLoadMethodImport>();
        private List<InterpreterStub> lastItems = new List<InterpreterStub>();
        private List<InterpreterImport> rightItems = new List<InterpreterImport>();

        public void AddMapping(DelayLoadMethodImport left, InterpreterImport right, InterpreterStub last)
        {
            // TODO, andrewau, this require proper locking and sorting for multithreaded compilation.
            // correctness and determinism
            leftItems.Add(left);
            rightItems.Add(right);
            lastItems.Add(last);
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append(nameMangler.CompilationUnitPrefix);
            sb.Append("__InterpreterMap"u8);
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.AddSymbol(this);
            for (int i = 0; i < leftItems.Count; i++)
            {
                // This will emit the RVAs for these 
                builder.EmitReloc(leftItems[i], RelocType.IMAGE_REL_FILE_ABSOLUTE);
                builder.EmitReloc(rightItems[i], RelocType.IMAGE_REL_FILE_ABSOLUTE);
                builder.EmitReloc(lastItems[i], RelocType.IMAGE_REL_FILE_ABSOLUTE);
            }
            return builder.ToObjectData();
        }
    }
}
