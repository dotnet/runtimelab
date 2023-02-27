// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using ILCompiler.Compiler;
using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using ILCompiler.LLVM;
using ILLink.Shared;

using Internal.IL;
using Internal.IL.Stubs;
using Internal.JitInterface;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using LLVMSharp.Interop;

namespace ILCompiler
{
    public unsafe sealed class LLVMCodegenCompilation : RyuJitCompilation
    {
        [DllImport("libLLVM", EntryPoint = "LLVMContextSetOpaquePointers", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMContextRef LLVMContextSetOpaquePointers(LLVMContextRef C, bool OpaquePointers);

        private readonly ConditionalWeakTable<Thread, CorInfoImpl> _corinfos = new();
        private string _outputFile;

        // the LLVM Module generated from IL, can only be one.
        internal static LLVMModuleRef Module { get; private set; }
        internal LLVMTargetDataRef TargetData { get; }
        public new LLVMCodegenNodeFactory NodeFactory { get; }
        internal LLVMDIBuilderRef DIBuilder { get; }
        internal Dictionary<string, DebugMetadata> DebugMetadataMap { get; }
        internal ConfigurableWasmImportPolicy ConfigurableWasmImportPolicy { get; }

        internal LLVMCodegenCompilation(DependencyAnalyzerBase<NodeFactory> dependencyGraph,
            LLVMCodegenNodeFactory nodeFactory,
            IEnumerable<ICompilationRootProvider> roots,
            ILProvider ilProvider,
            DebugInformationProvider debugInformationProvider,
            Logger logger,
            LLVMCodegenConfigProvider options,
            IInliningPolicy inliningPolicy,
            DevirtualizationManager devirtualizationManager,
            InstructionSetSupport instructionSetSupport,
            ConfigurableWasmImportPolicy configurableWasmImportPolicy,
            MethodImportationErrorProvider errorProvider,
            RyuJitCompilationOptions baseOptions)
            : base(dependencyGraph, nodeFactory, GetCompilationRoots(roots, nodeFactory), ilProvider, debugInformationProvider, logger, devirtualizationManager, inliningPolicy ?? new LLVMNoInLiningPolicy(), instructionSetSupport,
                null /* ProfileDataManager */, errorProvider, baseOptions, 1)
        {
            NodeFactory = nodeFactory;
            LLVMModuleRef m = LLVMModuleRef.CreateWithName(options.ModuleName);
            m.Target = options.Target;
            m.DataLayout = options.DataLayout;
            Module = m;
            TargetData = m.CreateExecutionEngine().TargetData;
            DIBuilder = Module.CreateDIBuilder();
            DebugMetadataMap = new Dictionary<string, DebugMetadata>();
            ILImporter.Context = Module.Context;
            LLVMContextSetOpaquePointers(Module.Context, false);

            ConfigurableWasmImportPolicy = configurableWasmImportPolicy;
        }

        private static IEnumerable<ICompilationRootProvider> GetCompilationRoots(IEnumerable<ICompilationRootProvider> existingRoots, NodeFactory factory)
        {
            foreach (var existingRoot in existingRoots)
                yield return existingRoot;
        }

        protected override void CompileInternal(string outputFile, ObjectDumper dumper)
        {
            _outputFile = outputFile;

            CorInfoImpl.JitStartCompilation();

            _dependencyGraph.ComputeMarkedNodes();

            foreach (var (_, corInfo) in _corinfos)
            {
                CorInfoImpl.JitFinishThreadContextBoundCompilation();
            }

            CorInfoImpl.JitFinishCompilation();

            LLVMObjectWriter.EmitObject(outputFile, _dependencyGraph.MarkedNodeList, this, dumper);
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
            CorInfoImpl corInfo = _corinfos.GetValue(Thread.CurrentThread, thread =>
            {
                string outputFilePath = Path.ChangeExtension(_outputFile, null) + "clrjit.bc";
                CorInfoImpl.JitStartThreadContextBoundCompilation(outputFilePath, Module.Target, Module.DataLayout);

                return new CorInfoImpl(this);
            });

            foreach (LLVMMethodCodeNode methodCodeNodeNeedingCode in methodsToCompile)
            {
                if (methodCodeNodeNeedingCode.StaticDependenciesAreComputed)
                    continue;

                if (Logger.IsVerbose)
                {
                    Logger.LogMessage($"Compiling {methodCodeNodeNeedingCode.Method}...");
                }

                CompileSingleMethod(corInfo, methodCodeNodeNeedingCode);
            }
        }

        private unsafe void CompileSingleMethod(CorInfoImpl corInfo, LLVMMethodCodeNode methodCodeNodeNeedingCode)
        {
            MethodDesc method = methodCodeNodeNeedingCode.Method;

            TypeSystemException exception = _methodImportationErrorProvider.GetCompilationError(method);

            // If we previously failed to import the method, do not try to import it again and go
            // directly to the error path.
            if (exception == null)
            {
                try
                {
                    CompileSingleMethodInternal(corInfo, methodCodeNodeNeedingCode);
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
                CompileSingleMethodInternal(corInfo, methodCodeNodeNeedingCode, throwingIL);

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

        private unsafe void CompileSingleMethodInternal(CorInfoImpl corInfo, LLVMMethodCodeNode methodCodeNodeNeedingCode, MethodIL methodIL = null)
        {
            // Add a declaration to this module. The method may be referenced in compiler's data structures.
            MethodDesc method = methodCodeNodeNeedingCode.Method;
            string mangledName = NodeFactory.NameMangler.GetMangledMethodName(method).ToString();
            LLVMValueRef externFunc = ILImporter.GetOrCreateLLVMFunction(Module, mangledName, method.Signature, method.RequiresInstArg());

            corInfo.CompileMethod(methodCodeNodeNeedingCode, methodIL);
            methodCodeNodeNeedingCode.CompilationCompleted = true;

            if (method.IsRuntimeExport)
            {
                ILImporter.GenerateRuntimeExportThunk(this, method, externFunc);
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
                signatureTypes.Add(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0)); // *MethodTable
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

        public override bool StructIsWrappedPrimitive(TypeDesc method, TypeDesc primitiveTypeDesc)
        {
            return ILImporter.StructIsWrappedPrimitive(method, primitiveTypeDesc);
        }

        public override int PadOffset(TypeDesc type, uint atOffset)
        {
            return ILImporter.PadOffset(type, (int)atOffset);
        }

        // We define an alternative entrypoint for the runtime exports, one that has the (original) managed calling convention.
        // This allows managed code as well as low-level runtime helpers to avoid the overhead of shadow stack save/restore
        // when calling the export. Thus, the "mangling" we use here is effectively an ABI contract between the compiler and
        // runtime.
        public override string GetRuntimeExportManagedEntrypointName(MethodDesc method)
        {
            if (!method.IsRuntimeExport)
            {
                return null;
            }

            return ((EcmaMethod)method).GetRuntimeExportName() + "_Managed";
        }

        public override ISymbolNode GetExternalMethodAccessor(MethodDesc method, ReadOnlySpan<TargetAbiType> sig)
        {
            Debug.Assert(!sig.IsEmpty);
            string name = PInvokeILProvider.GetDirectCallExternName(method);

            return NodeFactory.ExternSymbolWithAccessor(name, method, sig);
        }
    }
}
