// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis.X64;
using ILCompiler.DependencyAnalysis.X86;
using ILCompiler.DependencyAnalysis.ARM;
using ILCompiler.DependencyAnalysis.ARM64;
using ILCompiler.DependencyAnalysis.LoongArch64;
using ILCompiler.DependencyAnalysis.Wasm;

using Internal.IL.Stubs;
using Internal.TypeSystem;
using Internal.JitInterface;
using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    internal sealed class ExternMethodAccessorNode : AssemblyStubNode
    {
        private readonly ExternSymbolKey _externSymbolKey;
        private TargetAbiType[] _signature;
        private object _methods;

        public ExternMethodAccessorNode(ExternSymbolKey externSymbolKey)
        {
            _externSymbolKey = externSymbolKey;
            // Must also be a valid Javascript identifier
            QualifiedName = externSymbolKey.WasmImport
                ? externSymbolKey.ExternModuleName + "_" + externSymbolKey.ExternMethodName.ToString()
                : externSymbolKey.ExternMethodName.ToString();
        }

        public string QualifiedName { get; }

        public ExternSymbolKey ExternSymbolKey => _externSymbolKey;

        public ref TargetAbiType[] Signature => ref _signature;

        public void AddMethod(MethodDesc method)
        {
            method = method is PInvokeTargetNativeMethod nativeMethod ? nativeMethod.Target : method;
            switch (_methods)
            {
                case null:
                    _methods = method;
                    break;
                case MethodDesc oneMethod:
                    if (oneMethod != method)
                    {
                        _methods = new HashSet<MethodDesc>() { oneMethod, method };
                    }
                    break;
                default:
                    ((HashSet<MethodDesc>)_methods).Add(method);
                    break;
            }
        }

        public IEnumerable<MethodDesc> EnumerateMethods()
        {
            switch (_methods)
            {
                case null:
                    return Array.Empty<MethodDesc>();
                case MethodDesc oneMethod:
                    return new MethodDesc[] { oneMethod };
                default:
                    return (HashSet<MethodDesc>)_methods;
            }
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("get.");
            sb.Append(QualifiedName);
        }

        public override int ClassCode => 935251149;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return ExternSymbolKey.CompareTo(((ExternMethodAccessorNode)other).ExternSymbolKey);
        }

        protected override void EmitCode(NodeFactory factory, ref X64Emitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref X86Emitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref ARMEmitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref ARM64Emitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref LoongArch64Emitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref WasmEmitter instructionEncoder, bool relocsOnly) { }

        protected override string GetName(NodeFactory context) => $"ExternMethodAccessor {ExternSymbolKey.ExternMethodName}";
    }

    // Wasm imports are different if any of module name, function name, ABI signature are different.
    // Direct externs are different if the extern function name is different.
    internal sealed class ExternSymbolKey  : IEquatable<ExternSymbolKey>
    {
        private readonly TargetAbiType[] _abiSignature;

        public ExternSymbolKey(string externModuleName, string externMethodName, bool wasmImport,
            ReadOnlySpan<TargetAbiType> abiSignature)
        {
            _abiSignature = abiSignature.ToArray();

            ExternModuleName = externModuleName;
            ExternMethodName = externMethodName;
            WasmImport = wasmImport;
        }

        public string ExternModuleName { get; }
        public readonly Utf8String ExternMethodName;
        public bool WasmImport { get; }

        public override bool Equals(object obj) => obj is ExternSymbolKey wasmImportKey && Equals(wasmImportKey);

        // Only compare the module name signature for Wasm Imports.
        public bool Equals(ExternSymbolKey other) => ExternMethodName.Equals(other.ExternMethodName)
                                                     && WasmImport == other.WasmImport
                                                     && (!WasmImport || (ExternModuleName == other.ExternModuleName && _abiSignature.AsSpan().SequenceEqual(other._abiSignature)));

        public override int GetHashCode()
        {
            return WasmImport
                ? HashCode.Combine(ExternModuleName, ExternMethodName, WasmImport, _abiSignature)
                : HashCode.Combine(ExternMethodName, WasmImport);
        }

        public int CompareTo(ExternSymbolKey other)
        {
            int result = ExternMethodName.CompareTo(other.ExternMethodName);
            if (result != 0)
            {
                return result;
            }

            result = WasmImport.CompareTo(other.WasmImport);
            if (result != 0)
            {
                return result;
            }

            if (!WasmImport)
            {
                return 0;
            }

            result = ExternModuleName.CompareTo(other.ExternModuleName);
            if (result != 0)
            {
                return result;
            }

            return CompareAbiSignatures(_abiSignature, other._abiSignature);
        }

        private static int CompareAbiSignatures(TargetAbiType[] abiSignature, TargetAbiType[] otherAbiSignature)
        {
            if (abiSignature.Length != otherAbiSignature.Length)
            {
                return abiSignature.Length < otherAbiSignature.Length ? 1 : -1;
            }

            for (int i = 0; i < abiSignature.Length; i++)
            {
                var argCompare = abiSignature[i].CompareTo(otherAbiSignature[i]);
                if (argCompare != 0)
                {
                    return argCompare;
                }
            }

            return 0;
        }
    }
}
