// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;

namespace ILCompiler.ObjectWriter
{
    public readonly struct WasmFunctionType(WasmValueType result, WasmValueType[] parameters) : IEquatable<WasmFunctionType>
    {
        private readonly WasmValueType _result = result;
        private readonly WasmValueType[] _parameters = parameters;

        [UnscopedRef] public ReadOnlySpan<WasmValueType> Results => (_result is WasmValueType.Invalid) ? [] : new(in _result);
        public ReadOnlySpan<WasmValueType> Parameters => _parameters;

        public override bool Equals(object obj) => obj is WasmFunctionType type && Equals(type);

        public bool Equals(WasmFunctionType other)
        {
            return Results.SequenceEqual(other.Results) && Parameters.SequenceEqual(other.Parameters);
        }

        public override int GetHashCode()
        {
            HashCode hash = default;
            hash.AddBytes(MemoryMarshal.AsBytes(Results));
            hash.AddBytes(MemoryMarshal.AsBytes(Parameters));
            return hash.ToHashCode();
        }

        public static bool operator ==(WasmFunctionType left, WasmFunctionType right) => left.Equals(right);
        public static bool operator !=(WasmFunctionType left, WasmFunctionType right) => !(left == right);
    }

    public enum WasmValueType : byte
    {
        Invalid = 0,
        I32 = 0x7F,
        I64 = 0x7E,
        F32 = 0x7D,
        F64 = 0x7C
    }

    public enum WasmSectionId
    {
        Invalid = -1,
        Custom,
        Type,
        Import,
        Function,
        Table,
        Memory,
        Global,
        Export,
        Start,
        Element,
        Code,
        Data,
        DataCount
    }

    public enum WasmRelocationKind
    {
        R_WASM_FUNCTION_INDEX_LEB,
        R_WASM_TABLE_INDEX_SLEB,
        R_WASM_TABLE_INDEX_I32,
        R_WASM_MEMORY_ADDR_LEB,
        R_WASM_MEMORY_ADDR_SLEB,
        R_WASM_MEMORY_ADDR_I32,
        R_WASM_TYPE_INDEX_LEB,
        R_WASM_GLOBAL_INDEX_LEB,
        R_WASM_FUNCTION_OFFSET_I32,
        R_WASM_SECTION_OFFSET_I32,
        R_WASM_TAG_INDEX_LEB,
        R_WASM_MEMORY_ADDR_REL_SLEB,
        R_WASM_TABLE_INDEX_REL_SLEB,
        R_WASM_GLOBAL_INDEX_I32,
        R_WASM_MEMORY_ADDR_LEB64,
        R_WASM_MEMORY_ADDR_SLEB64,
        R_WASM_MEMORY_ADDR_I64,
        R_WASM_MEMORY_ADDR_REL_SLEB64,
        R_WASM_TABLE_INDEX_SLEB64,
        R_WASM_TABLE_INDEX_I64,
        R_WASM_TABLE_NUMBER_LEB,
        R_WASM_MEMORY_ADDR_TLS_SLEB,
        R_WASM_FUNCTION_OFFSET_I64,
        R_WASM_MEMORY_ADDR_LOCREL_I32,
        R_WASM_TABLE_INDEX_REL_SLEB64,
        R_WASM_MEMORY_ADDR_TLS_SLEB64,
        R_WASM_FUNCTION_INDEX_I32,
    }
}
