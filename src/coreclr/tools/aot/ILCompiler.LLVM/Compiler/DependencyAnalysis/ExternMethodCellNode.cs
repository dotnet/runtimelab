// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

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
    //
    // WASM requires the callee and caller signature to match. At the LLVM level, "callee type" is the function
    // type attached of the called operand and "caller" - that of its callsite. The problem, then, is that for a
    // given module, we can only have one function declaration, thus, one callee type. And we cannot know whether
    // this type will be the right one until, in general, runtime (this is the case for WASM imports provided by
    // the host environment). Thus, to achieve the experience of runtime erros on signature mismatches, we "hide"
    // the target behind an indirection.
    //
    internal sealed class ExternMethodCellNode(Utf8String externMethodName) : ObjectNode, ISymbolDefinitionNode
    {
        private WasmFunctionType? _signature;
        private object _methods;

        public Utf8String ExternMethodName { get; } = externMethodName;
        public WasmFunctionType Signature => _signature.Value;

        public int Offset => 0;
        public override bool RepresentsIndirectionCell => true;
        public override bool IsShareable => false;
        public override bool StaticDependenciesAreComputed => true;

        public void AddMethod(MethodDesc method)
        {
            method = method is PInvokeTargetNativeMethod nativeMethod ? nativeMethod.Target : method;

            switch (_methods)
            {
                case null:
                    _methods = method;
                    _signature = WasmAbi.GetWasmFunctionType(method, WasmFunctionAbiOptions.IsNative);
                    return;

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

            if (_signature != null && !WasmAbi.HasWasmFunctionType(method, Signature, WasmFunctionAbiOptions.IsNative))
            {
                _signature = null; // Mismatch!
            }
        }

        public bool HasSignatureMismatch(NodeFactory factory)
        {
            Debug.Assert(factory.MarkingComplete); // This question can only be answered after we've encountered all PIs.
            return _signature == null;
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

        public void EmitWarnings(Compilation compilation)
        {
            if (HasSignatureMismatch(compilation.NodeFactory))
            {
                IEnumerable<MethodDesc> methods = _methods switch
                {
                    null => [],
                    MethodDesc oneMethod => [oneMethod],
                    _ => (HashSet<MethodDesc>)_methods,
                };

                StringBuilder text = new();
                text.Append($"Signature mismatch detected: '{ExternMethodName}' will not be imported from the host environment");
                foreach (MethodDesc method in methods)
                {
                    text.Append($"\n Defined as: {method.Signature.ReturnType} {method}");
                }

                // Error code is just below the "AOT analysis" namespace.
                compilation.Logger.LogWarning(text.ToString(), 3049, (string)null);
            }
        }

        public override ObjectData GetData(NodeFactory factory, bool relocsOnly)
        {
            ObjectDataBuilder builder = new(factory, relocsOnly);
            builder.AddSymbol(this);
            builder.EmitPointerReloc(((LLVMCodegenNodeFactory)factory).ExternWasmMethod(this));

            return builder.ToObjectData();
        }

        public override ObjectNodeSection GetSection(NodeFactory factory) => ObjectNodeSection.ReadOnlyDataSection;

        public void AppendMangledName(NameMangler nameMangler, Utf8StringBuilder sb)
        {
            sb.Append("cell.");
            sb.Append(ExternMethodName);
        }

        public override int ClassCode => 935251149;

        public override int CompareToImpl(ISortableNode other, CompilerComparer comparer)
        {
            return ExternMethodName.CompareTo(((ExternMethodCellNode)other).ExternMethodName);
        }

        protected override string GetName(NodeFactory context) => $"{nameof(ExternMethodCellNode)} {ExternMethodName}";
    }

    internal sealed class ExternWasmMethodNode(ExternMethodCellNode methodCell) : ExternFunctionSymbolNode(methodCell.ExternMethodName), IWasmFunctionNode
    {
        private readonly ExternMethodCellNode _methodCell = methodCell;

        public WasmFunctionType GetWasmFunctionType(NodeFactory factory) =>
            _methodCell.HasSignatureMismatch(factory) ? new(WasmValueType.Invalid, []) : _methodCell.Signature;

        public bool GetImportModuleAndName(Compilation compilation, out string module, out string name)
        {
            NodeFactory factory = compilation.NodeFactory;
            if (_methodCell.HasSignatureMismatch(factory))
            {
                (module, name) = (null, null);
                return false;
            }

            MethodDesc method = _methodCell.GetSingleMethod(factory);
            return compilation.PInvokeILProvider.GetWasmImportCallInfo(method, out name, out module);
        }

        public override int ClassCode => 1890343813;

        protected override string GetName(NodeFactory context) => $"WasmImportFunctionNode {Utf8Name}";
    }
}
