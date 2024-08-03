// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

using ILCompiler.DependencyAnalysis;
using ObjectData = ILCompiler.DependencyAnalysis.ObjectNode.ObjectData;
using static ILCompiler.ObjectWriter.WasmRelocationKind;

using Internal.Text;
using Internal.TypeSystem;
using System.Runtime.CompilerServices;

//
// The WASM object writer. WASM object files are valid WASM binaries with relocation sections
// and a "linking" section that describes the symbols the object file defines and references.
//
// https://webassembly.github.io/spec/core/binary/modules.html
// https://github.com/WebAssembly/tool-conventions/blob/main/Linking.md
//
namespace ILCompiler.ObjectWriter
{
    internal sealed class WasmObjectWriter(LLVMCodegenCompilation compilation)
    {
        private const int InvalidIndexInt32 = -1;
        private const uint InvalidIndex = uint.MaxValue;
        private const uint InvalidOffset = uint.MaxValue;

        private readonly LLVMCodegenCompilation _compilation = compilation;
        private readonly bool _is64Bit = compilation.NodeFactory.Target.PointerSize == 8;

        private WasmSection _codeWasmSection = new(WasmSectionId.Code);
        private WasmSection _dataWasmSection = new(WasmSectionId.Data);

        private ArrayBuilder<SymbolInfo> _symbols;
        private readonly Dictionary<ISymbolNode, int> _symbolNodeToSymbolIndexMap = [];
        private readonly Dictionary<Utf8String, int> _symbolNameToSymbolIndexMap = [];
        private ArrayBuilder<WasmFunctionType> _types;
        private readonly Dictionary<WasmFunctionType, uint> _typeToTypeIndexMap = [];
        private uint _undefinedFunctionsCount;

        private readonly Utf8StringBuilder _utf8StringBuilder = new();
        private readonly Dictionary<ObjectNodeSection, Utf8String> _nodeSectionNameMap = [];
        private uint _currentWasmSectionIndex;

        public void Emit(ObjectNodeSection section, ISymbolDefinitionNode sectionSymbol, ObjectData data)
        {
            if (section.Type == SectionType.Executable)
            {
                AddLinkingSection(ref _codeWasmSection, section, sectionSymbol, data);
            }
            else
            {
                AddLinkingSection(ref _dataWasmSection, section, sectionSymbol, data);
            }
        }

        public void WriteObject(string objectFilePath)
        {
            using Stream stream = new FileStream(objectFilePath, FileMode.Create);
            WriteWasmHeader(stream);
            WriteWasmSection(WasmSectionId.Type, stream, static (w, s) => w.WriteTypeSection(s));
            WriteWasmSection(WasmSectionId.Import, stream, static (w, s) => w.WriteImportSection(s));
            WriteWasmSection(WasmSectionId.Function, stream, static (w, s) => w.WriteFunctionSection(s));
            WriteWasmSection(WasmSectionId.DataCount, stream, static (w, s) => w.WriteDataCountSection(s));
            WriteWasmSection(WasmSectionId.Code, stream, static (w, s) => w.WriteCodeSection(s));
            WriteWasmSection(WasmSectionId.Data, stream, static (w, s) => w.WriteDataSection(s));
            WriteWasmSection(WasmSectionId.Custom, stream, static (w, s) => w.WriteLinkingSection(s));
            WriteRelocationSectionForSection(stream, _codeWasmSection);
            WriteRelocationSectionForSection(stream, _dataWasmSection);
        }

