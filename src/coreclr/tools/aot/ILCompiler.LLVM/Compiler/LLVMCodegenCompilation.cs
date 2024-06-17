// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using ILLink.Shared;

using Internal.IL;
using Internal.IL.Stubs;
using Internal.JitInterface;
using Internal.TypeSystem;

namespace ILCompiler
{
    public sealed partial class LLVMCodegenCompilation : RyuJitCompilation
    {
        private Dictionary<int, CorInfoImpl> _compilationContexts;
        private readonly LLVMCompilationResults _compilationResults = new();
        private string _outputFile;

        internal LLVMCodegenConfigProvider Options { get; }
        public new LLVMCodegenNodeFactory NodeFactory { get; }

        internal LLVMCodegenCompilation(DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            LLVMCodegenNodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> roots,
            ILProvider ilProvider,
            DebugInformationProvider debugInformationProvider,
            Logger logger,
            LLVMCodegenConfigProvider options,
            IInliningPolicy inliningPolicy,
            InstructionSetSupport instructionSetSupport,
            MethodImportationErrorProvider errorProvider,
            ReadOnlyFieldPolicy readOnlyFieldPolicy,
            RyuJitCompilationOptions baseOptions,
            int parallelism)
            : base(dependencyGraph, nodeFactory, roots, ilProvider, debugInformationProvider, logger, inliningPolicy, instructionSetSupport,
                null /* ProfileDataManager */, errorProvider, readOnlyFieldPolicy, baseOptions, parallelism)
        {
            NodeFactory = nodeFactory;
            Options = options;
            InitializeCodeGen();
        }

        protected override void CompileInternal(string outputFile, ObjectDumper dumper)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            StartCompilation(outputFile);

            _dependencyGraph.ComputeMarkedNodes();
            NodeFactory.SetMarkingComplete();
            Console.WriteLine($"LLVM compilation to IR finished in {stopwatch.Elapsed.TotalSeconds:0.##} seconds");

            FinishCompilation();

            LLVMObjectWriter.EmitObject(outputFile, _dependencyGraph.MarkedNodeList, this, dumper);

            Console.WriteLine($"LLVM generation of bitcode finished in {stopwatch.Elapsed.TotalSeconds:0.##} seconds");
        }

        private void StartCompilation(string outputFile)
        {
            _outputFile = outputFile;
        }

        private void FinishCompilation()
        {
            Parallel.ForEach(_compilationContexts, new() { MaxDegreeOfParallelism = _parallelism }, context =>
            {
                context.Value.JitFinishSingleThreadedCompilation();
            });

            _compilationContexts = null;
        }

        protected override void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj)
        {
            // Determine the list of method we actually need to compile
            var methodsToCompile = new List<LLVMMethodCodeNode>();
            var canonicalMethodsToCompile = new HashSet<MethodDesc>();

            foreach (DependencyNodeCore<NodeFactory> dependency in obj)
            {
                var methodCodeNodeNeedingCode = dependency as LLVMMethodCodeNode;
                if (methodCodeNodeNeedingCode == null)
                {
                    // To compute dependencies of the shadow method that tracks dictionary
                    // dependencies we need to ensure there is code for the canonical method body.
                    var dependencyMethod = (ShadowConcreteMethodNode)dependency;
                    methodCodeNodeNeedingCode = (LLVMMethodCodeNode)dependencyMethod.CanonicalMethodNode;
                }

                // We might have already queued this method for compilation
                MethodDesc method = methodCodeNodeNeedingCode.Method;
                if (method.IsCanonicalMethod(CanonicalFormKind.Any)
                    && !canonicalMethodsToCompile.Add(method))
                {
                    continue;
                }

                methodsToCompile.Add(methodCodeNodeNeedingCode);
            }

            CompileMethods(methodsToCompile);
        }

        private void CompileMethods(List<LLVMMethodCodeNode> methodsToCompile)
        {
            if (Logger.IsVerbose)
            {
                Logger.LogMessage($"Compiling {methodsToCompile.Count} methods...");
            }

            int moduleCount = Options.MaxLlvmModuleCount;
            if (_compilationContexts == null)
            {
                _compilationContexts = new();
                for (int index = 0; index < moduleCount; index++)
                {
                    CorInfoImpl corInfo = new CorInfoImpl(this);
                    string outputFilePath = Path.ChangeExtension(_outputFile, null) + $".{index}.bc";

                    corInfo.JitStartSingleThreadedCompilation(outputFilePath, Options.Target, Options.DataLayout);
                    _compilationResults.Add(outputFilePath);
                    _compilationContexts[index] = corInfo;
                }
            }

            Parallel.For(0, moduleCount, new() { MaxDegreeOfParallelism = _parallelism }, index =>
            {
                CorInfoImpl corInfo = _compilationContexts[index];
                int allMethodsCount = methodsToCompile.Count;
                int moduleMethodCount = allMethodsCount / moduleCount + 1;
                int lowMethodIndex = index * moduleMethodCount;
                int highMethodIndex = Math.Min(lowMethodIndex + moduleMethodCount, allMethodsCount);

                for (int i = lowMethodIndex; i < highMethodIndex; i++)
                {
                    CompileSingleMethod(corInfo, methodsToCompile[i]);
                }
            });
        }

        private void CompileSingleMethod(CorInfoImpl corInfo, LLVMMethodCodeNode methodCodeNodeNeedingCode)
        {
            MethodDesc method = methodCodeNodeNeedingCode.Method;

            TypeSystemException exception = _methodImportationErrorProvider.GetCompilationError(method);

            // If we previously failed to import the method, do not try to import it again and go
            // directly to the error path.
            if (exception == null)
            {
                try
                {
                    corInfo.CompileMethod(methodCodeNodeNeedingCode, null);
                    methodCodeNodeNeedingCode.CompilationCompleted = true;
                }
                catch (TypeSystemException ex)
                {
                    exception = ex;
                }
            }

            if (exception != null)
            {
                // Try to compile the method again, but with a throwing method body this time.
                MethodIL throwingIL = TypeSystemThrowingILEmitter.EmitIL(method, exception);
                corInfo.CompileMethod(methodCodeNodeNeedingCode, throwingIL);
                methodCodeNodeNeedingCode.CompilationCompleted = true;

                if (exception is TypeSystemException.InvalidProgramException
                    && method.OwningType is MetadataType mdOwningType
                    && mdOwningType.HasCustomAttribute("System.Runtime.InteropServices", "ClassInterfaceAttribute"))
                {
                    Logger.LogWarning(method, DiagnosticId.COMInteropNotSupportedInFullAOT);
                }
                if ((_compilationOptions & RyuJitCompilationOptions.UseResilience) != 0)
                    Logger.LogMessage($"Method '{method}' will always throw because: {exception.Message}");
                else
                    Logger.LogError($"Method will always throw because: {exception.Message}", 1005, method, MessageSubCategory.AotAnalysis);
            }
        }

        internal LLVMCompilationResults GetCompilationResults() => _compilationResults;
    }
}
