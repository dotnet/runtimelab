// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Internal.TypeSystem;
using Internal.IL;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using LLVMSharp.Interop;
using ILCompiler.LLVM;
using Internal.JitInterface;
using Internal.IL.Stubs;

namespace ILCompiler
{
    public sealed class LLVMCodegenCompilation : RyuJitCompilation
    {
        private readonly ConditionalWeakTable<Thread, CorInfoImpl> _corinfos = new ConditionalWeakTable<Thread, CorInfoImpl>();
        // private CountdownEvent _compilationCountdown;

        internal LLVMCodegenConfigProvider Options { get; }
        internal LLVMModuleRef Module { get; }
        internal LLVMTargetDataRef TargetData { get; }
        public new LLVMCodegenNodeFactory NodeFactory { get; }
        internal LLVMDIBuilderRef DIBuilder { get; }
        internal Dictionary<string, DebugMetadata> DebugMetadataMap { get; }
        internal LLVMCodegenCompilation(DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            LLVMCodegenNodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> roots,
            ILProvider ilProvider,
            DebugInformationProvider debugInformationProvider,
            Logger logger,
            LLVMCodegenConfigProvider options,
            IInliningPolicy inliningPolicy,
            DevirtualizationManager devirtualizationManager,
            InstructionSetSupport instructionSetSupport)
            : base(dependencyGraph, nodeFactory, GetCompilationRoots(roots, nodeFactory), ilProvider, debugInformationProvider, logger, devirtualizationManager, inliningPolicy, instructionSetSupport, 0)
        {
            NodeFactory = nodeFactory;
            LLVMModuleRef m = LLVMModuleRef.CreateWithName(options.ModuleName);
            m.Target = options.Target;
            m.DataLayout = options.DataLayout;
            Module = m;
            TargetData = m.CreateExecutionEngine().TargetData;
            Options = options;
            DIBuilder = Module.CreateDIBuilder();
            DebugMetadataMap = new Dictionary<string, DebugMetadata>();
        }

        private static IEnumerable<ICompilationRootProvider> GetCompilationRoots(IEnumerable<ICompilationRootProvider> existingRoots, NodeFactory factory)
        {
            foreach (var existingRoot in existingRoots)
                yield return existingRoot;
        }

        protected override void CompileInternal(string outputFile, ObjectDumper dumper)
        {
            _dependencyGraph.ComputeMarkedNodes();

            var nodes = _dependencyGraph.MarkedNodeList;

            Console.WriteLine($"RyuJIT compilation results, total methods {totalMethodCount} RyuJit Methods {ryuJitMethodCount} % {((decimal)ryuJitMethodCount * 100 / totalMethodCount):n4}");
            LLVMObjectWriter.EmitObject(outputFile, nodes, NodeFactory, this, dumper);
        }

        protected override void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj)
        {
            // Determine the list of method we actually need to compile
            var methodsToCompile = new List<LLVMMethodCodeNode>();
            foreach (var dependency in obj)
            {
                var methodCodeNodeNeedingCode = dependency as LLVMMethodCodeNode;
                if (methodCodeNodeNeedingCode == null)
                {
                    // To compute dependencies of the shadow method that tracks dictionary
                    // dependencies we need to ensure there is code for the canonical method body.
                    var dependencyMethod = (ShadowConcreteMethodNode)dependency;
                    methodCodeNodeNeedingCode = (LLVMMethodCodeNode)dependencyMethod.CanonicalMethodNode;
                }

                // We might have already compiled this method.
                if (methodCodeNodeNeedingCode.StaticDependenciesAreComputed)
                    continue;

                methodsToCompile.Add(methodCodeNodeNeedingCode);
            }
            CompileSingleThreaded(methodsToCompile);
        }

        private void CompileSingleThreaded(List<LLVMMethodCodeNode> methodsToCompile)
        {
            CorInfoImpl corInfo = _corinfos.GetValue(Thread.CurrentThread, thread => new CorInfoImpl(this, Module.Handle));

            foreach (LLVMMethodCodeNode methodCodeNodeNeedingCode in methodsToCompile)
            {
                if (methodCodeNodeNeedingCode.StaticDependenciesAreComputed)
                    continue;

                if (Logger.IsVerbose)
                {
                    Logger.Writer.WriteLine($"Compiling {methodCodeNodeNeedingCode.Method}...");
                }

                CompileSingleMethod(corInfo, methodCodeNodeNeedingCode);
            }
        }

        static int totalMethodCount;
        static int ryuJitMethodCount;
        private void CompileSingleMethod(CorInfoImpl corInfo, LLVMMethodCodeNode methodCodeNodeNeedingCode)
        {
            MethodDesc method = methodCodeNodeNeedingCode.Method;

            try
            {
                corInfo.CompileMethod(methodCodeNodeNeedingCode);
                ryuJitMethodCount++;
            }
            catch (CodeGenerationFailedException)
            {
                ILImporter.CompileMethod(this, methodCodeNodeNeedingCode);
            }
            catch (TypeSystemException ex)
            {
                // TODO: fail compilation if a switch was passed

                // Try to compile the method again, but with a throwing method body this time.
                MethodIL throwingIL = TypeSystemThrowingILEmitter.EmitIL(method, ex);
                corInfo.CompileMethod(methodCodeNodeNeedingCode, throwingIL);

                // TODO: Log as a warning. For now, just log to the logger; but this needs to
                // have an error code, be supressible, the method name/sig needs to be properly formatted, etc.
                // https://github.com/dotnet/corert/issues/72
                Logger.Writer.WriteLine($"Warning: Method `{method}` will always throw because: {ex.Message}");
            }
            finally
            {
                totalMethodCount++;
                // if (_compilationCountdown != null)
                //     _compilationCountdown.Signal();
            }
        }

        public TypeDesc ConvertToCanonFormIfNecessary(TypeDesc type, CanonicalFormKind policy)
        {
            if (!type.IsCanonicalSubtype(CanonicalFormKind.Any))
                return type;

            if (type.IsPointer || type.IsByRef)
            {
                ParameterizedType parameterizedType = (ParameterizedType)type;
                TypeDesc paramTypeConverted = ConvertToCanonFormIfNecessary(parameterizedType.ParameterType, policy);
                if (paramTypeConverted == parameterizedType.ParameterType)
                    return type;

                if (type.IsPointer)
                    return TypeSystemContext.GetPointerType(paramTypeConverted);

                if (type.IsByRef)
                    return TypeSystemContext.GetByRefType(paramTypeConverted);
            }

            return type.ConvertToCanonForm(policy);
        }
    }
}
