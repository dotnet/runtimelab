// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

using ILCompiler.DependencyAnalysisFramework;
using ILCompiler.ObjectWriter;
using Internal.JitInterface;
using Internal.JitInterface.LLVMInterop;
using Internal.Text;
using Internal.TypeSystem;
using ObjectData = ILCompiler.DependencyAnalysis.ObjectNode.ObjectData;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Object writer that emits LLVM (bitcode).
    /// </summary>
    internal sealed unsafe class LLVMObjectWriter
    {
        private readonly WasmObjectWriter _wasmObjectWriter;

        private readonly LLVMContext* _llvmContext;

        // Module with the external functions and getters for them. Must be separate from the main module so as to avoid
        // direct calls with mismatched signatures at the LLVM level.
        private readonly LLVMModuleRef _moduleWithExternalFunctions;

        private readonly LLVMType* _ptrType;

        // Node factory and compilation for which ObjectWriter was instantiated.
        private readonly LLVMCodegenCompilation _compilation;
        private readonly NodeFactory _nodeFactory;

        // Path to the bitcode file we're emitting.
        private readonly string _objectFilePath;

        // Used for writing mangled names together with Utf8Name.
        private readonly Utf8StringBuilder _utf8StringBuilder = new Utf8StringBuilder();

#if DEBUG
        private static readonly Dictionary<string, ISymbolNode> _previouslyWrittenNodeNames = new();
#endif

        private LLVMObjectWriter(string objectFilePath, LLVMCodegenCompilation compilation)
        {
            _wasmObjectWriter = new WasmObjectWriter(compilation);
            _llvmContext = compilation.LLVMContext;
            _ptrType = compilation.LLVMPtrType;

            string target = compilation.Options.Target;
            string dataLayout = compilation.Options.DataLayout;
            _moduleWithExternalFunctions = LLVMModuleRef.Create(_llvmContext, "external"u8, target, dataLayout);

            _compilation = compilation;
            _nodeFactory = compilation.NodeFactory;
            _objectFilePath = objectFilePath;
        }

        public static void EmitObject(string objectFilePath, IEnumerable<DependencyNode> nodes, LLVMCodegenCompilation compilation, IObjectDumper dumper)
        {
            LLVMObjectWriter objectWriter = new LLVMObjectWriter(objectFilePath, compilation);
            NodeFactory factory = compilation.NodeFactory;

            try
            {
                foreach (DependencyNode depNode in nodes)
                {
                    ObjectNode node = depNode as ObjectNode;
                    if (node == null)
                        continue;

                    if (node.ShouldSkipEmittingObjectNode(factory))
                        continue;

                    switch (node)
                    {
                        case ExternMethodAccessorNode accessorNode:
                            objectWriter.GetCodeForExternMethodAccessor(accessorNode);
                            continue;
                        default:
                            break;
                    }

                    ObjectData nodeContents = node.GetData(factory);

                    dumper?.DumpObjectNode(factory, node, nodeContents);
#if DEBUG
                    foreach (ISymbolNode definedSymbol in nodeContents.DefinedSymbols)
                    {
                        try
                        {
                            _previouslyWrittenNodeNames.Add(definedSymbol.GetMangledName(factory.NameMangler), definedSymbol);
                        }
                        catch (ArgumentException)
                        {
                            ISymbolNode alreadyWrittenSymbol = _previouslyWrittenNodeNames[definedSymbol.GetMangledName(factory.NameMangler)];
                            Debug.Fail("Duplicate node name emitted to file",
                            $"Symbol {definedSymbol.GetMangledName(factory.NameMangler)} has already been written to the output object file {objectFilePath} with symbol {alreadyWrittenSymbol}");
                        }
                    }
#endif
                    objectWriter.EmitObjectNode(node, nodeContents);
                }

                objectWriter.FinishObjWriter();

                // Make sure we have released all memory used for mangling names.
                Debug.Assert(objectWriter._utf8StringBuilder.Length == 0);
            }
            catch
            {
                // If there was an exception while generating the OBJ file, make sure we don't leave the unfinished object file around.
                // TODO-LLVM: we also emit the results file, etc, should delete those too.
                try
                {
                    File.Delete(objectFilePath);
                }
                catch { }

                // Continue with the original exception.
                throw;
            }
        }

        private void EmitObjectNode(ObjectNode node, ObjectData nodeContents)
        {
            ISymbolDefinitionNode sectionSymbol = null;
            ObjectNodeSection section = node.GetSection(_nodeFactory);
            if (section.IsStandardSection && node is ISymbolDefinitionNode definingSymbol)
            {
                // We **could** emit everything into one huge section, which is also how other targets do it.
                // However, that would hinder linker GC and diagnosability. We therefore choose to split
                // the data up into sections, one for each object node. Note that this choice only exists
                // for data sections. We do not have control over how code is treated by the linker - it is
                // always processed on a function granularity.
                sectionSymbol = definingSymbol;
            }

            _wasmObjectWriter.Emit(section, sectionSymbol, nodeContents);
        }

        private void FinishObjWriter()
        {
#if DEBUG
            _moduleWithExternalFunctions.PrintToFile(Path.ChangeExtension(_objectFilePath, "external.txt"));
            _moduleWithExternalFunctions.Verify();
#endif
            string dataWasmObjectPath = _objectFilePath;
            _wasmObjectWriter.WriteObject(dataWasmObjectPath);

            string externalLlvmObjectPath = Path.ChangeExtension(_objectFilePath, "external.bc");
            _moduleWithExternalFunctions.WriteBitcodeToFile(externalLlvmObjectPath);

            LLVMCompilationResults compilationResults = _compilation.GetCompilationResults();
            compilationResults.Add(dataWasmObjectPath);
            compilationResults.Add(externalLlvmObjectPath);
            compilationResults.SerializeToFile(Path.ChangeExtension(_objectFilePath, "results.txt"));
        }

        private void GetCodeForExternMethodAccessor(ExternMethodAccessorNode node)
        {
            LLVMTypeRef externFuncType;
            if (node.Signature != null)
            {
                LLVMTypeRef GetLlvmType(TargetAbiType abiType) => abiType switch
                {
                    TargetAbiType.Void => _compilation.LLVMVoidType,
                    TargetAbiType.Int32 => _compilation.LLVMInt32Type,
                    TargetAbiType.Int64 => _compilation.LLVMInt64Type,
                    TargetAbiType.Float => _compilation.LLVMFloatType,
                    TargetAbiType.Double => _compilation.LLVMDoubleType,
                    _ => throw new UnreachableException()
                };

                TargetAbiType[] sig = node.Signature;
                LLVMTypeRef returnType = GetLlvmType(sig[0]);
                LLVMTypeRef[] paramTypes = new LLVMTypeRef[sig.Length - 1];
                for (int i = 0; i < paramTypes.Length; i++)
                {
                    paramTypes[i] = GetLlvmType(sig[i + 1]);
                }

                externFuncType = LLVMTypeRef.CreateFunction(returnType, paramTypes);
            }
            else // Report the signature mismatch warning, use a placeholder signature.
            {
                string text = $"Signature mismatch detected: '{node.ExternMethodName}' will not be imported from the host environment";

                foreach (MethodDesc method in node.EnumerateMethods())
                {
                    text += $"\n Defined as: {method.Signature.ReturnType} {method}";
                }

                // Error code is just below the "AOT analysis" namespace.
                _compilation.Logger.LogWarning(text, 3049, Path.GetFileNameWithoutExtension(_objectFilePath));

                externFuncType = LLVMTypeRef.CreateFunction(_compilation.LLVMVoidType, []);
            }

            ReadOnlySpan<byte> externFuncName = node.ExternMethodName.AsSpan();
            LLVMModuleRef externFuncModule = _moduleWithExternalFunctions;
            Debug.Assert(externFuncModule.GetNamedFunction(externFuncName).Handle == null);
            LLVMValueRef externFunc = externFuncModule.AddFunction(externFuncName, externFuncType);

            // Add import attributes if specified.
            if (node.Signature != null && _compilation.PInvokeILProvider.GetWasmImportCallInfo(node.GetSingleMethod(), out string externName, out string moduleName))
            {
                externFunc.AddFunctionAttribute("wasm-import-module"u8, moduleName);
                externFunc.AddFunctionAttribute("wasm-import-name"u8, externName);
            }

            // Define the accessor function.
            using Utf8Name accessorFuncName = GetMangledUtf8Name(node);
            LLVMTypeRef accessorFuncType = LLVMTypeRef.CreateFunction(externFunc.TypeOf, Array.Empty<LLVMTypeRef>());
            LLVMValueRef accessorFunc = externFuncModule.AddFunction(accessorFuncName, accessorFuncType);

            using LLVMBuilderRef builder = LLVMBuilderRef.Create(_llvmContext);
            LLVMBasicBlockRef block = accessorFunc.AppendBasicBlock("GetExternFunc"u8);
            builder.PositionAtEnd(block);
            builder.BuildRet(externFunc);
        }

        private Utf8Name GetMangledUtf8Name(ExternMethodAccessorNode node, string suffix = null)
        {
            Utf8StringBuilder builder = _utf8StringBuilder;

            int offset = builder.Length;
            node.AppendMangledName(_nodeFactory.NameMangler, _utf8StringBuilder);
            if (suffix != null)
            {
                builder.Append(suffix);
            }
            return new(builder, offset);
        }

        private Utf8Name GetUtf8Name(string name)
        {
            Utf8StringBuilder builder = _utf8StringBuilder;

            int offset = builder.Length;
            builder.Append(name);
            return new(builder, offset);
        }

        private static LLVMValueRef CreateCall(LLVMBuilderRef builder, LLVMValueRef func, ReadOnlySpan<LLVMValueRef> args, ReadOnlySpan<byte> name = default)
        {
            return builder.BuildCall(func.GetValueType(), func, args, name);
        }

        /// <summary>
        /// A little helper struct that releases memory associated with the string when disposed.
        /// Intended to be used together with 'using', RAII-style; must be passed by reference.
        /// </summary>
        private readonly ref struct Utf8Name
        {
            private readonly Utf8StringBuilder _builder;
            private readonly int _offset;
            private readonly int _length;

            public Utf8Name(Utf8StringBuilder builder, int offset)
            {
                _builder = builder;
                _offset = offset;
                _length = builder.Length - offset;

                builder.Append('\0');
            }

            public void Dispose()
            {
                Debug.Assert(_offset < _builder.Length, "Double dispose");
                _builder.Truncate(_offset);
            }

            public override string ToString() => Encoding.UTF8.GetString(this);

            public static implicit operator ReadOnlySpan<byte>(in Utf8Name name) =>
                name._builder.AsSpan().Slice(name._offset, name._length);
        }
    }
}
