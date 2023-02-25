// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis.X64;
using ILCompiler.DependencyAnalysis.X86;
using ILCompiler.DependencyAnalysis.ARM;
using ILCompiler.DependencyAnalysis.ARM64;
using ILCompiler.DependencyAnalysis.Wasm;

using Internal.IL.Stubs;
using Internal.TypeSystem;
using Internal.JitInterface;
using Internal.Text;

namespace ILCompiler.DependencyAnalysis
{
    internal class ExternMethodAccessorNode : AssemblyStubNode
    {
        private readonly Utf8String _externMethodName;
        private TargetAbiType[] _signature;
        private object _methods;

        public ExternMethodAccessorNode(string externMethodName)
        {
            _externMethodName = externMethodName;
        }

        public ref readonly Utf8String ExternMethodName => ref _externMethodName;
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
            sb.Append(ExternMethodName);
        }

        public override int ClassCode => 935251149;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return ExternMethodName.CompareTo(((ExternMethodAccessorNode)other).ExternMethodName);
        }

        protected override void EmitCode(NodeFactory factory, ref X64Emitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref X86Emitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref ARMEmitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref ARM64Emitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref WasmEmitter instructionEncoder, bool relocsOnly) { }

        protected override string GetName(NodeFactory context) => $"ExternMethodAccessor {ExternMethodName}";
    }
}