        private void AddLinkingSection(ref WasmSection wasmSection, ObjectNodeSection section, ISymbolDefinitionNode sectionSymbol, ObjectData data)
        {
            Relocation[] relocations = data.Relocs;
            foreach (ref Relocation relocation in relocations.AsSpan())
            {
                AddSymbol(relocation.Target);
            }

            ArrayBuilder<int> definedSymbolIndices = default;
            foreach (ISymbolDefinitionNode definedSymbol in data.DefinedSymbols)
            {
                int symbolIndex = AddSymbol(definedSymbol, isDefinition: true);
                definedSymbolIndices.Add(symbolIndex);

                if (_compilation.NodeFactory.GetSymbolAlternateName(definedSymbol) is string alternateName)
                {
                    symbolIndex = AddSymbol(definedSymbol, isDefinition: true, alternateName);
                    definedSymbolIndices.Add(symbolIndex);
                }
            }

            int sectionSymbolIndex = sectionSymbol is null ? InvalidIndexInt32 : _symbolNodeToSymbolIndexMap[sectionSymbol];
            LinkingSectionName name = new(section, sectionSymbolIndex);
            wasmSection.AddLinkingSection(name, data.Data, checked((uint)data.Alignment), relocations, definedSymbolIndices.ToArray());
        }

        private int AddSymbol(ISymbolNode symbol, bool isDefinition = false, string alternateName = null)
        {
            void UpdateExistingSymbol(ref int symbolIndex)
            {
                if (isDefinition)
                {
                    ref SymbolInfo symbolInfo = ref _symbols.AsSpan()[symbolIndex];
                    if (symbolInfo.IsDefined)
                    {
                        throw new InvalidOperationException($"Duplicate symbol definition: {symbolInfo.Name}");
                    }

                    // Update the representative symbol to point to the definition.
                    symbolInfo.Symbol = symbol;
                    symbolInfo.IsDefined = true;

                    if (symbolInfo.IsFunction)
                    {
                        _undefinedFunctionsCount--; // No longer an undefined function.
                    }
                }
            }

            Utf8String symbolName;
            ref int symbolIndexViaNode = ref Unsafe.NullRef<int>();
            if (alternateName == null)
            {
                symbolIndexViaNode = ref CollectionsMarshal.GetValueRefOrAddDefault(_symbolNodeToSymbolIndexMap, symbol, out bool symbolExistsViaNode);
                if (symbolExistsViaNode)
                {
                    UpdateExistingSymbol(ref symbolIndexViaNode);
                    return symbolIndexViaNode;
                }

                Utf8StringBuilder utf8StringBuilder = _utf8StringBuilder.Clear();
                symbol.AppendMangledName(_compilation.NameMangler, utf8StringBuilder);
                symbolName = utf8StringBuilder.ToUtf8String();
            }
            else
            {
                symbolName = alternateName;
            }

            ref int symbolIndexViaName = ref CollectionsMarshal.GetValueRefOrAddDefault(_symbolNameToSymbolIndexMap, symbolName, out bool symbolExists);
            if (!symbolExists)
            {
                SymbolInfo symbolInfo = new(symbolName) { Symbol = symbol, IsDefined = isDefinition };
                if (symbolInfo.IsFunction)
                {
                    if (!isDefinition)
                    {
                        _undefinedFunctionsCount++;
                    }

                    WasmFunctionType type = symbolInfo.GetFunctionType(_compilation);
                    ref uint typeIndex = ref CollectionsMarshal.GetValueRefOrAddDefault(_typeToTypeIndexMap, type, out bool exists);
                    if (!exists)
                    {
                        typeIndex = (uint)_types.Count;
                        _types.Add(type);
                    }
                    symbolInfo.FunctionTypeIndex = typeIndex;
                }

                symbolIndexViaName = _symbols.Count;
                _symbols.Add(symbolInfo);
            }
            else
            {
                UpdateExistingSymbol(ref symbolIndexViaName);
            }

            if (alternateName == null)
            {
                symbolIndexViaNode = symbolIndexViaName;
            }
            return symbolIndexViaName;
        }

        private static void WriteWasmHeader(Stream stream)
        {
            stream.Write("\0asm"u8); // The magic number
            stream.Write([0x01, 0x00, 0x00, 0x00]); // The version
        }

