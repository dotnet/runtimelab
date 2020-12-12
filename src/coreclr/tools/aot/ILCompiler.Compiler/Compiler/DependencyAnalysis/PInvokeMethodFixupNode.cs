// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;

using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Represents a single PInvoke MethodFixupCell as defined in the core library.
    /// </summary>
    public class PInvokeMethodFixupNode : ObjectNode, ISymbolDefinitionNode
    {
        private readonly PInvokeModuleData _moduleData;
        private readonly string _entryPointName;
        private readonly CharSet _charSetMangling;

        public PInvokeMethodFixupNode(PInvokeModuleData moduleData, string entryPointName, CharSet charSetMangling)
        {
            _moduleData = moduleData;
            _entryPointName = entryPointName;
            _charSetMangling = charSetMangling;
        }

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("__pinvoke_");
            _moduleData.AppendMangledName(nameMangler, sb);
            sb.Append("__");
            sb.Append(_entryPointName);
            if(_charSetMangling != default)
            {
                sb.Append("__");
                sb.Append(_charSetMangling.ToString());
            }
        }
        public int Offset => 0;
        public override bool IsShareable => true;

        protected override string GetName(NodeFactory factory) => this.GetMangledName(factory.NameMangler);

        public override ObjectNodeSection Section => ObjectNodeSection.DataSection;

        public override bool StaticDependenciesAreComputed => true;

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly = false)
        {
            ObjectDataBuilder builder = new ObjectDataBuilder(factory, relocsOnly);
            builder.RequireInitialPointerAlignment();

            builder.AddSymbol(this);

            //
            // Emit a MethodFixupCell struct
            //

            // Address (to be fixed up at runtime)
            builder.EmitZeroPointer();

            // Entry point name
            if (factory.Target.IsWindows && _entryPointName.StartsWith("#", StringComparison.OrdinalIgnoreCase))
            {
                // Windows-specific ordinal import
                // CLR-compatible behavior: Strings that can't be parsed as a signed integer are treated as zero.
                int entrypointOrdinal;
                if (!int.TryParse(_entryPointName.Substring(1), out entrypointOrdinal))
                    entrypointOrdinal = 0;

                // CLR-compatible behavior: Ordinal imports are 16-bit on Windows. Discard rest of the bits.
                builder.EmitNaturalInt((ushort)entrypointOrdinal);
            }
            else
            {
                // Import by name
                builder.EmitPointerReloc(factory.ConstantUtf8String(_entryPointName));
            }

            // Module fixup cell
            builder.EmitPointerReloc(factory.PInvokeModuleFixup(_moduleData));

            builder.EmitInt((int)_charSetMangling);

            return builder.ToObjectData();
        }

        public override int ClassCode => -1592006940;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            var charSetCompare = _charSetMangling.CompareTo(((PInvokeMethodFixupNode)other)._charSetMangling);
            if (charSetCompare != 0)
                return charSetCompare;

            var moduleCompare = _moduleData.CompareTo(((PInvokeMethodFixupNode)other)._moduleData, comparer);
            if (moduleCompare != 0)
                return moduleCompare;

            return string.Compare(_entryPointName, ((PInvokeMethodFixupNode)other)._entryPointName);
        }
    }
}
