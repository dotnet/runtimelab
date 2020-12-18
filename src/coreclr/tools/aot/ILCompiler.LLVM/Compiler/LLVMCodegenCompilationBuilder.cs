// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;

namespace ILCompiler
{
    public sealed class LLVMCodegenCompilationBuilder : CompilationBuilder
    {
        // These need to provide reasonable defaults so that the user can optionally skip
        // calling the Use/Configure methods and still get something reasonable back.
        LLVMCodegenConfigProvider _config = new LLVMCodegenConfigProvider(Array.Empty<string>());
        private ILProvider _ilProvider = new CoreRTILProvider();

        public LLVMCodegenCompilationBuilder(CompilerTypeSystemContext context, CompilationModuleGroup group)
            : base(context, group, new CoreRTNameMangler(new LLVMNodeMangler(), false))
        {
        }

        public override CompilationBuilder UseBackendOptions(IEnumerable<string> options)
        {
            _config = new LLVMCodegenConfigProvider(options);
            return this;
        }

        public override CompilationBuilder UseILProvider(ILProvider ilProvider)
        {
            _ilProvider = ilProvider;
            return this;
        }

        protected override ILProvider GetILProvider()
        {
            return _ilProvider;
        }

        public override ICompilation ToCompilation()
        {
            LLVMCodegenNodeFactory factory = new LLVMCodegenNodeFactory(_context, _compilationGroup, _metadataManager, _interopStubManager, _nameMangler, _vtableSliceProvider, _dictionaryLayoutProvider, GetPreinitializationManager());
            DependencyAnalyzerBase<NodeFactory> graph = CreateDependencyGraph(factory, new ObjectNode.ObjectNodeComparer(new CompilerComparer()));
            return new LLVMCodegenCompilation(graph, factory, _compilationRoots, _ilProvider, _debugInformationProvider, _logger, _config);
        }
    }

    internal class LLVMCodegenConfigProvider
    {
        private readonly HashSet<string> _options;
        
        public const string NoLineNumbersString = "NoLineNumbers";

        public LLVMCodegenConfigProvider(IEnumerable<string> options)
        {
            _options = new HashSet<string>(options, StringComparer.OrdinalIgnoreCase);
        }

        public string Target => ValueOrDefault("Target", "wasm32-unknown-emscripten");
        public string ModuleName => ValueOrDefault("ModuleName", "netscripten");

        // https://llvm.org/docs/LangRef.html#langref-datalayout
        // e litte endian, mangled names
        // m:e ELF mangling 
        // p:32:32 pointer size 32, abi 32
        // i64:64 64 ints aligned 64
        // n:32:64 native widths
        // S128 natural alignment of stack
        public string DataLayout => ValueOrDefault("DataLayout", "e-m:e-p:32:32-i64:64-n32:64-S128");

        private string ValueOrDefault(string optionName, string defaultValue)
        {
            if (_options.TryGetValue(optionName, out string value))
            {
                return value;
            }

            return defaultValue;
        }

        public bool HasOption(string optionName)
        {
            return _options.Contains(optionName);
        }
    }
}