        private uint WriteTypeSection(Stream stream)
        {
            uint size = WriteULeb128(stream, (uint)_types.Count);
            foreach (ref WasmFunctionType type in _types.AsSpan())
            {
                size += WriteByte(stream, 0x60);
                size += WriteByteVector(stream, MemoryMarshal.AsBytes(type.Parameters));
                size += WriteByteVector(stream, MemoryMarshal.AsBytes(type.Results));
            }
            return size;
        }

        private uint WriteImportSection(Stream stream)
        {
            ReadOnlySpan<byte> importModuleName = "env"u8;

            // Import memory so that the data segments (and code) can reference it. // TODO-LLVM: 64 bit support.
            uint size = WriteULeb128(stream, 1 + _undefinedFunctionsCount);
            size += WriteByteVector(stream, importModuleName);
            size += WriteByteVector(stream, "__linear_memory"u8);
            size += WriteBytes(stream, [0x02, 0x00, 0x01]); // memtype: {min: 0x01, max: infinite}.

            uint importedFunctionIndex = 0;
            foreach (ref SymbolInfo symbol in _symbols.AsSpan())
            {
                if (!symbol.IsDefined && symbol.IsFunction)
                {
                    size += WriteByteVector(stream, importModuleName);
                    size += WriteByteVector(stream, symbol.Name.AsSpan());
                    size += WriteByte(stream, 0x00);
                    size += WriteULeb128(stream, symbol.FunctionTypeIndex);

                    if (IsWritingPhase(stream))
                    {
                        symbol.Index = importedFunctionIndex++;
                    }
                }
            }
            Debug.Assert(!IsWritingPhase(stream) || importedFunctionIndex == _undefinedFunctionsCount);
            return size;
        }

        private uint WriteFunctionSection(Stream stream)
        {
            uint size = 0;
            uint definedFunctionIndex = _undefinedFunctionsCount;
            uint definedFunctionsCount = (uint)_codeWasmSection.LinkingSections.Length;
            if (definedFunctionsCount != 0)
            {
                size += WriteULeb128(stream, definedFunctionsCount);
                foreach (ref LinkingSection definedFunction in _codeWasmSection.LinkingSections)
                {
                    Debug.Assert(definedFunction.Chunks.Length == 1);

                    uint wasmFunctionTypeIndex = InvalidIndex;
                    foreach (int definedSymbolIndex in definedFunction.Chunks[0].DefinedSymbolIndices)
                    {
                        ref SymbolInfo symbol = ref _symbols.AsSpan()[definedSymbolIndex];
                        Debug.Assert(symbol.DefinitionOffset == 0);

                        if (wasmFunctionTypeIndex == InvalidIndex)
                        {
                            wasmFunctionTypeIndex = symbol.FunctionTypeIndex;
                            size += WriteULeb128(stream, wasmFunctionTypeIndex);
                        }
                        else
                        {
                            Debug.Assert(wasmFunctionTypeIndex == symbol.FunctionTypeIndex);
                        }

                        if (IsWritingPhase(stream))
                        {
                            symbol.Index = definedFunctionIndex;
                        }
                    }

                    Debug.Assert(wasmFunctionTypeIndex != InvalidIndex);
                    definedFunctionIndex++;
                }
                Debug.Assert(definedFunctionIndex == definedFunctionsCount);
            }
            return size;
        }

        private uint WriteDataCountSection(Stream stream)
        {
            uint size = 0;
            uint dataSegmentCount = (uint)_dataWasmSection.LinkingSections.Length;
            if (dataSegmentCount != 0)
            {
                size += WriteULeb128(stream, dataSegmentCount);
            }
            return size;
        }

        private uint WriteCodeSection(Stream stream)
        {
            uint definedFunctionsCount = (uint)_codeWasmSection.LinkingSections.Length;
            if (definedFunctionsCount == 0)
            {
                return 0;
            }

            uint size = WriteULeb128(stream, definedFunctionsCount);
            foreach (ref LinkingSection definedFunction in _codeWasmSection.LinkingSections)
            {
                ref LinkingSectionChunk chunk = ref definedFunction.Chunks[0];
                if (IsWritingPhase(stream))
                {
                    chunk.WasmSectionRelativeOffset = size;
                }

                size += WriteByteVector(stream, chunk.Content);
            }
            if (IsWritingPhase(stream))
            {
                _codeWasmSection.Index = _currentWasmSectionIndex;
            }
            return size;
        }

