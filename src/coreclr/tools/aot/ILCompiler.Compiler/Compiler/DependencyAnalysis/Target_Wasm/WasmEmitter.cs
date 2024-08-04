// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

using ILCompiler.ObjectWriter;

namespace ILCompiler.DependencyAnalysis.Wasm
{
    public struct WasmEmitter(NodeFactory factory, bool relocsOnly)
    {
        public const uint InvalidIndex = uint.MaxValue;

        private readonly NodeFactory _factory = factory;
        private readonly bool _is64Bit = factory.Target.PointerSize == 8;

        public ObjectDataBuilder Builder = new ObjectDataBuilder(factory, relocsOnly);

        public void EmitCctorCheck(NodeFactory factory, uint nonGcStaticBaseLocal, uint cctorContextLocal)
        {
            EmitLocalTee(nonGcStaticBaseLocal);
            EmitNaturalAddConst(-NonGCStaticsNode.GetClassConstructorContextSize(factory.Target));
            EmitLocalTee(cctorContextLocal);
            EmitLoad(GetNaturalIntType());
            EmitIf();
            {
                EmitLocalGet(0); // Shadow stack.
                EmitLocalGet(cctorContextLocal);
                EmitLocalGet(nonGcStaticBaseLocal);
                EmitCall(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnNonGCStaticBase));
                EmitDrop();
            }
            EmitEnd();
        }

        public void EmitReturnAfterAlwaysThrowCall(WasmValueType callerReturnType, WasmValueType calleeReturnType, bool isEnd)
        {
            bool mismatch = callerReturnType != calleeReturnType;
            if (_factory.TargetsEmulatedEH())
            {
                if (mismatch)
                {
                    if (calleeReturnType != WasmValueType.Invalid)
                    {
                        EmitDrop();
                    }

                    switch (callerReturnType)
                    {
                        case WasmValueType.I32:
                            EmitI32Const(0);
                            break;
                        case WasmValueType.I64:
                            EmitI64Const(0);
                            break;
                        case WasmValueType.F32:
                            EmitF32Const(0);
                            break;
                        case WasmValueType.F64:
                            EmitF64Const(0);
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                }

                if (isEnd)
                {
                    EmitEnd();
                }
                else
                {
                    EmitReturn();
                }
            }
            else
            {
                // We can optimize out the unreachable in case we're 'end'ing with the correct return type.
                if (mismatch || !isEnd)
                {
                    EmitUnreachable();
                }

                if (isEnd)
                {
                    EmitEnd();
                }
            }
        }

        public void DefineLocals(ReadOnlySpan<(uint Count, WasmValueType Type)> locals = default)
        {
            uint length = 0;
            foreach ((uint count, _) in locals)
            {
                if (count != 0)
                {
                    length++;
                }
            }

            EmitULeb128(length);
            foreach ((uint count, WasmValueType type) in locals)
            {
                if (count != 0)
                {
                    EmitULeb128(count);
                    EmitULeb128((byte)type);
                }
            }
        }

        public readonly WasmValueType GetNaturalIntType() => _is64Bit ? WasmValueType.I64 : WasmValueType.I32;

        public void EmitCall(ISymbolNode target)
        {
            Builder.EmitByte(0x10);
            Builder.EmitReloc(target, RelocType.R_WASM_FUNCTION_INDEX_LEB);
        }

        public void EmitIf(WasmValueType blockType = WasmValueType.Invalid)
        {
            Builder.EmitByte(0x04);
            EmitBlockType(blockType);
        }

        public void EmitReturn() => Builder.EmitByte(0x0F);

        public void EmitUnreachable() => Builder.EmitByte(0x00);

        public void EmitEnd() => Builder.EmitByte(0x0B);

        public void EmitDrop() => Builder.EmitByte(0x1A);

        public void EmitLocalGet(uint index)
        {
            Builder.EmitByte(0x20);
            EmitULeb128(index);
        }

        public void EmitLocalTee(uint index)
        {
            Builder.EmitByte(0x22);
            EmitULeb128(index);
        }

        public void EmitLoad(WasmValueType loadType, uint offset = 0)
        {
            (uint alignment, int instr) = loadType switch
            {
                WasmValueType.I32 => (4u, 0x28),
                WasmValueType.I64 => (8u, 0x29),
                WasmValueType.F32 => (4u, 0x2A),
                WasmValueType.F64 => (8u, 0x2B),
                _ => throw new NotImplementedException()
            };
            Builder.EmitByte((byte)instr);
            EmitMemArg(alignment, offset);
        }

        public void EmitNaturalEQZ() => Builder.EmitByte(_is64Bit ? (byte)0x50 : (byte)0x45);

        public void EmitNaturalAddConst(int addend)
        {
            EmitNaturalConst(addend);
            EmitNaturalAdd();
        }

        public void EmitNaturalAdd() => Builder.EmitByte(_is64Bit ? (byte)0x7C : (byte)0x6A);

        public void EmitNaturalConst(ISymbolNode target, int delta = 0)
        {
            bool isFunc = WasmFunctionType.IsFunction(target);
            Debug.Assert(!isFunc || delta == 0);
            if (_is64Bit)
            {
                Builder.EmitByte(0x42);
                Builder.EmitReloc(target, isFunc ? RelocType.R_WASM_TABLE_INDEX_SLEB64 : RelocType.R_WASM_MEMORY_ADDR_SLEB64, delta);
            }
            else
            {
                Builder.EmitByte(0x41);
                Builder.EmitReloc(target, isFunc ? RelocType.R_WASM_TABLE_INDEX_SLEB : RelocType.R_WASM_MEMORY_ADDR_SLEB, delta);
            }
        }

        public void EmitNaturalConst(int value)
        {
            if (_is64Bit)
            {
                EmitI64Const(value);
            }
            else
            {
                EmitI32Const(value);
            }
        }

        public void EmitI32Const(int value)
        {
            Builder.EmitByte(0x41);
            EmitSLeb128(value);
        }

        public void EmitI64Const(long value)
        {
            Builder.EmitByte(0x42);
            EmitSLeb128(value);
        }

        public void EmitF32Const(float value)
        {
            Builder.EmitByte(0x43);
            Builder.EmitInt(BitConverter.SingleToInt32Bits(value));
        }

        public void EmitF64Const(double value)
        {
            Builder.EmitByte(0x44);
            Builder.EmitLong(BitConverter.DoubleToInt64Bits(value));
        }

        private void EmitMemArg(uint alignment, uint offset)
        {
            Debug.Assert(uint.IsPow2(alignment));
            EmitULeb128(uint.Log2(alignment));
            EmitULeb128(offset);
        }

        private void EmitBlockType(WasmValueType blockType)
        {
            // We only support block types "[] -> []" or "[] -> ValueType" that are encodable inline for now.
            if (blockType == WasmValueType.Invalid)
            {
                Builder.EmitByte(0x40);
            }
            else
            {
                EmitULeb128((uint)blockType);
            }
        }

        private unsafe void EmitULeb128(uint value)
        {
            Span<byte> bytes = stackalloc byte[5];
            int length = DwarfHelper.WriteULEB128(bytes, value);
            Builder.EmitBytes(bytes[..length]);
        }

        private unsafe void EmitSLeb128(long value)
        {
            Span<byte> bytes = stackalloc byte[10];
            int length = DwarfHelper.WriteSLEB128(bytes, value);
            Builder.EmitBytes(bytes[..length]);
        }
    }
}
