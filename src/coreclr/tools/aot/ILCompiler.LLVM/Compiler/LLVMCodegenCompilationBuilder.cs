// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Internal.JitInterface;

namespace ILCompiler
{
    public sealed class LLVMCodegenCompilationBuilder : RyuJitCompilationBuilder
    {
        private LLVMCodegenConfigProvider _config = new LLVMCodegenConfigProvider();

        public LLVMCodegenCompilationBuilder(CompilerTypeSystemContext context, CompilationModuleGroup group)
            : base(context, group, new LLVMNodeMangler())
        {
        }

        public override CompilationBuilder UseBackendOptions(IEnumerable<string> options)
        {
            base.UseBackendOptions(options);

            _config.SetOptions(GetBackendOptions());

            return this;
        }

        protected override RyuJitCompilation CreateCompilation(RyuJitCompilationOptions options)
        {
            ObjectDataInterner interner = ObjectDataInterner.Null;
            var factory = new LLVMCodegenNodeFactory(_context, _compilationGroup, _metadataManager, _interopStubManager, _nameMangler, _vtableSliceProvider, _dictionaryLayoutProvider, _inlinedThreadStatics, GetPreinitializationManager(), _devirtualizationManager, interner);
            DependencyAnalyzerBase<NodeFactory> graph = CreateDependencyGraph(factory, new ObjectNode.ObjectNodeComparer(new CompilerComparer()));

            return new LLVMCodegenCompilation(graph, factory, _compilationRoots, GetILProvider(), _debugInformationProvider, _logger, _config, _inliningPolicy, _instructionSetSupport, _methodImportationErrorProvider, _readOnlyFieldPolicy, options, _parallelism);
        }
    }

    internal sealed class LLVMCodegenConfigProvider
    {
        internal void SetOptions(IEnumerable<KeyValuePair<string, string>> options)
        {
            foreach ((string name, string value) in options)
            {
                switch (name)
                {
                    case "Target":
                        Target = value;
                        break;
                    case "DataLayout":
                        DataLayout = value;
                        break;
                    case "LlvmExceptionHandlingModel":
                        ExceptionHandlingModel = value switch
                        {
                            "cpp" => CorInfoLlvmEHModel.Cpp,
                            "wasm" => CorInfoLlvmEHModel.Wasm,
                            "emulated" => CorInfoLlvmEHModel.Emulated,
                            _ => throw new ArgumentException("Invalid LlvmExceptionHandlingModel value")
                        };
                        break;
                    case "MaxLlvmModuleCount":
                        MaxLlvmModuleCount = int.Parse(value);
                        if (MaxLlvmModuleCount < 1)
                        {
                            throw new ArgumentException("LLVM module count must be positive");
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        // https://llvm.org/docs/LangRef.html#langref-datalayout
        // e litte endian, mangled names
        // m:e ELF mangling
        // p:32:32 pointer size 32, abi 32
        // i64:64 64 ints aligned 64
        // n:32:64 native widths
        // S128 natural alignment of stack
        public string DataLayout { get; private set; } = "e-m:e-p:32:32-i64:64-n32:64-S128";
        public string Target { get; private set; } = "wasm32-unknown-emscripten";
        public CorInfoLlvmEHModel ExceptionHandlingModel { get; private set; } = CorInfoLlvmEHModel.Wasm;

        // Below options are debug-only and not supported.
        public int MaxLlvmModuleCount { get; private set; } = 8;
    }
}