        private uint WriteDataSection(Stream stream)
        {
            Span<LinkingSection> linkingSections = _dataWasmSection.LinkingSections;
            uint dataSegmentCount = (uint)linkingSections.Length;
            if (dataSegmentCount == 0)
            {
                return 0;
            }

            // The data section is a vector of data segments. Each segment represents one 'linking' section.
            uint size = WriteULeb128(stream, dataSegmentCount);
            for (int linkingSectionIndex = 0; linkingSectionIndex < linkingSections.Length; linkingSectionIndex++)
            {
                // Active segment for the '0'th memory.
                size += WriteByte(stream, 0);
                // This is the place for the constant expression denoting the offset at which the content
                // from this segment should be copied into linear memory. Since we are producing an object
                // file, where it is by definition unknown, we just fill the field with a dummy "i32.0; end".
                size += WriteBytes(stream, [0x41, 0x00, 0x0B]); // TODO-LLVM: does 64 bit need i64.0?

                uint segmentSize = 0;
                ref LinkingSection linkingSection = ref linkingSections[linkingSectionIndex];
                foreach (ref LinkingSectionChunk data in linkingSection.Chunks)
                {
                    segmentSize += AlignUpAddend(segmentSize, data.Alignment);
                    segmentSize += (uint)data.Content.Length;
                }
                size += WriteULeb128(stream, segmentSize);

                if (IsWritingPhase(stream))
                {
                    uint offset = 0;
                    foreach (ref LinkingSectionChunk chunk in linkingSection.Chunks)
                    {
                        // Align this chunk.
                        offset += WriteZeroes(stream, AlignUpAddend(offset, chunk.Alignment));

                        foreach (int definedSymbolIndex in chunk.DefinedSymbolIndices)
                        {
                            ref SymbolInfo definedSymbol = ref _symbols.AsSpan()[definedSymbolIndex];
                            definedSymbol.Index = (uint)linkingSectionIndex;
                            definedSymbol.LinkingSectionRelativeOffset = offset + definedSymbol.DefinitionOffset;
                        }
                        chunk.WasmSectionRelativeOffset = size + offset;

                        offset += WriteBytes(stream, chunk.Content);
                    }
                    Debug.Assert(offset == segmentSize);
                }
                size += segmentSize;
            }
            if (IsWritingPhase(stream))
            {
                _dataWasmSection.Index = _currentWasmSectionIndex;
            }
            return size;
        }

        private uint WriteLinkingSection(Stream stream)
        {
            const uint Version = 2;
            const byte WASM_SEGMENT_INFO = 5;
            const byte WASM_SYMBOL_TABLE = 8;

            uint size = WriteByteVector(stream, "linking"u8);
            size += WriteULeb128(stream, Version);
            size += WriteSection(WASM_SEGMENT_INFO, stream, static (w, s) => w.WriteSegmentInfoSubSection(s));
            size += WriteSection(WASM_SYMBOL_TABLE, stream, static (w, s) => w.WriteSymbolTableSubSection(s));
            return size;
        }

