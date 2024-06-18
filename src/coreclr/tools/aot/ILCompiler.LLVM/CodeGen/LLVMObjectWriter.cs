// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using ILCompiler.DependencyAnalysisFramework;

using Internal.JitInterface;
using Internal.JitInterface.LLVMInterop;
using Internal.Text;
using Internal.TypeSystem;
using ObjectData = ILCompiler.DependencyAnalysis.ObjectNode.ObjectData;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Object writer that emits LLVM (bitcode).
    /// </summary>
    internal sealed unsafe class LLVMObjectWriter
    {
        private readonly LLVMContext* _llvmContext;

        // Module with ILC-generated code and data.
        private readonly LLVMModuleRef _module;

        // Module with the external functions and getters for them. Must be separate from the main module so as to avoid
        // direct calls with mismatched signatures at the LLVM level.
        private readonly LLVMModuleRef _moduleWithExternalFunctions;

        private readonly LLVMType* _int8Type;
        private readonly LLVMType* _int32Type;
        private readonly LLVMType* _intPtrType;
        private readonly LLVMType* _ptrType;

        // Node factory and compilation for which ObjectWriter was instantiated.
        private readonly LLVMCodegenCompilation _compilation;
        private readonly NodeFactory _nodeFactory;

        // Path to the bitcode file we're emitting.
        private readonly string _objectFilePath;

        // Used for writing mangled names together with Utf8Name.
        private readonly Utf8StringBuilder _utf8StringBuilder = new Utf8StringBuilder();

        // List of global values to be kept alive via @llvm.used.
        private readonly List<LLVMValueRef> _keepAliveList = new();

        // Data emitted for the current object node. Initial capacity chosen to be the size of the largest node in a small program.
        private LLVMValueRef[] _currentObjectData = new LLVMValueRef[100_000];
        private LLVMTypeRef[] _currentObjectTypes = new LLVMTypeRef[100_000];

#if DEBUG
        private static readonly Dictionary<string, ISymbolNode> _previouslyWrittenNodeNames = new();
#endif

        private LLVMObjectWriter(string objectFilePath, LLVMCodegenCompilation compilation)
        {
            _llvmContext = compilation.LLVMContext;
            _int8Type = compilation.LLVMInt8Type;
            _int32Type = compilation.LLVMInt32Type;
            _intPtrType = compilation.NodeFactory.Target.PointerSize == 4 ? compilation.LLVMInt32Type : compilation.LLVMInt64Type;
            _ptrType = compilation.LLVMPtrType;

            string target = compilation.Options.Target;
            string dataLayout = compilation.Options.DataLayout;
            _module = LLVMModuleRef.Create(_llvmContext, "data"u8, target, dataLayout);
            _moduleWithExternalFunctions = LLVMModuleRef.Create(_llvmContext, "external"u8, target, dataLayout);

            _compilation = compilation;
            _nodeFactory = compilation.NodeFactory;
            _objectFilePath = objectFilePath;
        }

        public static void EmitObject(string objectFilePath, IEnumerable<DependencyNode> nodes, LLVMCodegenCompilation compilation, IObjectDumper dumper)
        {
            LLVMObjectWriter objectWriter = new LLVMObjectWriter(objectFilePath, compilation);
            NodeFactory factory = compilation.NodeFactory;

            try
            {
                foreach (DependencyNode depNode in nodes)
                {
                    ObjectNode node = depNode as ObjectNode;
                    if (node == null)
                        continue;

                    if (node.ShouldSkipEmittingObjectNode(factory))
                        continue;

                    switch (node)
                    {
                        case ReadyToRunGenericHelperNode readyToRunGenericHelperNode:
                            objectWriter.GetCodeForReadyToRunGenericHelper(readyToRunGenericHelperNode, factory);
                            continue;
                        case ReadyToRunHelperNode readyToRunHelperNode:
                            objectWriter.GetCodeForReadyToRunHelper(readyToRunHelperNode, factory);
                            continue;
                        case TentativeMethodNode tentativeMethodNode:
                            objectWriter.GetCodeForTentativeMethod(tentativeMethodNode, factory);
                            continue;
                        case UnboxingStubNode unboxStubNode:
                            objectWriter.GetCodeForUnboxThunkMethod(unboxStubNode);
                            continue;
                        case ExternMethodAccessorNode accessorNode:
                            objectWriter.GetCodeForExternMethodAccessor(accessorNode);
                            continue;
                        default:
                            break;
                    }

                    ObjectData nodeContents = node.GetData(factory);

                    dumper?.DumpObjectNode(factory, node, nodeContents);
#if DEBUG
                    foreach (ISymbolNode definedSymbol in nodeContents.DefinedSymbols)
                    {
                        try
                        {
                            _previouslyWrittenNodeNames.Add(definedSymbol.GetMangledName(factory.NameMangler), definedSymbol);
                        }
                        catch (ArgumentException)
                        {
                            ISymbolNode alreadyWrittenSymbol = _previouslyWrittenNodeNames[definedSymbol.GetMangledName(factory.NameMangler)];
                            Debug.Fail("Duplicate node name emitted to file",
                            $"Symbol {definedSymbol.GetMangledName(factory.NameMangler)} has already been written to the output object file {objectFilePath} with symbol {alreadyWrittenSymbol}");
                        }
                    }
#endif
                    objectWriter.EmitObjectNode(node, nodeContents);
                }

                objectWriter.FinishObjWriter();

                // Make sure we have released all memory used for mangling names.
                Debug.Assert(objectWriter._utf8StringBuilder.Length == 0);
            }
            catch
            {
                // If there was an exception while generating the OBJ file, make sure we don't leave the unfinished object file around.
                // TODO-LLVM: we also emit the results file, etc, should delete those too.
                try
                {
                    File.Delete(objectFilePath);
                }
                catch { }

                // Continue with the original exception.
                throw;
            }
        }

        private void EmitObjectNode(ObjectNode node, ObjectData nodeContents)
        {
            NodeFactory factory = _nodeFactory;
            int pointerSize = factory.Target.PointerSize;
            Span<LLVMTypeRef> typeElements = default;

            // All references to this symbol are through "ordinarily named" aliases. Thus, we need to suffix the real definition.
            ISymbolNode symbolNode = (ISymbolNode)node;
            using Utf8Name dataSymbolName = GetMangledUtf8Name(symbolNode, "__DATA");

            // Calculate the size of this object node.
            int dataSizeInBytes = nodeContents.Data.Length;
            int dataSizeInBytesAligned = dataSizeInBytes.AlignUp(pointerSize); // TODO-LLVM: do not pad out the data.
            int dataSizeInElements = dataSizeInBytesAligned / pointerSize;

            // If we need to create unaligned relocs then use a struct for the symbol.
            // Start with the premise that most nodes will just contain aligned relocs.
            bool useStruct = false;

            if (_currentObjectData.Length < dataSizeInElements)
            {
                Array.Resize(ref _currentObjectData, Math.Max(_currentObjectData.Length * 2, dataSizeInElements));
            }

            Span<LLVMValueRef> dataElements = _currentObjectData.AsSpan(0, dataSizeInElements);
            ReadOnlySpan<byte> data = nodeContents.Data.AsSpan();

            // Indicies in byte units to allow us to "zip" the binary data with the relocs
            int dataOffset = 0;
            int relocIndex = 0;
            int elementOffset = 0;

            int relocLength = nodeContents.Relocs.Length;
            int nextRelocOffset = 0;
            bool nextRelocValid = relocIndex < relocLength;
            if (nextRelocValid)
            {
                nextRelocOffset = nodeContents.Relocs[0].Offset;
            }

            while (dataOffset < dataSizeInBytes)
            {
                if (!useStruct && nextRelocValid && nextRelocOffset % pointerSize != 0)
                {
                    // Switch from array to struct. This will need more elements because binary data is output byte-by-byte.
                    useStruct = true;
                    dataSizeInElements = nodeContents.Relocs.Length + dataSizeInBytes - (nodeContents.Relocs.Length * pointerSize);

                    if (_currentObjectData.Length < dataSizeInElements)
                    {
                        Array.Resize(ref _currentObjectData, Math.Max(_currentObjectData.Length * 2, dataSizeInElements));
                    }
                    if (_currentObjectTypes.Length < dataSizeInElements)
                    {
                        Array.Resize(ref _currentObjectTypes, Math.Max(_currentObjectTypes.Length * 2, dataSizeInElements));
                    }

                    dataElements = _currentObjectData.AsSpan(0, dataSizeInElements);
                    typeElements = _currentObjectTypes.AsSpan(0, dataSizeInElements);

                    // Restart zipping while loop.
                    dataOffset = 0;
                    elementOffset = 0;
                    relocIndex = 0;

                    continue;
                }

                // Emit binary data until next reloc.  As some large nodes only contain binary data this is a small optimization.
                while (dataOffset < dataSizeInBytes && (!nextRelocValid || dataOffset < nextRelocOffset))
                {
                    if (useStruct)
                    {
                        typeElements[elementOffset] = _int8Type;
                        dataElements[elementOffset] = LLVMValueRef.CreateConstInt(_int8Type, data[dataOffset]);
                        dataOffset++;
                    }
                    else
                    {
                        ulong value = 0;
                        int size = Math.Min(dataSizeInBytes - dataOffset, pointerSize);
                        data.Slice(dataOffset, size).CopyTo(new Span<byte>(&value, size));
                        dataElements[elementOffset] = LLVMValueRef.CreateConstIntToPtr(LLVMValueRef.CreateConstInt(_intPtrType, value));
                        dataOffset += pointerSize;
                    }
                    elementOffset++;
                }

                if (nextRelocValid)
                {
                    Relocation reloc = nodeContents.Relocs[relocIndex];
                    Debug.Assert(IsSupportedRelocType(node, reloc.RelocType), $"{reloc.RelocType} in {node} not supported");

                    long delta;
                    fixed (void* location = &data[reloc.Offset])
                    {
                        delta = Relocation.ReadValue(reloc.RelocType, location);
                    }

                    ISymbolNode symbolRefNode = reloc.Target;
                    if (symbolRefNode is EETypeNode eeTypeNode && eeTypeNode.ShouldSkipEmittingObjectNode(factory))
                    {
                        symbolRefNode = factory.ConstructedTypeSymbol(eeTypeNode.Type);
                    }

                    dataElements[elementOffset] = GetSymbolReferenceValue(symbolRefNode, checked((int)delta));
                    if (useStruct)
                    {
                        typeElements[elementOffset] = _ptrType;
                    }

                    relocIndex++;
                    nextRelocValid = relocIndex < relocLength;
                    if (nextRelocValid)
                    {
                        nextRelocOffset = nodeContents.Relocs[relocIndex].Offset;
                    }

                    dataOffset += pointerSize;
                    elementOffset++;
                }
            }

            Debug.Assert(relocIndex == relocLength);

            // Create and initialize the LLVM global value.
            LLVMTypeRef dataSymbolType = useStruct
                ? LLVMTypeRef.CreateStruct(_llvmContext, typeElements, true)
                : LLVMTypeRef.CreateArray(_ptrType, (uint)dataSizeInElements);
            LLVMValueRef dataSymbolValue = useStruct
                ? LLVMValueRef.CreateConstStruct(dataSymbolType, dataElements)
                : LLVMValueRef.CreateConstArray(dataSymbolType, dataElements);

            LLVMValueRef dataSymbol = _module.AddGlobal(dataSymbolName, dataSymbolType, dataSymbolValue);
            dataSymbol.Alignment = (uint)nodeContents.Alignment;
            if (GetObjectNodeSection(node) is string section)
            {
                dataSymbol.Section = section;
            }

            foreach (ISymbolDefinitionNode definedSymbol in nodeContents.DefinedSymbols)
            {
                using Utf8Name definedSymbolName = GetMangledUtf8Name(definedSymbol);
                int definedSymbolOffset = definedSymbol.Offset;
                EmitSymbolDef(dataSymbol, definedSymbolName, definedSymbolOffset);

                string alternateDefinedSymbolName = factory.GetSymbolAlternateName(definedSymbol);
                if (alternateDefinedSymbolName != null)
                {
                    using Utf8Name alternateDefinedSymbolUtf8Name = GetUtf8Name(alternateDefinedSymbolName);
                    EmitSymbolDef(dataSymbol, alternateDefinedSymbolUtf8Name, definedSymbolOffset);
                }
            }

            if (ObjectNodeMustBeArtificiallyKeptAlive(node))
            {
                _keepAliveList.Add(dataSymbol);
            }
        }

        private void EmitSymbolDef(LLVMValueRef baseSymbol, ReadOnlySpan<byte> symbolIdentifier, int offsetFromBaseSymbol)
        {
            LLVMValueRef symbolAddress = baseSymbol;
            if (offsetFromBaseSymbol != 0)
            {
                symbolAddress = LLVMValueRef.CreateConstGEP(symbolAddress, offsetFromBaseSymbol);
            }

            LLVMValueRef symbolDef = _module.GetNamedAlias(symbolIdentifier);
            if (symbolDef.Handle == null)
            {
                _module.AddAlias(symbolIdentifier, _int8Type, symbolAddress);
            }
            else
            {
                // Set the aliasee.
                symbolDef.Aliasee = symbolAddress;
            }
        }

        private string GetObjectNodeSection(ObjectNode node)
        {
            ObjectNodeSection section = node.GetSection(_nodeFactory);

            // We do not want to just "return section.Name" because it forces LLVM to:
            // 1. Lay out symbols such that there must not be alignment holes between them.
            // 2. Put everything into the (few) specified sections, making linker GC effectively useless.
            // At the same time, the semantics of which section directions are correctness-bearing are not well-defined.
            // For now, "IsStandardSection" is sufficient...
            //
            return section.IsStandardSection ? null : section.Name;
        }

        private static bool IsSupportedRelocType(ObjectNode node, RelocType type)
        {
            if (node is StackTraceMethodMappingNode)
            {
                // Stack trace metadata uses relative pointers, but is currently unused.
                return true;
            }
            return type is RelocType.IMAGE_REL_BASED_HIGHLOW;
        }

        private static bool ObjectNodeMustBeArtificiallyKeptAlive(ObjectNode node)
        {
            // The modules section is referenced through the special __start/__stop
            // symbols, which don't cause the linker to consider it alive by default.
            return node is ModulesSectionNode;
        }

        private void FinishObjWriter()
        {
            EmitKeepAliveList();

#if DEBUG
            _module.PrintToFile(Path.ChangeExtension(_objectFilePath, ".txt"));
            _module.Verify();

            _moduleWithExternalFunctions.PrintToFile(Path.ChangeExtension(_objectFilePath, "external.txt"));
            _moduleWithExternalFunctions.Verify();
#endif

            string dataLlvmObjectPath = _objectFilePath;
            _module.WriteBitcodeToFile(dataLlvmObjectPath);

            string externalLlvmObjectPath = Path.ChangeExtension(_objectFilePath, "external.bc");
            _moduleWithExternalFunctions.WriteBitcodeToFile(externalLlvmObjectPath);

            LLVMCompilationResults compilationResults = _compilation.GetCompilationResults();
            compilationResults.Add(dataLlvmObjectPath);
            compilationResults.Add(externalLlvmObjectPath);
            compilationResults.SerializeToFile(Path.ChangeExtension(_objectFilePath, "results.txt"));
        }

        private void EmitKeepAliveList()
        {
            // See https://llvm.org/docs/LangRef.html#the-llvm-used-global-variable.
            ReadOnlySpan<LLVMValueRef> llvmUsedSymbols = CollectionsMarshal.AsSpan(_keepAliveList);
            if (llvmUsedSymbols.Length != 0)
            {
                LLVMTypeRef llvmUsedType = LLVMTypeRef.CreateArray(_ptrType, (uint)llvmUsedSymbols.Length);
                LLVMValueRef llvmUsedValue = LLVMValueRef.CreateConstArray(llvmUsedType, llvmUsedSymbols);
                LLVMValueRef llvmUsedGlobal = _module.AddGlobal("llvm.used"u8, llvmUsedType, llvmUsedValue);
                llvmUsedGlobal.Linkage = LLVMLinkage.LLVMAppendingLinkage;
                llvmUsedGlobal.Section = "llvm.metadata";
            }
        }

        private void GetCodeForReadyToRunGenericHelper(ReadyToRunGenericHelperNode node, NodeFactory factory)
        {
            if (node.Id == ReadyToRunHelperId.DelegateCtor)
            {
                GetCodeForDelegateCtorHelper(node);
                return;
            }

            using Utf8Name helperFuncName = GetMangledUtf8Name(node);
            LLVMTypeRef helperFuncType = LLVMTypeRef.CreateFunction(
                _ptrType, [_ptrType /* shadow stack */, _ptrType /* generic context */]);
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(helperFuncName, helperFuncType);

            using LLVMBuilderRef builder = LLVMBuilderRef.Create(_llvmContext);
            builder.PositionAtEnd(helperFunc.AppendBasicBlock("GenericReadyToRunHelper"u8));

            LLVMValueRef dictionary = OutputCodeForGetGenericDictionary(builder, helperFunc.GetParam(1), node);
            LLVMValueRef result = OutputCodeForDictionaryLookup(builder, factory, node, node.LookupSignature, dictionary);

            switch (node.Id)
            {
                case ReadyToRunHelperId.GetNonGCStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

                        if (TriggersLazyStaticConstructor(factory, target))
                        {
                            OutputCodeForTriggerCctor(builder, result);
                        }
                    }
                    break;

                case ReadyToRunHelperId.GetGCStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

                        if (TriggersLazyStaticConstructor(factory, target))
                        {
                            GenericLookupResult nonGcBaseLookup = factory.GenericLookup.TypeNonGCStaticBase(target);
                            LLVMValueRef nonGcStaticsBase = OutputCodeForDictionaryLookup(builder, factory, node, nonGcBaseLookup, dictionary);
                            OutputCodeForTriggerCctor(builder, nonGcStaticsBase);
                        }

                        result = builder.BuildLoad(_ptrType, result, "gcBase"u8);
                    }
                    break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

                        LLVMValueRef nonGcStaticsBase = default;
                        if (TriggersLazyStaticConstructor(factory, target))
                        {
                            GenericLookupResult nonGcBaseLookup = factory.GenericLookup.TypeNonGCStaticBase(target);
                            nonGcStaticsBase = OutputCodeForDictionaryLookup(builder, factory, node, nonGcBaseLookup, dictionary);
                        }

                        result = OutputCodeForGetThreadStaticBase(builder, result, nonGcStaticsBase);
                    }
                    break;

                // These are all simple: just get the thing from the dictionary and we're done
                case ReadyToRunHelperId.TypeHandle:
                case ReadyToRunHelperId.TypeHandleForCasting:
                case ReadyToRunHelperId.MethodHandle:
                case ReadyToRunHelperId.FieldHandle:
                case ReadyToRunHelperId.MethodDictionary:
                case ReadyToRunHelperId.MethodEntry:
                case ReadyToRunHelperId.VirtualDispatchCell:
                case ReadyToRunHelperId.DefaultConstructor:
                case ReadyToRunHelperId.ObjectAllocator:
                case ReadyToRunHelperId.ConstrainedDirectCall:
                    break;

                default:
                    throw new NotImplementedException();
            }

            builder.BuildRet(result);
        }

        private static bool TriggersLazyStaticConstructor(NodeFactory factory, TypeDesc type)
        {
            return factory.PreinitializationManager.HasLazyStaticConstructor(type.ConvertToCanonForm(CanonicalFormKind.Specific));
        }

        private void GetCodeForReadyToRunHelper(ReadyToRunHelperNode node, NodeFactory factory)
        {
            if (node.Id == ReadyToRunHelperId.VirtualCall)
            {
                GetCodeForVirtualCallHelper(node, factory);
                return;
            }
            if (node.Id == ReadyToRunHelperId.DelegateCtor)
            {
                GetCodeForDelegateCtorHelper(node);
                return;
            }

            LLVMTypeRef helperFuncType = node.Id == ReadyToRunHelperId.ResolveVirtualFunction
                ? LLVMTypeRef.CreateFunction(_ptrType, [_ptrType, /* this */ _ptrType])
                : LLVMTypeRef.CreateFunction(_ptrType, [_ptrType]);
            using Utf8Name helperFuncName = GetMangledUtf8Name(node);
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(helperFuncName, helperFuncType);

            using LLVMBuilderRef builder = LLVMBuilderRef.Create(_llvmContext);
            builder.PositionAtEnd(helperFunc.AppendBasicBlock("ReadyToRunHelper"u8));

            LLVMValueRef result;
            switch (node.Id)
            {
                case ReadyToRunHelperId.GetNonGCStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

                        result = GetSymbolReferenceValue(factory.TypeNonGCStaticsSymbol(target));

                        if (_compilation.HasLazyStaticConstructor(target))
                        {
                            OutputCodeForTriggerCctor(builder, result);
                        }
                    }
                    break;

                case ReadyToRunHelperId.GetGCStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

                        if (_compilation.HasLazyStaticConstructor(target))
                        {
                            LLVMValueRef nonGcBase = GetSymbolReferenceValue(factory.TypeNonGCStaticsSymbol(target));
                            OutputCodeForTriggerCctor(builder, nonGcBase);
                        }

                        result = GetSymbolReferenceValue(factory.TypeGCStaticsSymbol(target));
                        result = builder.BuildLoad(_ptrType, result, "gcBase"u8);
                    }
                    break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

                        LLVMValueRef nonGcBase = default;
                        if (_compilation.HasLazyStaticConstructor(target))
                        {
                            nonGcBase = GetSymbolReferenceValue(factory.TypeNonGCStaticsSymbol(target));
                        }

                        LLVMValueRef pModuleDataSlot = GetSymbolReferenceValue(factory.TypeThreadStaticIndex(target));
                        result = OutputCodeForGetThreadStaticBase(builder, pModuleDataSlot, nonGcBase);
                    }
                    break;

                case ReadyToRunHelperId.ResolveVirtualFunction:
                    {
                        MethodDesc targetMethod = (MethodDesc)node.Target;
                        LLVMValueRef objThis = helperFunc.GetParam(1);

                        if (targetMethod.OwningType.IsInterface)
                        {
                            // TODO-LLVM: would be nice to use pointers instead of IntPtr in "RhpResolveInterfaceMethod".
                            LLVMTypeRef resolveFuncType = LLVMTypeRef.CreateFunction(_intPtrType, [_ptrType, _ptrType, _intPtrType]);
                            LLVMValueRef resolveFunc = GetOrCreateLLVMFunction("RhpResolveInterfaceMethod"u8, resolveFuncType);

                            LLVMValueRef cell = GetSymbolReferenceValue(factory.InterfaceDispatchCell(targetMethod));
                            LLVMValueRef cellArg = builder.BuildPtrToInt(cell, _intPtrType, "cellArg"u8);
                            result = CreateCall(builder, resolveFunc, [helperFunc.GetParam(0), objThis, cellArg]);
                            result = builder.BuildIntToPtr(result, "pInterfaceFunc"u8);
                        }
                        else
                        {
                            Debug.Assert(!targetMethod.CanMethodBeInSealedVTable(factory));
                            result = OutputCodeForVTableLookup(builder, objThis, targetMethod);
                        }
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }

            builder.BuildRet(result);
        }

        private void GetCodeForVirtualCallHelper(ReadyToRunHelperNode node, NodeFactory factory)
        {
            MethodDesc targetMethod = (MethodDesc)node.Target;
            Debug.Assert(!targetMethod.OwningType.IsInterface);
            Debug.Assert(!targetMethod.CanMethodBeInSealedVTable(factory));
            Debug.Assert(!targetMethod.RequiresInstArg());

            using Utf8Name helperFuncName = GetMangledUtf8Name(node);
            LLVMTypeRef funcType = _compilation.GetLLVMSignatureForMethod(targetMethod.Signature, hasHiddenParam: false);
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(helperFuncName, funcType);

            using LLVMBuilderRef builder = LLVMBuilderRef.Create(_llvmContext);
            builder.PositionAtEnd(helperFunc.AppendBasicBlock("VirtualCall"u8));

            int argsCount = (int)helperFunc.ParamsCount;
            Span<LLVMValueRef> args = argsCount > 100 ? new LLVMValueRef[argsCount] : stackalloc LLVMValueRef[argsCount];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = helperFunc.GetParam((uint)i);
            }

            LLVMValueRef pTargetMethod = OutputCodeForVTableLookup(builder, helperFunc.GetParam(1), targetMethod);
            LLVMValueRef callTarget = builder.BuildCall(funcType, pTargetMethod, args);
            if (funcType.ReturnType != _compilation.LLVMVoidType)
            {
                builder.BuildRet(callTarget);
            }
            else
            {
                builder.BuildRetVoid();
            }
        }

        private void GetCodeForDelegateCtorHelper(AssemblyStubNode node)
        {
            NodeFactory factory = _nodeFactory;
            ReadyToRunGenericHelperNode genericNode = node as ReadyToRunGenericHelperNode;

            DelegateCreationInfo delegateCreationInfo;
            scoped Span<LLVMTypeRef> helperFuncParams;
            if (genericNode != null)
            {
                delegateCreationInfo = (DelegateCreationInfo)genericNode.Target;
                helperFuncParams = [_ptrType, _ptrType, _ptrType, _ptrType];
            }
            else
            {
                delegateCreationInfo = (DelegateCreationInfo)((ReadyToRunHelperNode)node).Target;
                helperFuncParams = [_ptrType, _ptrType, _ptrType];
            }

            using Utf8Name helperFuncName = GetMangledUtf8Name(node);
            LLVMTypeRef helperFuncType = LLVMTypeRef.CreateFunction(_compilation.LLVMVoidType, helperFuncParams);
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(helperFuncName, helperFuncType);

            // Incoming parameters are: (SS, this, targetObj, [GenericContext]).
            // Outgoing parameters are: (SS, this, targetObj, pTarget, [InvokeThunk]).
            // Thus, our main responsibility is to compute the target.
            //
            LLVMValueRef initializerFunc = GetOrCreateLLVMFunction(delegateCreationInfo.Constructor);
            ReadOnlySpan<LLVMTypeRef> initializerFuncParamTypes = initializerFunc.GetValueType().ParamTypes;

            using LLVMBuilderRef builder = LLVMBuilderRef.Create(_llvmContext);
            builder.PositionAtEnd(helperFunc.AppendBasicBlock(genericNode != null ? "GenericDelegateCtor"u8 : "DelegateCtor"u8));

            LLVMValueRef targetObj = helperFunc.GetParam(2);
            LLVMTypeRef targetValueType = initializerFuncParamTypes[3];
            LLVMValueRef targetValue;
            if (genericNode != null)
            {
                LLVMValueRef dictionary = OutputCodeForGetGenericDictionary(builder, helperFunc.GetParam(3), genericNode);
                targetValue = OutputCodeForDictionaryLookup(builder, factory, genericNode, genericNode.LookupSignature, dictionary);
            }
            else if (delegateCreationInfo.TargetNeedsVTableLookup)
            {
                targetValue = OutputCodeForVTableLookup(builder, targetObj, delegateCreationInfo.TargetMethod);
            }
            else
            {
                targetValue = GetSymbolReferenceValue(delegateCreationInfo.GetTargetNode(factory));
            }

            if (targetValue.TypeOf != targetValueType)
            {
                targetValue = builder.BuildPointerCast(targetValue, targetValueType, "pTarget"u8);
            }

            Span<LLVMValueRef> initializerArgs = stackalloc LLVMValueRef[initializerFuncParamTypes.Length];
            initializerArgs[0] = helperFunc.GetParam(0);
            initializerArgs[1] = helperFunc.GetParam(1);
            initializerArgs[2] = targetObj;
            initializerArgs[3] = targetValue;
            if (delegateCreationInfo.Thunk != null)
            {
                LLVMValueRef thunkValue = GetOrCreateLLVMFunction(delegateCreationInfo.Thunk);
                initializerArgs[4] = builder.BuildPointerCast(thunkValue, initializerFuncParamTypes[4]);
            }

            CreateCall(builder, initializerFunc, initializerArgs);
            builder.BuildRetVoid();
        }

        private void GetCodeForTentativeMethod(TentativeMethodNode node, NodeFactory factory)
        {
            LLVMValueRef tentativeStub = GetOrCreateLLVMFunction(node);
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(node.GetTarget(factory));

            using LLVMBuilderRef builder = LLVMBuilderRef.Create(_llvmContext);
            LLVMBasicBlockRef block = tentativeStub.AppendBasicBlock("TentativeStub"u8);
            builder.PositionAtEnd(block);

            CreateCall(builder, helperFunc, [tentativeStub.GetParam(0)]);
            CreateUnreachedAfterAlwaysThrowCall(builder);
        }

        private void GetCodeForUnboxThunkMethod(UnboxingStubNode node)
        {
            NodeFactory factory = _compilation.NodeFactory;

            // This is the regular unboxing thunk that just does "Target(ref @this.Data, args...)".
            // Note how we perform a shadow tail call here while simultaneously overwriting "this".
            //
            LLVMValueRef unboxingLlvmFunc = GetOrCreateLLVMFunction(node);
            LLVMValueRef unboxedLlvmFunc = GetOrCreateLLVMFunction(node.GetUnderlyingMethodEntrypoint(factory));
            Debug.Assert(unboxingLlvmFunc.ParamsCount == unboxedLlvmFunc.ParamsCount);

            using LLVMBuilderRef builder = LLVMBuilderRef.Create(_llvmContext);
            builder.PositionAtEnd(unboxingLlvmFunc.AppendBasicBlock("SimpleUnboxingThunk"u8));

            int argsCount = (int)unboxingLlvmFunc.ParamsCount;
            Span<LLVMValueRef> args = argsCount > 100 ? new LLVMValueRef[argsCount] : stackalloc LLVMValueRef[argsCount];
            for (int i = 0; i < args.Length; i++)
            {
                LLVMValueRef arg = unboxingLlvmFunc.GetParam((uint)i);
                if (i == 1)
                {
                    // Adjust "this" by the method table offset.
                    arg = CreateAddOffset(builder, arg, factory.Target.PointerSize, "dataThis"u8);
                }

                args[i] = arg;
            }

            LLVMValueRef unboxedCall = CreateCall(builder, unboxedLlvmFunc, args);
            if (unboxedCall.TypeOf != _compilation.LLVMVoidType)
            {
                builder.BuildRet(unboxedCall);
            }
            else
            {
                builder.BuildRetVoid();
            }
        }

        private void GetCodeForExternMethodAccessor(ExternMethodAccessorNode node)
        {
            LLVMTypeRef externFuncType;
            if (node.Signature != null)
            {
                LLVMTypeRef GetLlvmType(TargetAbiType abiType) => abiType switch
                {
                    TargetAbiType.Void => _compilation.LLVMVoidType,
                    TargetAbiType.Int32 => _int32Type,
                    TargetAbiType.Int64 => _compilation.LLVMInt64Type,
                    TargetAbiType.Float => _compilation.LLVMFloatType,
                    TargetAbiType.Double => _compilation.LLVMDoubleType,
                    _ => throw new UnreachableException()
                };

                TargetAbiType[] sig = node.Signature;
                LLVMTypeRef returnType = GetLlvmType(sig[0]);
                LLVMTypeRef[] paramTypes = new LLVMTypeRef[sig.Length - 1];
                for (int i = 0; i < paramTypes.Length; i++)
                {
                    paramTypes[i] = GetLlvmType(sig[i + 1]);
                }

                externFuncType = LLVMTypeRef.CreateFunction(returnType, paramTypes);
            }
            else // Report the signature mismatch warning, use a placeholder signature.
            {
                string text = $"Signature mismatch detected: '{node.ExternMethodName}' will not be imported from the host environment";

                foreach (MethodDesc method in node.EnumerateMethods())
                {
                    text += $"\n Defined as: {method.Signature.ReturnType} {method}";
                }

                // Error code is just below the "AOT analysis" namespace.
                _compilation.Logger.LogWarning(text, 3049, Path.GetFileNameWithoutExtension(_objectFilePath));

                externFuncType = LLVMTypeRef.CreateFunction(_compilation.LLVMVoidType, []);
            }

            ReadOnlySpan<byte> externFuncName = node.ExternMethodName.AsSpan();
            LLVMModuleRef externFuncModule = _moduleWithExternalFunctions;
            Debug.Assert(externFuncModule.GetNamedFunction(externFuncName).Handle == null);
            LLVMValueRef externFunc = externFuncModule.AddFunction(externFuncName, externFuncType);

            // Add import attributes if specified.
            if (node.Signature != null && _compilation.PInvokeILProvider.GetWasmImportCallInfo(node.GetSingleMethod(), out string externName, out string moduleName))
            {
                externFunc.AddFunctionAttribute("wasm-import-module"u8, moduleName);
                externFunc.AddFunctionAttribute("wasm-import-name"u8, externName);
            }

            // Define the accessor function.
            using Utf8Name accessorFuncName = GetMangledUtf8Name(node);
            LLVMTypeRef accessorFuncType = LLVMTypeRef.CreateFunction(externFunc.TypeOf, Array.Empty<LLVMTypeRef>());
            LLVMValueRef accessorFunc = externFuncModule.AddFunction(accessorFuncName, accessorFuncType);

            using LLVMBuilderRef builder = LLVMBuilderRef.Create(_llvmContext);
            LLVMBasicBlockRef block = accessorFunc.AppendBasicBlock("GetExternFunc"u8);
            builder.PositionAtEnd(block);
            builder.BuildRet(externFunc);
        }

        private LLVMValueRef OutputCodeForGetGenericDictionary(LLVMBuilderRef builder, LLVMValueRef context, ReadyToRunGenericHelperNode node)
        {
            LLVMValueRef dictionary;
            if (node is ReadyToRunGenericLookupFromTypeNode)
            {
                // Locate the VTable slot that points to the dictionary
                int vtableSlot = VirtualMethodSlotHelper.GetGenericDictionarySlot(_nodeFactory, (TypeDesc)node.DictionaryOwner);

                // Load the dictionary pointer from the VTable
                int pointerSize = _nodeFactory.Target.PointerSize;
                int slotOffset = EETypeNode.GetVTableOffset(pointerSize) + (vtableSlot * pointerSize);
                var slotAddr = CreateAddOffset(builder, context, slotOffset, "dictionarySlotAddr"u8);
                dictionary = builder.BuildLoad(_ptrType, slotAddr, "dictionary"u8);
            }
            else
            {
                dictionary = context;
            }

            return dictionary;
        }

        private LLVMValueRef OutputCodeForDictionaryLookup(LLVMBuilderRef builder, NodeFactory factory,
            ReadyToRunGenericHelperNode node, GenericLookupResult lookup, LLVMValueRef dictionary)
        {
            // Find the generic dictionary slot
            if (!factory.GenericDictionaryLayout(node.DictionaryOwner).TryGetSlotForEntry(lookup, out int dictionarySlot))
            {
                return LLVMValueRef.CreateConstNull(_ptrType);
            }
            int offset = dictionarySlot * factory.Target.PointerSize;

            // Load the generic dictionary cell
            LLVMValueRef slotAddr = CreateAddOffset(builder, dictionary, offset, "dictionarySlotAddr"u8);
            LLVMValueRef lookupResult = builder.BuildLoad(_ptrType, slotAddr, "slotValue"u8);

            if (node.HandlesInvalidEntries(factory))
            {
                LLVMBasicBlockRef currentBlock = builder.InsertBlock;
                LLVMValueRef currentFunc = currentBlock.Parent;
                LLVMBasicBlockRef slotNotAvailableBlock = currentFunc.AppendBasicBlock("DictionarySlotNotAvailable"u8);
                LLVMBasicBlockRef slotAvailableBlock = currentFunc.AppendBasicBlock("DictionarySlotAvailable"u8);
                slotAvailableBlock.MoveAfter(currentBlock);

                LLVMValueRef nullValue = LLVMValueRef.CreateConstNull(_ptrType);
                LLVMValueRef isSlotNotAvailable = builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, lookupResult, nullValue, "isSlotNotAvailable"u8);
                builder.BuildCondBr(isSlotNotAvailable, slotNotAvailableBlock, slotAvailableBlock);

                LLVMValueRef slotNotAvailableFunc = GetOrCreateLLVMFunction(ReadyToRunGenericHelperNode.GetBadSlotHelper(factory));
                builder.PositionAtEnd(slotNotAvailableBlock);
                CreateCall(builder, slotNotAvailableFunc, [currentFunc.GetParam(0)]);
                CreateUnreachedAfterAlwaysThrowCall(builder);

                builder.PositionAtEnd(slotAvailableBlock);
            }

            return lookupResult;
        }

        private LLVMValueRef OutputCodeForVTableLookup(LLVMBuilderRef builder, LLVMValueRef objThis, MethodDesc method)
        {
            LLVMValueRef pMethodTable = builder.BuildLoad(_ptrType, objThis, "pMethodTable"u8);

            int slot = VirtualMethodSlotHelper.GetVirtualMethodSlot(_nodeFactory, method, method.OwningType);
            int slotOffset = EETypeNode.GetVTableOffset(_nodeFactory.Target.PointerSize) + (slot * _nodeFactory.Target.PointerSize);
            LLVMValueRef pSlot = CreateAddOffset(builder, pMethodTable, slotOffset, "pSlot"u8);
            LLVMValueRef slotValue = builder.BuildLoad(_ptrType, pSlot, "pTarget"u8);

            return slotValue;
        }

        private LLVMValueRef OutputCodeForGetCctorContext(LLVMBuilderRef builder, LLVMValueRef nonGcStaticBaseValue)
        {
            // We need to trigger the cctor before returning the base. It is stored at the beginning of the non-GC statics region.
            int pContextOffset = -NonGCStaticsNode.GetClassConstructorContextSize(_nodeFactory.Target);
            LLVMValueRef pContext = CreateAddOffset(builder, nonGcStaticBaseValue, pContextOffset, "pContext"u8);
            return pContext;
        }

        private void OutputCodeForTriggerCctor(LLVMBuilderRef builder, LLVMValueRef nonGcStaticBaseValue)
        {
            LLVMValueRef pContext = OutputCodeForGetCctorContext(builder, nonGcStaticBaseValue);
            LLVMValueRef initialized = builder.BuildLoad(_intPtrType, pContext, "initialized"u8);
            LLVMValueRef isInitialized = builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, initialized, CreateConst(_intPtrType, 0), "isInitialized"u8);

            LLVMValueRef func = builder.InsertBlock.Parent;
            LLVMBasicBlockRef callHelperBlock = func.AppendBasicBlock("CallHelper"u8);
            LLVMBasicBlockRef returnBlock = func.AppendBasicBlock("Return"u8);
            builder.BuildCondBr(isInitialized, returnBlock, callHelperBlock);

            builder.PositionAtEnd(callHelperBlock);
            IMethodNode helperFuncNode = (IMethodNode)_nodeFactory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnNonGCStaticBase);
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(helperFuncNode);
            LLVMValueRef nonGcStaticBaseValueArg = builder.BuildPtrToInt(nonGcStaticBaseValue, _intPtrType);

            CreateCall(builder, helperFunc, [func.GetParam(0), pContext, nonGcStaticBaseValueArg]);
            builder.BuildBr(returnBlock);

            builder.PositionAtEnd(returnBlock);
        }

        private LLVMValueRef OutputCodeForGetThreadStaticBase(LLVMBuilderRef builder, LLVMValueRef pModuleDataSlot, LLVMValueRef nonGcStaticBaseValue)
        {
            LLVMValueRef shadowStack = builder.InsertBlock.Parent.GetParam(0);

            // First arg: address of the TypeManager slot that provides the helper with information about
            // module index and the type manager instance (which is used for initialization on first access).
            LLVMValueRef pModuleData = builder.BuildLoad(_ptrType, pModuleDataSlot, "pModuleData"u8);

            // Second arg: index of the type in the ThreadStatic section of the modules.
            LLVMValueRef pTypeTlsIndex = CreateAddOffset(builder, pModuleDataSlot, _nodeFactory.Target.PointerSize, "pTypeTlsIndex"u8);
            LLVMValueRef typeTlsIndex = builder.BuildLoad(_int32Type, pTypeTlsIndex, "typeTlsIndex"u8);

            HelperEntrypoint helper;
            scoped Span<LLVMValueRef> getBaseHelperArgs;
            if (nonGcStaticBaseValue.Handle != null)
            {
                LLVMValueRef pContext = OutputCodeForGetCctorContext(builder, nonGcStaticBaseValue);

                helper = HelperEntrypoint.EnsureClassConstructorRunAndReturnThreadStaticBase;
                getBaseHelperArgs = [shadowStack, pModuleData, typeTlsIndex, pContext];
            }
            else
            {
                helper = HelperEntrypoint.GetThreadStaticBaseForType;
                getBaseHelperArgs = [shadowStack, pModuleData, typeTlsIndex];
            }
            IMethodNode getBaseHelperNode = (IMethodNode)_nodeFactory.HelperEntrypoint(helper);
            LLVMValueRef getBaseHelperFunc = GetOrCreateLLVMFunction(getBaseHelperNode);
            LLVMValueRef getBaseHelperCall = CreateCall(builder, getBaseHelperFunc, getBaseHelperArgs);

            return getBaseHelperCall;
        }

        private LLVMValueRef GetOrCreateSymbol(ISymbolNode symbolRef)
        {
            LLVMValueRef symbolDefValue;
            if (symbolRef is IMethodNode { Offset: 0 } methodNode)
            {
                symbolDefValue = GetOrCreateLLVMFunction(methodNode);
            }
            else if (symbolRef is ExternSymbolNode)
            {
                // We assume extenal symbol nodes are functions. This is rather fragile, but handling this precisely
                // would require modifying producers of these nodes to provide more information (namely, the signature
                // for function symbols).
                //
                using Utf8Name externFuncName = GetMangledUtf8Name(symbolRef);
                LLVMValueRef externFunc = _module.GetNamedFunction(externFuncName);
                if (externFunc.Handle == null)
                {
                    LLVMTypeRef funcType = LLVMTypeRef.CreateFunction(_compilation.LLVMVoidType, []);
                    Debug.Assert(_module.GetNamedGlobal(externFuncName).Handle == null);
                    externFunc = _module.AddFunction(externFuncName, funcType);
                }

                symbolDefValue = externFunc;
            }
            else
            {
                using Utf8Name symbolDefName = GetMangledUtf8Name(symbolRef);
                symbolDefValue = _module.GetNamedAlias(symbolDefName);

                if (symbolDefValue.Handle == null)
                {
                    // Dummy aliasee; emission will fill in the real value.
                    LLVMValueRef aliasee = LLVMValueRef.CreateConstNull(_ptrType);
                    symbolDefValue = _module.AddAlias(symbolDefName, _int8Type, aliasee);
                }
            }

            return symbolDefValue;
        }

        private LLVMValueRef GetSymbolReferenceValue(ISymbolNode symbolRef, int delta = 0)
        {
            LLVMValueRef symbolRefValue = GetOrCreateSymbol(symbolRef);
            int symbolRefOffset = symbolRef.Offset + delta;
            if (symbolRefOffset != 0)
            {
                symbolRefValue = LLVMValueRef.CreateConstGEP(symbolRefValue, symbolRefOffset);
            }

            return symbolRefValue;
        }

        private LLVMValueRef GetOrCreateLLVMFunction(IMethodNode methodNode)
        {
            MethodDesc method = methodNode.Method;
            using Utf8Name methodName = GetMangledUtf8Name(methodNode);
            LLVMValueRef methodFunc = GetOrCreateLLVMFunction(methodName, method.Signature, method.RequiresInstArg());

            return methodFunc;
        }

        private LLVMValueRef GetOrCreateLLVMFunction(ReadOnlySpan<byte> mangledName, MethodSignature signature, bool hasHiddenParam)
        {
            LLVMValueRef llvmFunction = _module.GetNamedFunction(mangledName);

            if (llvmFunction.Handle == null)
            {
                LLVMTypeRef llvmFuncType = _compilation.GetLLVMSignatureForMethod(signature, hasHiddenParam);
                return _module.AddFunction(mangledName, llvmFuncType);
            }
            return llvmFunction;
        }

        private LLVMValueRef GetOrCreateLLVMFunction(ReadOnlySpan<byte> mangledName, LLVMTypeRef functionType)
        {
            LLVMValueRef llvmFunction = _module.GetNamedFunction(mangledName);

            if (llvmFunction.Handle == null)
            {
                return _module.AddFunction(mangledName, functionType);
            }
            return llvmFunction;
        }

        private Utf8Name GetMangledUtf8Name(ISymbolNode node, string suffix = null)
        {
            Utf8StringBuilder builder = _utf8StringBuilder;

            int offset = builder.Length;
            node.AppendMangledName(_nodeFactory.NameMangler, _utf8StringBuilder);
            if (suffix != null)
            {
                builder.Append(suffix);
            }
            return new(builder, offset);
        }

        private Utf8Name GetUtf8Name(string name)
        {
            Utf8StringBuilder builder = _utf8StringBuilder;

            int offset = builder.Length;
            builder.Append(name);
            return new(builder, offset);
        }

        private void CreateUnreachedAfterAlwaysThrowCall(LLVMBuilderRef builder)
        {
            if (_compilation.GetLlvmExceptionHandlingModel() == CorInfoLlvmEHModel.Emulated)
            {
                LLVMTypeRef type = builder.InsertBlock.Parent.GetValueType().ReturnType;
                if (type == _compilation.LLVMVoidType)
                {
                    builder.BuildRetVoid();
                }
                else
                {
                    builder.BuildRet(LLVMValueRef.CreateConstNull(type));
                }
            }
            else
            {
                builder.BuildUnreachable();
            }
        }

        private static LLVMValueRef CreateCall(LLVMBuilderRef builder, LLVMValueRef func, ReadOnlySpan<LLVMValueRef> args, ReadOnlySpan<byte> name = default)
        {
            return builder.BuildCall(func.GetValueType(), func, args, name);
        }

        private LLVMValueRef CreateAddOffset(LLVMBuilderRef builder, LLVMValueRef address, int offset, ReadOnlySpan<byte> name)
        {
            if (offset != 0)
            {
                LLVMValueRef offsetValue = CreateConst(_int32Type, offset);
                address = builder.BuildGEP(address, offsetValue, name);
            }

            return address;
        }

        private static LLVMValueRef CreateConst(LLVMTypeRef type, int value) => LLVMValueRef.CreateConstInt(type, (ulong)value);

        /// <summary>
        /// A little helper struct that releases memory associated with the string when disposed.
        /// Intended to be used together with 'using', RAII-style; must be passed by reference.
        /// </summary>
        private readonly ref struct Utf8Name
        {
            private readonly Utf8StringBuilder _builder;
            private readonly int _offset;
            private readonly int _length;

            public Utf8Name(Utf8StringBuilder builder, int offset)
            {
                _builder = builder;
                _offset = offset;
                _length = builder.Length - offset;

                builder.Append('\0');
            }

            public void Dispose()
            {
                Debug.Assert(_offset < _builder.Length, "Double dispose");
                _builder.Truncate(_offset);
            }

            public override string ToString() => Encoding.UTF8.GetString(this);

            public static implicit operator ReadOnlySpan<byte>(in Utf8Name name) =>
                name._builder.AsSpan().Slice(name._offset, name._length);
        }
    }
}
