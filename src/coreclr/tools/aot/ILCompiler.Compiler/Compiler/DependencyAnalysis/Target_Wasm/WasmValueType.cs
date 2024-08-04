// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace ILCompiler.DependencyAnalysis.Wasm
{
    public enum WasmValueType : byte
    {
        Invalid = 0,
        I32 = 0x7F,
        I64 = 0x7E,
        F32 = 0x7D,
        F64 = 0x7C
    }
}