        private uint WriteSegmentInfoSubSection(Stream stream)
        {
            Utf8String GetNodeSectionName(ObjectNodeSection section)
            {
                ref Utf8String utf8Name = ref CollectionsMarshal.GetValueRefOrAddDefault(_nodeSectionNameMap, section, out bool exists);
                if (!exists)
                {
                    string name = section.Name; // Same logic as "ElfObjectWriter.CreateSection".
                    utf8Name = name switch
                    {
                        "rdata" => ".rodata",
                        _ when name.StartsWith('_') || name.StartsWith('.') => name,
                        _ => "." + name
                    };
                }

                return utf8Name;
            }

            uint size = 0;
            Span<LinkingSection> dataSegments = _dataWasmSection.LinkingSections;
            uint dataSegmentCount = (uint)dataSegments.Length;
            if (dataSegmentCount != 0)
            {
                size += WriteULeb128(stream, dataSegmentCount);
                foreach (ref LinkingSection segment in dataSegments)
                {
                    uint alignment = 1;
                    foreach (ref LinkingSectionChunk data in segment.Chunks)
                    {
                        alignment = Math.Max(alignment, data.Alignment);
                    }

                    Utf8String segmentBaseName = GetNodeSectionName(segment.Name.Section);
                    int segmentSymbolIndex = segment.Name.SectionSymbolIndex;
                    Utf8String segmentSymbolName = segmentSymbolIndex != InvalidIndexInt32
                        ? _symbols[segmentSymbolIndex].Name : default;
                    uint segmentNameSize = (uint)segmentBaseName.Length;
                    if (segmentSymbolIndex != InvalidIndexInt32)
                    {
                        segmentNameSize += (uint)segmentSymbolName.Length + 1;
                    }

                    size += WriteULeb128(stream, segmentNameSize);
                    size += WriteBytes(stream, segmentBaseName.AsSpan());
                    if (segmentSymbolIndex != InvalidIndexInt32)
                    {
                        size += WriteByte(stream, (byte)'.');
                        size += WriteBytes(stream, segmentSymbolName.AsSpan());
                    }
                    size += WriteULeb128(stream, uint.Log2(alignment));
                    size += WriteULeb128(stream, 0); // No flags for now.
                }
            }
            return size;
        }

        private uint WriteSymbolTableSubSection(Stream stream)
        {
            const byte SYMTAB_FUNCTION = 0;
            const byte SYMTAB_DATA = 1;
            const uint WASM_SYM_UNDEFINED = 0x10;
            const uint WASM_SYM_NO_STRIP = 0x80;

            uint symbolCount = (uint)_symbols.Count;
            if (symbolCount == 0)
            {
                return 0;
            }

            uint size = WriteULeb128(stream, symbolCount);
            foreach (ref SymbolInfo symbol in _symbols.AsSpan())
            {
                byte kind = symbol.IsData ? SYMTAB_DATA : SYMTAB_FUNCTION;
                size += WriteByte(stream, kind);

                bool isDefined = symbol.IsDefined;
                uint flags = 0;
                if (!isDefined)
                {
                    flags |= WASM_SYM_UNDEFINED;
                }
                if (symbol.MustBeArtificiallyKeptAlive)
                {
                    flags |= WASM_SYM_NO_STRIP;
                }
                size += WriteULeb128(stream, flags);

                if (kind == SYMTAB_DATA)
                {
                    size += WriteByteVector(stream, symbol.Name.AsSpan());
                    if (isDefined)
                    {
                        size += WriteULeb128(stream, symbol.Index);
                        size += WriteULeb128(stream, symbol.LinkingSectionRelativeOffset);
                        size += WriteULeb128(stream, 0); // Size. Always zero for now.
                    }
                }
                else
                {
                    size += WriteULeb128(stream, symbol.Index);
                    if (isDefined) // Undefined symbols take their name from the import.
                    {
                        size += WriteByteVector(stream, symbol.Name.AsSpan());
                    }
                }
            }
            return size;
        }

