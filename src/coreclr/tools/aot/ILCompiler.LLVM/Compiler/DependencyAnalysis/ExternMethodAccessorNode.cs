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
            return ExternSymbolKey.CompareTo(((ExternMethodAccessorNode)other).ExternSymbolKey, comparer);
        }

        protected override void EmitCode(NodeFactory factory, ref X64Emitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref X86Emitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref ARMEmitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref ARM64Emitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref LoongArch64Emitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref WasmEmitter instructionEncoder, bool relocsOnly) { }

        protected override string GetName(NodeFactory context) => $"ExternMethodAccessor {ExternSymbolKey.ExternMethodName}";
    }

    internal sealed class ExternSymbolKey  : IEquatable<ExternSymbolKey>
    {
        private readonly MethodSignature _methodSignature;

        public ExternSymbolKey(string externModuleName, string externMethodName, bool wasmImport,
            MethodSignature methodSignature)
        {
            _methodSignature = methodSignature;

            ExternModuleName = externModuleName;
            ExternMethodName = externMethodName;
            WasmImport = wasmImport;
        }

        public string ExternModuleName { get; }
        public readonly Utf8String ExternMethodName;
        public bool WasmImport { get; }

        public override bool Equals(object obj) => obj is ExternSymbolKey wasmImportKey && Equals(wasmImportKey);

        // Only compare the signature for Wasm Imports.  `memset` is an example that appears with different signatures
        public bool Equals(ExternSymbolKey other) => ExternModuleName == other.ExternModuleName
                                                     && ExternMethodName.Equals(other.ExternMethodName)
                                                     && WasmImport == other.WasmImport
                                                     && (!WasmImport || _methodSignature.EquivalentTo(other._methodSignature));

        public override int GetHashCode()
        {
            return WasmImport
                ? HashCode.Combine(ExternModuleName, ExternMethodName, WasmImport, _methodSignature)
                : HashCode.Combine(ExternModuleName, ExternMethodName, WasmImport);
        }

        public int CompareTo(ExternSymbolKey other, CompilerComparer comparer)
        {
            int result = ExternMethodName.CompareTo(other.ExternMethodName);
            if (result != 0)
            {
                return result;
            }

            result = ExternModuleName.CompareTo(other.ExternModuleName);
            if (result != 0)
            {
                return result;
            }

            result = WasmImport.CompareTo(other.WasmImport);
            if (result != 0)
            {
                return result;
            }

            return comparer.Compare(_methodSignature, other._methodSignature);
        }
    }
}
