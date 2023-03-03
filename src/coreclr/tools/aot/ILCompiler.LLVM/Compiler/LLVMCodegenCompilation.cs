// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;

using ILCompiler.DependencyAnalysis;
using ILCompiler.DependencyAnalysisFramework;
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
        private readonly Dictionary<TypeDesc, LLVMTypeRef> _llvmStructs = new();
        private readonly ConditionalWeakTable<Thread, CorInfoImpl> _corinfos = new();
        private string _outputFile;

        internal string Target { get; }
        internal string DataLayout { get; }
        internal string ModuleName { get; }
        internal ConfigurableWasmImportPolicy ConfigurableWasmImportPolicy { get; }
        public new LLVMCodegenNodeFactory NodeFactory { get; }

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
            : base(dependencyGraph, nodeFactory, GetCompilationRoots(roots, nodeFactory), ilProvider, debugInformationProvider, logger, devirtualizationManager, inliningPolicy, instructionSetSupport,
                null /* ProfileDataManager */, errorProvider, baseOptions, 1)
        {
            NodeFactory = nodeFactory;
            Target = options.Target;
            DataLayout = options.DataLayout;
            ModuleName = options.ModuleName;
            ConfigurableWasmImportPolicy = configurableWasmImportPolicy;
        }

        private static IEnumerable<ICompilationRootProvider> GetCompilationRoots(IEnumerable<ICompilationRootProvider> existingRoots, NodeFactory factory)
        {
            foreach (var existingRoot in existingRoots)
                yield return existingRoot;
        }

        protected override void CompileInternal(string outputFile, ObjectDumper dumper)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            _outputFile = outputFile;
            CorInfoImpl.JitStartCompilation();

            _dependencyGraph.ComputeMarkedNodes();

            foreach (var (_, corInfo) in _corinfos)
            {
                CorInfoImpl.JitFinishThreadContextBoundCompilation();
            }

            CorInfoImpl.JitFinishCompilation();

            LLVMObjectWriter.EmitObject(outputFile, _dependencyGraph.MarkedNodeList, this, dumper);

            Console.WriteLine($"LLVM bitcode generation finished in {stopwatch.Elapsed.TotalSeconds:0.##} seconds");
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
                CorInfoImpl.JitStartThreadContextBoundCompilation(outputFilePath, Target, DataLayout);

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
            corInfo.CompileMethod(methodCodeNodeNeedingCode, methodIL);
            methodCodeNodeNeedingCode.CompilationCompleted = true;
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

        internal LLVMTypeRef GetLLVMSignatureForMethod(MethodSignature signature, bool hasHiddenParam)
        {
            TypeDesc returnType = signature.ReturnType;
            LLVMTypeRef llvmReturnType;
            bool returnOnStack = false;
            if (!NeedsReturnStackSlot(signature))
            {
                returnOnStack = true;
                llvmReturnType = GetLLVMTypeForTypeDesc(returnType);
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
                if (CanStoreTypeOnStack(type))
                {
                    signatureTypes.Add(GetLLVMTypeForTypeDesc(type));
                }
            }

            return LLVMTypeRef.CreateFunction(llvmReturnType, signatureTypes.ToArray(), false);
        }

        internal static bool NeedsReturnStackSlot(MethodSignature signature)
        {
            return !signature.ReturnType.IsVoid && !CanStoreTypeOnStack(signature.ReturnType);
        }

        internal static bool CanStoreTypeOnStack(TypeDesc type)
        {
            if (type is DefType defType)
            {
                if (!defType.IsGCPointer && !defType.ContainsGCPointers && !ContainsIsByRef(type))
                {
                    return true;
                }
            }
            else if (type is PointerType || type is FunctionPointerType)
            {
                return true;
            }

            return false;
        }

        public override bool StructIsWrappedPrimitive(TypeDesc type, TypeDesc primitiveType)
        {
            Debug.Assert(type.IsValueType);
            Debug.Assert(primitiveType.IsPrimitive);

            if (type.GetElementSize().AsInt != primitiveType.GetElementSize().AsInt)
            {
                return false;
            }

            int instanceFieldCount = 0;
            bool foundPrimitive = false;

            foreach (FieldDesc field in type.GetFields())
            {
                if (field.IsStatic)
                {
                    continue;
                }

                instanceFieldCount++;

                // If there's more than one field, figuring out whether this is a primitive gets complicated, so assume it's not
                if (instanceFieldCount > 1)
                {
                    break;
                }

                TypeDesc fieldType = field.FieldType;
                if (fieldType == primitiveType)
                {
                    foundPrimitive = true;
                }
                else if (fieldType.IsValueType && !fieldType.IsPrimitive && StructIsWrappedPrimitive(fieldType, primitiveType))
                {
                    foundPrimitive = true;
                }
            }

            if (instanceFieldCount == 1 && foundPrimitive)
            {
                return true;
            }

            return false;
        }

        public override int PadOffset(TypeDesc type, int atOffset)
        {
            var fieldAlignment = type is DefType && type.IsValueType ? ((DefType)type).InstanceFieldAlignment : type.Context.Target.LayoutPointerSize;
            var alignment = LayoutInt.Min(fieldAlignment, new LayoutInt(ComputePackingSize(type))).AsInt;
            var padding = (atOffset + (alignment - 1)) & ~(alignment - 1);

            return padding;
        }

        internal int PadNextOffset(TypeDesc type, int atOffset)
        {
            return PadOffset(type, atOffset) + type.GetElementSize().AsInt;
        }

        internal LLVMTypeRef GetLLVMTypeForTypeDesc(TypeDesc type)
        {
            switch (type.Category)
            {
                case TypeFlags.Boolean:
                case TypeFlags.SByte:
                case TypeFlags.Byte:
                    return LLVMTypeRef.Int8;

                case TypeFlags.Int16:
                case TypeFlags.UInt16:
                case TypeFlags.Char:
                    return LLVMTypeRef.Int16;

                case TypeFlags.Int32:
                case TypeFlags.UInt32:
                    return LLVMTypeRef.Int32;

                case TypeFlags.IntPtr:
                case TypeFlags.UIntPtr:
                    return _nodeFactory.Target.PointerSize == 4 ? LLVMTypeRef.Int32 : LLVMTypeRef.Int64;

                case TypeFlags.Array:
                case TypeFlags.SzArray:
                case TypeFlags.ByRef:
                case TypeFlags.Class:
                case TypeFlags.Interface:
                case TypeFlags.Pointer:
                case TypeFlags.FunctionPointer:
                    return LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);

                case TypeFlags.Int64:
                case TypeFlags.UInt64:
                    return LLVMTypeRef.Int64;

                case TypeFlags.Single:
                    return LLVMTypeRef.Float;

                case TypeFlags.Double:
                    return LLVMTypeRef.Double;

                case TypeFlags.ValueType:
                case TypeFlags.Nullable:
                    {
                        if (!_llvmStructs.TryGetValue(type, out LLVMTypeRef llvmStructType))
                        {
                            // LLVM thinks certain sizes of struct have a different calling convention than Clang does.
                            // Treating them as ints fixes that and is more efficient in general
                            int structSize = type.GetElementSize().AsInt;
                            int structAlignment = ((DefType)type).InstanceFieldAlignment.AsInt;
                            switch (structSize)
                            {
                                case 1:
                                    llvmStructType = LLVMTypeRef.Int8;
                                    break;
                                case 2:
                                    if (structAlignment == 2)
                                    {
                                        llvmStructType = LLVMTypeRef.Int16;
                                    }
                                    else
                                    {
                                        goto default;
                                    }
                                    break;
                                case 4:
                                    if (structAlignment == 4)
                                    {
                                        if (StructIsWrappedPrimitive(type, type.Context.GetWellKnownType(WellKnownType.Single)))
                                        {
                                            llvmStructType = LLVMTypeRef.Float;
                                        }
                                        else
                                        {
                                            llvmStructType = LLVMTypeRef.Int32;
                                        }
                                    }
                                    else
                                    {
                                        goto default;
                                    }
                                    break;
                                case 8:
                                    if (structAlignment == 8)
                                    {
                                        if (StructIsWrappedPrimitive(type, type.Context.GetWellKnownType(WellKnownType.Double)))
                                        {
                                            llvmStructType = LLVMTypeRef.Double;
                                        }
                                        else
                                        {
                                            llvmStructType = LLVMTypeRef.Int64;
                                        }
                                    }
                                    else
                                    {
                                        goto default;
                                    }
                                    break;

                                default:
                                    // Forward-declare the struct in case there's a reference to it in the fields.
                                    // This must be a named struct or LLVM hits a stack overflow
                                    llvmStructType = LLVMTypeRef.Int8.Context.CreateNamedStruct(type.ToString());
                                    _llvmStructs[type] = llvmStructType;

                                    FieldDesc[] instanceFields = type.GetFields().Where(field => !field.IsStatic).ToArray();
                                    FieldAndOffset[] fieldLayout = new FieldAndOffset[instanceFields.Length];
                                    for (int i = 0; i < instanceFields.Length; i++)
                                    {
                                        fieldLayout[i] = new FieldAndOffset(instanceFields[i], instanceFields[i].Offset);
                                    }

                                    // Sort fields by offset and size in order to handle generating unions
                                    FieldAndOffset[] sortedFields = fieldLayout.OrderBy(fieldAndOffset => fieldAndOffset.Offset.AsInt).
                                        ThenByDescending(fieldAndOffset => fieldAndOffset.Field.FieldType.GetElementSize().AsInt).ToArray();

                                    List<LLVMTypeRef> llvmFields = new List<LLVMTypeRef>(sortedFields.Length);
                                    int lastOffset = -1;
                                    int nextNewOffset = -1;
                                    TypeDesc prevType = null;
                                    int totalSize = 0;

                                    foreach (FieldAndOffset fieldAndOffset in sortedFields)
                                    {
                                        int curOffset = fieldAndOffset.Offset.AsInt;

                                        if (prevType == null || (curOffset != lastOffset && curOffset >= nextNewOffset))
                                        {
                                            // The layout should be in order
                                            Debug.Assert(curOffset > lastOffset);

                                            int prevElementSize;
                                            if (prevType == null)
                                            {
                                                lastOffset = 0;
                                                prevElementSize = 0;
                                            }
                                            else
                                            {
                                                prevElementSize = prevType.GetElementSize().AsInt;
                                            }

                                            // Pad to this field if necessary
                                            int paddingSize = curOffset - lastOffset - prevElementSize;
                                            if (paddingSize > 0)
                                            {
                                                AddPaddingFields(paddingSize, llvmFields);
                                                totalSize += paddingSize;
                                            }

                                            TypeDesc fieldType = fieldAndOffset.Field.FieldType;
                                            int fieldSize = fieldType.GetElementSize().AsInt;

                                            llvmFields.Add(GetLLVMTypeForTypeDesc(fieldType));

                                            totalSize += fieldSize;
                                            lastOffset = curOffset;
                                            prevType = fieldType;
                                            nextNewOffset = curOffset + fieldSize;
                                        }
                                    }

                                    // If explicit layout is greater than the sum of fields, add padding
                                    if (totalSize < structSize)
                                    {
                                        AddPaddingFields(structSize - totalSize, llvmFields);
                                    }

                                    llvmStructType.StructSetBody(llvmFields.ToArray(), true);
                                    break;
                            }

                            _llvmStructs[type] = llvmStructType;
                        }
                        return llvmStructType;
                    }

                case TypeFlags.Enum:
                    return GetLLVMTypeForTypeDesc(type.UnderlyingType);

                case TypeFlags.Void:
                    return LLVMTypeRef.Void;

                default:
                    throw new UnreachableException(type.Category.ToString());
            }
        }

        private static bool ContainsIsByRef(TypeDesc type)
        {
            if (type.IsByRef || type.IsByRefLike)
            {
                return true;
            }

            foreach (var field in type.GetFields())
            {
                if (field.IsStatic)
                    continue;

                var fieldType = field.FieldType;
                if (fieldType.IsValueType)
                {
                    var fieldDefType = (DefType)fieldType;
                    if (!fieldDefType.ContainsGCPointers && !fieldDefType.IsByRefLike)
                        continue;

                    if (ContainsIsByRef(fieldType))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private static int ComputePackingSize(TypeDesc type)
        {
            if (type is MetadataType)
            {
                var metaType = type as MetadataType;
                var layoutMetadata = metaType.GetClassLayout();

                // If a type contains pointers then the metadata specified packing size is ignored (On desktop this is disqualification from ManagedSequential)
                if (layoutMetadata.PackingSize == 0 || metaType.ContainsGCPointers)
                    return type.Context.Target.DefaultPackingSize;
                else
                    return layoutMetadata.PackingSize;
            }
            else
            {
                return type.Context.Target.DefaultPackingSize;
            }
        }

        private static void AddPaddingFields(int paddingSize, List<LLVMTypeRef> llvmFields)
        {
            int numInts = paddingSize / 4;
            int numBytes = paddingSize - numInts * 4;
            for (int i = 0; i < numInts; i++)
            {
                llvmFields.Add(LLVMTypeRef.Int32);
            }
            for (int i = 0; i < numBytes; i++)
            {
                llvmFields.Add(LLVMTypeRef.Int8);
            }
        }
    }
}