        private unsafe void WriteRelocationSectionForSection(Stream stream, WasmSection section)
        {
            uint relocCount = section.RelocationCount;
            if (relocCount == 0)
            {
                return;
            }

            WriteWasmSection(WasmSectionId.Custom, stream, (w, stream) =>
            {
                ReadOnlySpan<byte> name = section.Id switch
                {
                    WasmSectionId.Code => "reloc.CODE"u8,
                    WasmSectionId.Data => "reloc.DATA"u8,
                    _ => throw new NotImplementedException()
                };

                uint size = WriteByteVector(stream, name);
                size += WriteULeb128(stream, section.Index);
                size += WriteULeb128(stream, relocCount);
                foreach (ref LinkingSection linkingSection in section.LinkingSections)
                {
                    foreach (ref LinkingSectionChunk data in linkingSection.Chunks)
                    {
                        foreach (ref Relocation relocation in data.Relocations.AsSpan())
                        {
                            int symbolIndex = _symbolNodeToSymbolIndexMap[relocation.Target];
                            WasmRelocationKind kind = GetWasmRelocationKind(in relocation);

                            size += WriteByte(stream, checked((byte)kind));
                            size += WriteULeb128(stream, data.WasmSectionRelativeOffset + checked((uint)relocation.Offset));
                            if (kind == R_WASM_TYPE_INDEX_LEB)
                            {
                                size += WriteULeb128(stream, _symbols[symbolIndex].Index);
                            }
                            else
                            {
                                size += WriteULeb128(stream, (uint)symbolIndex);
                            }

                            long addend = relocation.Target.Offset;
                            fixed (void* location = &data.Content[relocation.Offset])
                            {
                                addend += Relocation.ReadValue(relocation.RelocType, location);
                            }
                            if (WasmRelocKindHasAddend(kind))
                            {
                                size += WriteSLeb128(stream, addend);
                            }
                            else
                            {
                                Debug.Assert(addend == 0);
                            }
                        }
                    }
                }
                return size;
            });
        }

        private void WriteWasmSection(WasmSectionId id, Stream stream, Func<WasmObjectWriter, Stream, uint> write)
        {
            if (WriteSection((byte)id, stream, write) != 0)
            {
                _currentWasmSectionIndex++;
            }
        }

        // WASM sections are prefixed by their sizes (in bytes). Due to the use of variable-length encodings, it
        // is not possible to calculate this size without actually encoding the section in full. This can be solved
        // in two ways:
        // 1. Write each section into an intermediate buffer, obtain the size, and then copy that buffer to
        //    the output stream.
        // 2. Do the emission in two passes: first measure, then write out.
        //
        // We take the second approach due its lower memory overhead (recall that we are already holding all of
        // the data in memory, in the byte arrays of linking section chunks).
        //
        private uint WriteSection(byte prefix, Stream stream, Func<WasmObjectWriter, Stream, uint> write)
        {
            uint size = 0;
            uint dataSize = write(this, null);
            if (dataSize != 0)
            {
                size += WriteByte(stream, prefix);
                size += WriteULeb128(stream, dataSize);
                size += write(this, stream);
            }
            return size;
        }

        private static uint WriteULeb128(Stream stream, ulong value, int pad = 0)
        {
            uint count = 0;
            do
            {
                byte @byte = (byte)(value & 0x7f);
                value >>= 7;
                count++;
                if (value != 0 || count < pad)
                {
                    @byte |= 0x80; // Mark this byte to show that more bytes will follow.
                }
                stream?.WriteByte(@byte);
            } while (value != 0);

            // Pad with 0x80 and emit a null byte at the end.
            if (count < pad)
            {
                for (; count < pad - 1; count++)
                {
                    stream?.WriteByte(0x80);
                }
                stream?.WriteByte(0);
                count++;
            }
            return count;
        }

        private static uint WriteSLeb128(Stream stream, long value)
        {
            bool more = true;
            uint count = 0;
            do
            {
                byte @byte = (byte)(value & 0x7f);
                value >>= 7;
                count++;
                bool isSignBitSet = (@byte & 0x40) != 0;
                if ((value == 0 && !isSignBitSet) || (value == -1 && isSignBitSet))
                {
                    more = false;
                }
                else
                {
                    @byte |= 0x80;
                }
                stream?.WriteByte(@byte);
            } while (more);

            return count;
        }

