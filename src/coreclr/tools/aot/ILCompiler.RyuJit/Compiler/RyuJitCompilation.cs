// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.IL.Stubs;
using Internal.TypeSystem;
using Internal.JitInterface;

namespace ILCompiler
{
    public class RyuJitCompilation : Compilation
    {
        private readonly ConditionalWeakTable<Thread, CorInfoImpl> _corinfos = new ConditionalWeakTable<Thread, CorInfoImpl>();
        internal readonly RyuJitCompilationOptions _compilationOptions;
        private readonly ExternSymbolMappedField _hardwareIntrinsicFlags;
        private CountdownEvent _compilationCountdown;
        private readonly Dictionary<string, InstructionSet> _instructionSetMap;
        private readonly ProfileDataManager _profileDataManager;

        public InstructionSetSupport InstructionSetSupport { get; }

        public RyuJitCompilation(
            DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            NodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> roots,
            ILProvider ilProvider,
            DebugInformationProvider debugInformationProvider,
            Logger logger,
            DevirtualizationManager devirtualizationManager,
            IInliningPolicy inliningPolicy,
            InstructionSetSupport instructionSetSupport,
            ProfileDataManager profileDataManager,
            RyuJitCompilationOptions options)
            : base(dependencyGraph, nodeFactory, roots, ilProvider, debugInformationProvider, devirtualizationManager, inliningPolicy, logger)
        {
            _compilationOptions = options;
            _hardwareIntrinsicFlags = new ExternSymbolMappedField(nodeFactory.TypeSystemContext.GetWellKnownType(WellKnownType.Int32), "g_cpuFeatures");
            InstructionSetSupport = instructionSetSupport;

            _instructionSetMap = new Dictionary<string, InstructionSet>();
            foreach (var instructionSetInfo in InstructionSetFlags.ArchitectureToValidInstructionSets(TypeSystemContext.Target.Architecture))
            {
                if (!instructionSetInfo.Specifiable)
                    continue;

                _instructionSetMap.Add(instructionSetInfo.ManagedName, instructionSetInfo.InstructionSet);
            }

            _profileDataManager = profileDataManager;
        }

        public ProfileDataManager ProfileData => _profileDataManager;

        protected override void CompileInternal(string outputFile, ObjectDumper dumper)
        {
            _dependencyGraph.ComputeMarkedNodes();
            var nodes = _dependencyGraph.MarkedNodeList;

            NodeFactory.SetMarkingComplete();

            ObjectWritingOptions options = default;
            if (_debugInformationProvider is not NullDebugInformationProvider)
                options |= ObjectWritingOptions.GenerateDebugInfo;

            ObjectWriter.EmitObject(outputFile, nodes, NodeFactory, options, dumper);
        }

