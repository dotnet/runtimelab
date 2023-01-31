// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using ILCompiler.Compiler;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;
using Internal.IL;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
using LLVMSharp.Interop;
using ILCompiler.LLVM;
using Internal.JitInterface;
using Internal.IL.Stubs;
using System.Runtime.InteropServices;
using LLVMSharp;

namespace ILCompiler
{
    public sealed class LLVMCodegenCompilation : RyuJitCompilation
    {
        [DllImport("libLLVM", EntryPoint = "LLVMContextSetOpaquePointers", CallingConvention = CallingConvention.Cdecl)]
        public static extern LLVMContextRef LLVMContextSetOpaquePointers(LLVMContextRef C, bool OpaquePointers);

        private readonly ConditionalWeakTable<Thread, CorInfoImpl> _corinfos = new ConditionalWeakTable<Thread, CorInfoImpl>();
        private string _outputFile;
        private readonly bool _disableRyuJit;

        // the LLVM Module generated from IL, can only be one.
        internal static LLVMModuleRef Module { get; private set; }
        internal LLVMTargetDataRef TargetData { get; }
        public new LLVMCodegenNodeFactory NodeFactory { get; }
        internal LLVMDIBuilderRef DIBuilder { get; }
        internal Dictionary<string, DebugMetadata> DebugMetadataMap { get; }
        internal bool NativeLib { get; }
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
            bool nativeLib,
            ConfigurableWasmImportPolicy configurableWasmImportPolicy,
            MethodImportationErrorProvider errorProvider)
            : base(dependencyGraph, nodeFactory, GetCompilationRoots(roots, nodeFactory), ilProvider, debugInformationProvider, logger, devirtualizationManager, inliningPolicy ?? new LLVMNoInLiningPolicy(), instructionSetSupport,
                null /* ProfileDataManager */, errorProvider, 0, 1)
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

            NativeLib = nativeLib;
            ConfigurableWasmImportPolicy = configurableWasmImportPolicy;
            _disableRyuJit = options.DisableRyuJit == "1"; // TODO-LLVM: delete when all code is compiled via RyuJIT
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

            CorInfoImpl.Shutdown(); // writes the LLVM bitcode
            CorInfoImpl.FreeUnmanagedResources();

            LLVMObjectWriter.EmitObject(outputFile, nodes, this, dumper);

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
                var methodIL = GetMethodIL(method);

                if (methodIL == null)
                {
                    if (method.IsInternalCall)
                    {
                        // this might have been the subject of a call in the clrjit module.  We need the function here so we can populate the symbol
                        var mangledName = NodeFactory.NameMangler.GetMangledMethodName(method).ToString();
                        var sig = method.Signature;
                        ILImporter.GetOrCreateLLVMFunction(Module, mangledName, GetLLVMSignatureForMethod(sig, method.RequiresInstArg()));
                    }

                    methodCodeNodeNeedingCode.CompilationCompleted = true;
                    return;
                }
                
                if (!_disableRyuJit)
                {
                    corInfo.RegisterLlvmCallbacks((IntPtr)Unsafe.AsPointer(ref corInfo), _outputFile, Module.Target, Module.DataLayout);

                    corInfo.CompileMethod(methodCodeNodeNeedingCode);
                    methodCodeNodeNeedingCode.CompilationCompleted = true;

                    // TODO: delete this external function when old module is gone
                    var mangledName = NodeFactory.NameMangler.GetMangledMethodName(method).ToString();
                    LLVMValueRef externFunc = ILImporter.GetOrCreateLLVMFunction(Module, mangledName, GetLLVMSignatureForMethod(method.Signature, method.RequiresInstArg()));

                    ILImporter.GenerateRuntimeExportThunk(this, method, externFunc);

                    ryuJitMethodCount++;
                }
                else
                {
                    ILImporter.CompileMethod(this, methodCodeNodeNeedingCode);
                }
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
    }
}
