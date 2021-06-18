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
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public sealed class LLVMCodegenCompilation : RyuJitCompilation
    {
        private readonly ConditionalWeakTable<Thread, CorInfoImpl> _corinfos = new ConditionalWeakTable<Thread, CorInfoImpl>();
        private string _outputFile;

        internal LLVMCodegenConfigProvider Options { get; }
        // the LLVM Module generated from IL, can only be one.
        internal static LLVMModuleRef Module { get; private set; }
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
            _outputFile = outputFile;
            _dependencyGraph.ComputeMarkedNodes();

            var nodes = _dependencyGraph.MarkedNodeList;

            LLVMObjectWriter.EmitObject(outputFile, nodes, NodeFactory, this, dumper);

            CorInfoImpl.Shutdown(); // writes the LLVM bitcode

            Console.WriteLine($"RyuJIT compilation results, total methods {totalMethodCount} RyuJit Methods {ryuJitMethodCount} {((decimal)ryuJitMethodCount * 100 / totalMethodCount):n4}%");
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
            CorInfoImpl corInfo = _corinfos.GetValue(Thread.CurrentThread, thread => new CorInfoImpl(this));

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
        private unsafe void CompileSingleMethod(CorInfoImpl corInfo, LLVMMethodCodeNode methodCodeNodeNeedingCode)
        {
            MethodDesc method = methodCodeNodeNeedingCode.Method;

            try
            {
                var sig = method.Signature;
                if (sig.ReturnType.IsVoid &&
                    !method.RequiresInstArg()) // speed up
                {
                    corInfo.RegisterLlvmCallbacks((IntPtr)Unsafe.AsPointer(ref corInfo), _outputFile, Module.Target, Module.DataLayout);
                    corInfo.CompileMethod(methodCodeNodeNeedingCode);
                    methodCodeNodeNeedingCode.CompilationCompleted = true;
                    // TODO: delete this external function when old module is gone
                    LLVMValueRef externFunc = Module.AddFunction(NodeFactory.NameMangler.GetMangledMethodName(method).ToString(), GetLLVMSignatureForMethod(sig, method.RequiresInstArg()));
                    externFunc.Linkage = LLVMLinkage.LLVMExternalLinkage;

                    ILImporter.GenerateRuntimeExportThunk(this, method, externFunc);

                    ryuJitMethodCount++;
                }
                else ILImporter.CompileMethod(this, methodCodeNodeNeedingCode);
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

        internal static LLVMTypeRef GetLLVMSignatureForMethod(MethodSignature signature, bool hasHiddenParam)
        {
            TypeDesc returnType = signature.ReturnType;
            LLVMTypeRef llvmReturnType;
            bool returnOnStack = false;
            if (!NeedsReturnStackSlot(signature))
            {
                returnOnStack = true;
                llvmReturnType = ILImporter.GetLLVMTypeForTypeDesc(returnType);
            }
            else
            {
                llvmReturnType = LLVMTypeRef.Void;
            }

            List<LLVMTypeRef> signatureTypes = new List<LLVMTypeRef>();
            signatureTypes.Add(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)); // Shadow stack pointer

            if (!returnOnStack && !signature.ReturnType.IsVoid)
            {
                signatureTypes.Add(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
            }

            if (hasHiddenParam)
            {
                signatureTypes.Add(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)); // *EEType
            }

            // Intentionally skipping the 'this' pointer since it could always be a GC reference
            // and thus must be on the shadow stack
            foreach (TypeDesc type in signature)
            {
                if (ILImporter.CanStoreTypeOnStack(type))
                {
                    signatureTypes.Add(ILImporter.GetLLVMTypeForTypeDesc(type));
                }
            }

            return LLVMTypeRef.CreateFunction(llvmReturnType, signatureTypes.ToArray(), false);
        }

        /// <summary>
        /// Returns true if the method returns a type that must be kept
        /// on the shadow stack
        /// </summary>
        internal static bool NeedsReturnStackSlot(MethodSignature signature)
        {
            return !signature.ReturnType.IsVoid && !ILImporter.CanStoreTypeOnStack(signature.ReturnType);
        }

        public TypeDesc GetWellKnownType(WellKnownType wellKnownType)
        {
            return TypeSystemContext.GetWellKnownType(wellKnownType);
        }

        public override void AddOrReturnGlobalSymbol(ISortableSymbolNode gcStaticSymbol, NameMangler nameMangler)
        {
            LLVMObjectWriter.AddOrReturnGlobalSymbol(Module, gcStaticSymbol, nameMangler);
        }
    }
}
