// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.JitInterface;

namespace ILCompiler
{
    public sealed class LLVMCodegenCompilationBuilder : CompilationBuilder
    {
        // These need to provide reasonable defaults so that the user can optionally skip
        // calling the Use/Configure methods and still get something reasonable back.
        LLVMCodegenConfigProvider _config = new LLVMCodegenConfigProvider(Array.Empty<string>());
        private ILProvider _ilProvider = new CoreRTILProvider();
        private KeyValuePair<string, string>[] _ryujitOptions = Array.Empty<KeyValuePair<string, string>>();

        public LLVMCodegenCompilationBuilder(CompilerTypeSystemContext context, CompilationModuleGroup group)
            : base(context, group, new CoreRTNameMangler(new LLVMNodeMangler(), false))
        {
        }

        public override CompilationBuilder UseBackendOptions(IEnumerable<string> options)
        {
            _config = new LLVMCodegenConfigProvider(options);
            var builder = new ArrayBuilder<KeyValuePair<string, string>>();

            foreach (string param in options)
            {
                int indexOfEquals = param.IndexOf('=');

                // We're skipping bad parameters without reporting.
                // This is not a mainstream feature that would need to be friendly.
                // Besides, to really validate this, we would also need to check that the config name is known.
                if (indexOfEquals < 1)
                    continue;

                string name = param.Substring(0, indexOfEquals);
                string value = param.Substring(indexOfEquals + 1);

                builder.Add(new KeyValuePair<string, string>(name, value));
            }

            _ryujitOptions = builder.ToArray();

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
            ArrayBuilder<CorJitFlag> jitFlagBuilder = new ArrayBuilder<CorJitFlag>();

            switch (_optimizationMode)
            {
                case OptimizationMode.None:
                    // Dont set CORJIT_FLAG_DEBUG_CODE as it will set MinOpts and hence no liveness, and no SSA
                    // TODO: a better way to enable SSA with this flag set?
                    //jitFlagBuilder.Add(CorJitFlag.CORJIT_FLAG_DEBUG_CODE);
                    jitFlagBuilder.Add(CorJitFlag.CORJIT_FLAG_DEBUG_INFO);
                    break;

                case OptimizationMode.PreferSize:
                    jitFlagBuilder.Add(CorJitFlag.CORJIT_FLAG_SIZE_OPT);
                    break;

                case OptimizationMode.PreferSpeed:
                    jitFlagBuilder.Add(CorJitFlag.CORJIT_FLAG_SPEED_OPT);
                    break;

                default:
                    // Not setting a flag results in BLENDED_CODE.
                    break;
            }

            LLVMCodegenNodeFactory factory = new LLVMCodegenNodeFactory(_context, _compilationGroup, _metadataManager, _interopStubManager, _nameMangler, _vtableSliceProvider, _dictionaryLayoutProvider, GetPreinitializationManager());
            JitConfigProvider.Initialize(_context.Target, jitFlagBuilder.ToArray(), _ryujitOptions);
            DependencyAnalyzerBase<NodeFactory> graph = CreateDependencyGraph(factory, new ObjectNode.ObjectNodeComparer(new CompilerComparer()));
            return new LLVMCodegenCompilation(graph, factory, _compilationRoots, _ilProvider, _debugInformationProvider, _logger, _config, _inliningPolicy, _devirtualizationManager, _instructionSetSupport);
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
