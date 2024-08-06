// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace ILCompiler.ObjectWriter
{
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
