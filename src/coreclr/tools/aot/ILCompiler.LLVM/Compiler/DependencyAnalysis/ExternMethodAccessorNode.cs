// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ILCompiler.DependencyAnalysis.ARM;
using ILCompiler.DependencyAnalysis.ARM64;
using ILCompiler.DependencyAnalysis.LoongArch64;
using ILCompiler.DependencyAnalysis.RiscV64;
using ILCompiler.DependencyAnalysis.Wasm;
using ILCompiler.DependencyAnalysis.X64;
using ILCompiler.DependencyAnalysis.X86;

using Internal.IL.Stubs;
using Internal.JitInterface;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal sealed class ExternMethodAccessorNode(string externMethodName) : AssemblyStubNode, IWasmFunctionNode
    {
        private readonly Utf8String _externMethodName = externMethodName;
        private TargetAbiType[] _signature;
        private object _methods;

        public Utf8String ExternMethodName => _externMethodName;
        public ref TargetAbiType[] Signature => ref _signature;

        public override int ClassCode => 935251149;

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

        public bool HasSignatureMismatch(NodeFactory factory)
        {
            Debug.Assert(factory.MarkingComplete); // This question can only be answered after we've encountered all PIs.
            return _signature == null;
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

        public MethodDesc GetSingleMethod(NodeFactory factory)
        {
            Debug.Assert(!HasSignatureMismatch(factory));
            switch (_methods)
            {
                case MethodDesc oneMethod:
                    return oneMethod;
                default:
                    foreach (MethodDesc method in (HashSet<MethodDesc>)_methods)
                    {
                        return method;
                    }
                    return null;
            }
        }

        public override void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("get.");
            sb.Append(ExternMethodName);
        }

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return ExternMethodName.CompareTo(((ExternMethodAccessorNode)other).ExternMethodName);
        }

        public WasmFunctionType GetWasmFunctionType(NodeFactory factory) =>
            new WasmFunctionType(WasmAbi.GetNaturalIntType(factory.Target), []);

        public void EmitWarnings(Compilation compilation)
        {
            if (HasSignatureMismatch(compilation.NodeFactory))
            {
                string text = $"Signature mismatch detected: '{ExternMethodName}' will not be imported from the host environment";

                foreach (MethodDesc method in EnumerateMethods())
                {
                    text += $"\n Defined as: {method.Signature.ReturnType} {method}";
                }

                // Error code is just below the "AOT analysis" namespace.
                compilation.Logger.LogWarning(text, 3049, (string)null);
            }
        }

        protected override void EmitCode(NodeFactory factory, ref WasmEmitter encoder, bool relocsOnly)
        {
            encoder.DefineLocals();
            encoder.EmitNaturalConst(((LLVMCodegenNodeFactory)factory).ExternWasmMethod(this));
            encoder.EmitEnd();
        }

        protected override void EmitCode(NodeFactory factory, ref X64Emitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref X86Emitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref ARMEmitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref ARM64Emitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref LoongArch64Emitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();
        protected override void EmitCode(NodeFactory factory, ref RiscV64Emitter instructionEncoder, bool relocsOnly) => throw new NotImplementedException();

        protected override string GetName(NodeFactory context) => $"ExternMethodAccessor {ExternMethodName}";
    }

    internal sealed class ExternWasmMethodNode(ExternMethodAccessorNode accessor) : ExternSymbolNode(accessor.ExternMethodName), IWasmFunctionNode
    {
        private readonly ExternMethodAccessorNode _accessor = accessor;

        public override int ClassCode => 1890343813;

        public WasmFunctionType GetWasmFunctionType(NodeFactory factory)
        {
            if (_accessor.HasSignatureMismatch(factory))
            {
                return new WasmFunctionType(WasmValueType.Invalid, []);
            }

            // TODO-LLVM: the initial design of ExternMethodAccessorNode assumed that, eventually, ILC would not
            // need to concern itself with ABI (i. e. that only the Jit would need to know the ABI details). That
            // is why Signature is provided by Jit. However, at this point, it has become clear that we will not
            // be able to avoid WASM signature building in ILC, and so the Jit-based mechanism is basically (ILC
            // can compute the details by itself). Remove it.
            static WasmValueType ToWasmType(TargetAbiType type) => type switch
            {
                TargetAbiType.Void => WasmValueType.Invalid,
                TargetAbiType.Int32 => WasmValueType.I32,
                TargetAbiType.Int64 => WasmValueType.I64,
                TargetAbiType.Float => WasmValueType.F32,
                TargetAbiType.Double => WasmValueType.F64,
                _ => throw new NotImplementedException()
            };

            ReadOnlySpan<TargetAbiType> jitSig = _accessor.Signature;
            WasmValueType wasmReturnType = ToWasmType(jitSig[0]);
            WasmValueType[] wasmParamTypes = new WasmValueType[jitSig.Length - 1];
            for (int i = 0; i < wasmParamTypes.Length; i++)
            {
                wasmParamTypes[i] = ToWasmType(jitSig[i + 1]);
            }

            return new WasmFunctionType(wasmReturnType, wasmParamTypes);
        }

        public bool GetImportModuleAndName(Compilation compilation, out string module, out string name)
        {
            NodeFactory factory = compilation.NodeFactory;
            if (_accessor.HasSignatureMismatch(factory))
            {
                (module, name) = (null, null);
                return false;
            }

            MethodDesc method = _accessor.GetSingleMethod(factory);
            return compilation.PInvokeILProvider.GetWasmImportCallInfo(method, out name, out module);
        }

        protected override string GetName(NodeFactory context) => $"WasmImportFunctionNode {Utf8Name}";
    }
}