        private static uint WriteByteVector(Stream stream, ReadOnlySpan<byte> bytes)
        {
            uint size = WriteULeb128(stream, (uint)bytes.Length);
            size += WriteBytes(stream, bytes);
            return size;
        }

        private static uint WriteBytes(Stream stream, ReadOnlySpan<byte> bytes)
        {
            stream?.Write(bytes);
            return (uint)bytes.Length;
        }

        private static uint WriteZeroes(Stream stream, uint count)
        {
            for (uint i = 0; i < count; i++)
            {
                WriteByte(stream, 0);
            }
            return count;
        }

        private static uint WriteByte(Stream stream, byte value)
        {
            stream?.WriteByte(value);
            return 1;
        }

        private static bool IsWritingPhase(Stream stream) => stream != null;

        private WasmRelocationKind GetWasmRelocationKind(ref readonly Relocation relocation)
        {
            switch (relocation.RelocType)
            {
                case RelocType.IMAGE_REL_BASED_HIGHLOW:
                    if (SymbolInfo.IsFunctionSymbol(relocation.Target))
                    {
                        return _is64Bit ? R_WASM_TABLE_INDEX_I64 : R_WASM_TABLE_INDEX_I32;
                    }

                    Debug.Assert(SymbolInfo.IsDataSymbol(relocation.Target));
                    return _is64Bit ? R_WASM_MEMORY_ADDR_I64 : R_WASM_MEMORY_ADDR_I32;

                case RelocType.R_WASM_FUNCTION_OFFSET_I32:
                    return R_WASM_FUNCTION_OFFSET_I32;

                default:
                    throw new NotSupportedException($"Unsupported relocation type: {relocation.RelocType}");
            }
        }

        private static bool WasmRelocKindHasAddend(WasmRelocationKind kind)
        {
            switch (kind)
            {
                case R_WASM_MEMORY_ADDR_LEB:
                case R_WASM_MEMORY_ADDR_LEB64:
                case R_WASM_MEMORY_ADDR_SLEB:
                case R_WASM_MEMORY_ADDR_SLEB64:
                case R_WASM_MEMORY_ADDR_REL_SLEB:
                case R_WASM_MEMORY_ADDR_REL_SLEB64:
                case R_WASM_MEMORY_ADDR_I32:
                case R_WASM_MEMORY_ADDR_I64:
                case R_WASM_MEMORY_ADDR_TLS_SLEB:
                case R_WASM_MEMORY_ADDR_TLS_SLEB64:
                case R_WASM_FUNCTION_OFFSET_I32:
                case R_WASM_FUNCTION_OFFSET_I64:
                case R_WASM_SECTION_OFFSET_I32:
                case R_WASM_MEMORY_ADDR_LOCREL_I32:
                    return true;
                default:
                    return false;
            }
        }

        private static uint AlignUpAddend(uint value, uint alignment)
        {
            Debug.Assert(uint.IsPow2(alignment));
            return (value + (alignment - 1)) & ~(alignment - 1) - value;
        }

        private struct SymbolInfo(Utf8String name)
        {
            public readonly Utf8String Name = name;
            public ISymbolNode Symbol;
            public bool IsDefined;
            public uint Index; // Wasm entity index or data segment index.
            public uint FunctionTypeIndex; // Only used for code symbols.
            public uint LinkingSectionRelativeOffset; // Only used for data symbols.

            // The modules section is referenced through the special __start/__stop symbols.
            // symbols, which don't cause the linker to consider it alive by default.
            public readonly bool MustBeArtificiallyKeptAlive => Symbol is ModulesSectionNode;
            public readonly uint DefinitionOffset => checked((uint)((ISymbolDefinitionNode)Symbol).Offset);

            public readonly bool IsFunction => IsFunctionSymbol(Symbol);
            public readonly bool IsData => IsDataSymbol(Symbol);