        protected override void ComputeDependencyNodeDependencies(List<DependencyNodeCore<NodeFactory>> obj)
        {
            // Determine the list of method we actually need to compile
            var methodsToCompile = new List<MethodCodeNode>();
            var canonicalMethodsToCompile = new HashSet<MethodDesc>();

            foreach (DependencyNodeCore<NodeFactory> dependency in obj)
            {
                var methodCodeNodeNeedingCode = dependency as MethodCodeNode;
                if (methodCodeNodeNeedingCode == null)
                {
                    // To compute dependencies of the shadow method that tracks dictionary
                    // dependencies we need to ensure there is code for the canonical method body.
                    var dependencyMethod = (ShadowConcreteMethodNode)dependency;
                    methodCodeNodeNeedingCode = (MethodCodeNode)dependencyMethod.CanonicalMethodNode;
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

            if ((_compilationOptions & RyuJitCompilationOptions.SingleThreadedCompilation) != 0)
            {
                CompileSingleThreaded(methodsToCompile);
            }
            else
            {
                CompileMultiThreaded(methodsToCompile);
            }
        }
        private void CompileMultiThreaded(List<MethodCodeNode> methodsToCompile)
        {
            if (Logger.IsVerbose)
            {
                Logger.Writer.WriteLine($"Compiling {methodsToCompile.Count} methods...");
            }

            WaitCallback compileSingleMethodDelegate = m =>
            {
                CorInfoImpl corInfo = _corinfos.GetValue(Thread.CurrentThread, thread => new CorInfoImpl(this));
                CompileSingleMethod(corInfo, (MethodCodeNode)m);
            };

            using (_compilationCountdown = new CountdownEvent(methodsToCompile.Count))
            {

                foreach (MethodCodeNode methodCodeNodeNeedingCode in methodsToCompile)
                {
                    ThreadPool.QueueUserWorkItem(compileSingleMethodDelegate, methodCodeNodeNeedingCode);
                }

                _compilationCountdown.Wait();
                _compilationCountdown = null;
            }
        }


        private void CompileSingleThreaded(List<MethodCodeNode> methodsToCompile)
        {
            CorInfoImpl corInfo = _corinfos.GetValue(Thread.CurrentThread, thread => new CorInfoImpl(this));

            foreach (MethodCodeNode methodCodeNodeNeedingCode in methodsToCompile)
            {
                if (Logger.IsVerbose)
                {
                    Logger.Writer.WriteLine($"Compiling {methodCodeNodeNeedingCode.Method}...");
                }

                CompileSingleMethod(corInfo, methodCodeNodeNeedingCode);
            }
        }

        private void CompileSingleMethod(CorInfoImpl corInfo, MethodCodeNode methodCodeNodeNeedingCode)
        {
            MethodDesc method = methodCodeNodeNeedingCode.Method;

            try
            {
                corInfo.CompileMethod(methodCodeNodeNeedingCode);
            }
            catch (TypeSystemException ex)
            {
                // TODO: fail compilation if a switch was passed

                // Try to compile the method again, but with a throwing method body this time.
                MethodIL throwingIL = TypeSystemThrowingILEmitter.EmitIL(method, ex);
                corInfo.CompileMethod(methodCodeNodeNeedingCode, throwingIL);

                if (ex is TypeSystemException.InvalidProgramException
                    && method.OwningType is MetadataType mdOwningType
                    && mdOwningType.HasCustomAttribute("System.Runtime.InteropServices", "ClassInterfaceAttribute"))
                {
                    Logger.LogWarning("COM interop is not supported with full ahead of time compilation", 9701, method, MessageSubCategory.AotAnalysis);
                }
                else
                {
                    Logger.LogWarning($"Method will always throw because: {ex.Message}", 1005, method, MessageSubCategory.AotAnalysis);
                }
            }
            finally
            {
                if (_compilationCountdown != null)
                    _compilationCountdown.Signal();
            }
        }

        public override MethodIL GetMethodIL(MethodDesc method)
        {
            TypeDesc owningType = method.OwningType;
            string intrinsicId = InstructionSetSupport.GetHardwareIntrinsicId(TypeSystemContext.Target.Architecture, owningType);
            if (!string.IsNullOrEmpty(intrinsicId)
                && HardwareIntrinsicHelpers.IsIsSupportedMethod(method))
            {
                InstructionSet instructionSet = _instructionSetMap[intrinsicId];

                // If this is an instruction set that is optimistically supported, but is not one of the
                // intrinsics that are known to be always available, emit IL that checks the support level
                // at runtime.
                if (!InstructionSetSupport.IsInstructionSetSupported(instructionSet)
                    && InstructionSetSupport.OptimisticFlags.HasInstructionSet(instructionSet))
                {
                    return HardwareIntrinsicHelpers.EmitIsSupportedIL(method, _hardwareIntrinsicFlags);
                }
            }

            return base.GetMethodIL(method);
        }

        public virtual void AddOrReturnGlobalSymbol(ISortableSymbolNode gcStaticSymbol, NameMangler nameMangler)
        {
        }
    }

    [Flags]
    public enum RyuJitCompilationOptions
    {
        MethodBodyFolding = 0x1,
        SingleThreadedCompilation = 0x2,
    }
}
