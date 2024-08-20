// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;

namespace ILCompiler.DependencyAnalysis.Wasm
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

        public static bool IsFunction(ISymbolNode symbol) => symbol is ExternSymbolNode or IWasmFunctionNode or IMethodNode { Offset: 0 };

        public static bool operator ==(WasmFunctionType left, WasmFunctionType right) => left.Equals(right);
        public static bool operator !=(WasmFunctionType left, WasmFunctionType right) => !(left == right);
    }
}