            public readonly WasmFunctionType GetFunctionType(LLVMCodegenCompilation compilation)
            {
                Debug.Assert(IsFunction);
                if (Symbol is ExternSymbolNode)
                {
                    // We assume extenal symbol nodes are functions. This is rather fragile, but handling this precisely
                    // would require modifying producers of these nodes to provide more information (namely, the signature
                    // for function symbols).
                    // TODO-LLVM-Bug: depending on the order symbols are encountered, this hack could lead to problems.
                    // E. g. a "naked" RhpNewFast, then a "properly typed" RhpNewFast runtime import. However, the linker
                    // can tolerate mismatches, so as long as the problematic functions aren't called directly...
                    return new WasmFunctionType(WasmValueType.Invalid, []);
                }

                MethodDesc method = ((IMethodNode)Symbol).Method;
                return compilation.GetWasmFunctionTypeForMethod(method.Signature, method.RequiresInstArg());
            }

            public static bool IsFunctionSymbol(ISymbolNode symbol) => symbol is ExternSymbolNode or IMethodNode { Offset: 0 };
            public static bool IsDataSymbol(ISymbolNode symbol) => !IsFunctionSymbol(symbol);
        }

        private struct WasmSection(WasmSectionId id)
        {
            private readonly Dictionary<LinkingSectionName, int> _nameToLinkingSectionIndexMap = [];
            private ArrayBuilder<LinkingSection> _linkingSections;

            public WasmSectionId Id { get; } = id;
            public uint RelocationCount { get; private set; }
            public uint Index { get; set; } = InvalidIndex;
            public readonly Span<LinkingSection> LinkingSections => _linkingSections.AsSpan();

            public void AddLinkingSection(LinkingSectionName name, byte[] content, uint alignment, Relocation[] relocations, int[] definedSymbolIndices)
            {
                RelocationCount += (uint)relocations.Length;

                int nextIndex = _linkingSections.Count;
                int index = nextIndex;
                if (Id == WasmSectionId.Data) // Only the data section supports multiple chunks in a given linking section.
                {
                    ref int indexViaMap = ref CollectionsMarshal.GetValueRefOrAddDefault(_nameToLinkingSectionIndexMap, name, out bool exists);
                    if (exists)
                    {
                        index = indexViaMap;
                    }
                    else
                    {
                        indexViaMap = index;
                    }
                }

                if (index == nextIndex)
                {
                    _linkingSections.Add(new LinkingSection(name));
                }

                LinkingSections[index].AddChunk(new LinkingSectionChunk()
                {
                    Content = content,
                    Alignment = alignment,
                    Relocations = relocations,
                    DefinedSymbolIndices = definedSymbolIndices
                });
            }
        }

        private readonly struct LinkingSectionName(ObjectNodeSection section, int sectionSymbolIndex) : IEquatable<LinkingSectionName>
        {
            public readonly ObjectNodeSection Section = section;
            public readonly int SectionSymbolIndex = sectionSymbolIndex;

            public override bool Equals(object obj) => obj is LinkingSectionName name && Equals(name);
            public bool Equals(LinkingSectionName other) => Section == other.Section && SectionSymbolIndex == other.SectionSymbolIndex;
            public override int GetHashCode() => HashCode.Combine(Section, SectionSymbolIndex);
        }

        // The "linking section" corresponds to the concept of a section in static linking. For data, each linking
        // section gets its own data segment, and for code - a function. It is therefore impossible to have multiple
        // functions in the same section, reflecting the underlying limitations of WASM's static linking.
        private struct LinkingSection(LinkingSectionName name)
        {
            private ArrayBuilder<LinkingSectionChunk> _chunks;

            public readonly LinkingSectionName Name = name;
            public readonly Span<LinkingSectionChunk> Chunks => _chunks.AsSpan();

            public void AddChunk(LinkingSectionChunk chunk) => _chunks.Add(chunk);
        }

        private struct LinkingSectionChunk()
        {
            public byte[] Content;
            public uint Alignment;
            public Relocation[] Relocations;
            public int[] DefinedSymbolIndices;

            public uint WasmSectionRelativeOffset = InvalidOffset;
        }
    }
}
