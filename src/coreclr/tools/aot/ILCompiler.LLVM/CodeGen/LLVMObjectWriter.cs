// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.JitInterface;
using Internal.Text;
using Internal.TypeSystem;
using LLVMSharp.Interop;
using ObjectData = ILCompiler.DependencyAnalysis.ObjectNode.ObjectData;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Object writer that emits LLVM (bitcode).
    /// </summary>
    internal sealed unsafe class LLVMObjectWriter
    {
        // Module with ILC-generated code and data.
        private readonly LLVMModuleRef _module;

        // Module with the external functions and getters for them. Must be separate from the main module so as to avoid
        // direct calls with mismatched signatures at the LLVM level.
        private readonly LLVMModuleRef _moduleWithExternalFunctions;

        private LLVMTypeRef _ptrType;
        private LLVMTypeRef _intPtrType;

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

#if DEBUG
        private static readonly Dictionary<string, ISymbolNode> _previouslyWrittenNodeNames = new();
#endif

        private LLVMObjectWriter(string objectFilePath, LLVMCodegenCompilation compilation)
        {
            _module = LLVMModuleRef.CreateWithName("data");
            _module.Target = compilation.Options.Target;
            _module.DataLayout = compilation.Options.DataLayout;

            _moduleWithExternalFunctions = LLVMModuleRef.CreateWithName("external");
            _moduleWithExternalFunctions.Target = compilation.Options.Target;
            _moduleWithExternalFunctions.DataLayout = compilation.Options.DataLayout;

            _ptrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
            _intPtrType = compilation.NodeFactory.Target.PointerSize == 4 ? LLVMTypeRef.Int32 : LLVMTypeRef.Int64;

            _compilation = compilation;
            _nodeFactory = compilation.NodeFactory;
            _objectFilePath = objectFilePath;
        }

        public static void EmitObject(string objectFilePath, IEnumerable<DependencyNode> nodes, LLVMCodegenCompilation compilation, IObjectDumper dumper)
        {
            LLVMObjectWriter objectWriter = new LLVMObjectWriter(objectFilePath, compilation);
            NodeFactory factory = compilation.NodeFactory;

            bool succeeded = false;
            try
            {
                foreach (DependencyNode depNode in nodes)
                {
                    if (depNode is LLVMMethodCodeNode runtimeExportNode && runtimeExportNode.Method.HasCustomAttribute("System.Runtime", "RuntimeExportAttribute"))
                    {
                        objectWriter.EmitRuntimeExportThunk(runtimeExportNode);
                        continue;
                    }

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

                succeeded = true;
            }
            finally
            {
                objectWriter.FinishObjWriter();

                if (!succeeded)
                {
                    // If there was an exception while generating the OBJ file, make sure we don't leave the unfinished
                    // object file around.
                    try
                    {
                        File.Delete(objectFilePath);
                    }
                    catch
                    {
                    }
                }
                else
                {
                    // Make sure we have released all memory used for mangling names.
                    Debug.Assert(objectWriter._utf8StringBuilder.Length == 0);
                }
            }
        }

        private unsafe void EmitObjectNode(ObjectNode node, ObjectData nodeContents)
        {
            NodeFactory factory = _nodeFactory;
            int pointerSize = factory.Target.PointerSize;

            // All references to this symbol are through "ordinarily named" aliases. Thus, we need to suffix the real definition.
            ISymbolNode symbolNode = node as ISymbolNode ?? ((IHasStartSymbol)node).StartSymbol;
            using Utf8Name dataSymbolName = GetMangledUtf8Name(symbolNode, "__DATA");

            // Calculate the size of this object node.
            int dataSizeInBytes = nodeContents.Data.Length;
            int dataSizeInBytesAligned = dataSizeInBytes.AlignUp(pointerSize); // TODO-LLVM: do not pad out the data.
            int dataSizeInPointers = dataSizeInBytesAligned / pointerSize;

            // Create and initialize the LLVM global value.
            LLVMTypeRef dataSymbolType = LLVMTypeRef.CreateArray(_ptrType, (uint)dataSizeInPointers);
            LLVMValueRef dataSymbol = _module.AddGlobal(dataSymbolName, dataSymbolType);
            dataSymbol.Section = node.GetSection(_nodeFactory).Name;
            dataSymbol.Alignment = (uint)nodeContents.Alignment;

            // Emit the value of this object node.
            if (_currentObjectData.Length < dataSizeInPointers)
            {
                Array.Resize(ref _currentObjectData, Math.Max(_currentObjectData.Length * 2, dataSizeInPointers));
            }
            Span<LLVMValueRef> dataElements = _currentObjectData.AsSpan(0, dataSizeInPointers);
            dataElements.Clear();

            // Emit relocations. We assume these are always aligned.
            foreach (Relocation reloc in nodeContents.Relocs)
            {
                long delta;
                fixed (void* location = &nodeContents.Data[reloc.Offset])
                {
                    delta = Relocation.ReadValue(reloc.RelocType, location);
                }

                int symbolRefIndex = Math.DivRem(reloc.Offset, pointerSize, out int unalignedOffset);
                if (unalignedOffset != 0)
                {
                    throw new NotImplementedException("Unaligned relocation");
                }

                ISymbolNode symbolRefNode = reloc.Target;
                if (symbolRefNode is EETypeNode eeTypeNode && eeTypeNode.ShouldSkipEmittingObjectNode(factory))
                {
                    symbolRefNode = factory.ConstructedTypeSymbol(eeTypeNode.Type);
                }

                dataElements[symbolRefIndex] = GetSymbolReferenceValue(symbolRefNode, checked((int)delta));
            }

            // Emit binary data.
            ReadOnlySpan<byte> data = nodeContents.Data.AsSpan();
            for (int i = 0; i < dataElements.Length; i++)
            {
                ref LLVMValueRef dataElementRef = ref dataElements[i];
                if (dataElementRef == default)
                {
                    ulong value = 0;
                    int offset = i * pointerSize;
                    int size = Math.Min(dataSizeInBytes - offset, pointerSize);
                    data.Slice(offset, size).CopyTo(new Span<byte>(&value, size));

                    dataElementRef = LLVMValueRef.CreateConstIntToPtr(LLVMValueRef.CreateConstInt(_intPtrType, value), _ptrType);
                }
            }

            dataSymbol.Initializer = LLVMValueRef.CreateConstArray(_ptrType, dataElements);

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
                LLVMValueRef offsetValue = CreateConst(LLVMTypeRef.Int32, offsetFromBaseSymbol);
                symbolAddress = LLVMValueRef.CreateConstGEP2(LLVMTypeRef.Int8, symbolAddress, new[] { offsetValue });
            }

            LLVMValueRef symbolDef = _module.GetNamedAlias(symbolIdentifier);
            if (symbolDef.Handle == IntPtr.Zero)
            {
                _module.AddAlias(symbolIdentifier, LLVMTypeRef.Int8, symbolAddress);
            }
            else
            {
                // Set the aliasee.
                LLVM.AliasSetAliasee(symbolDef, symbolAddress);
            }
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
#endif
            _module.Verify(LLVMVerifierFailureAction.LLVMAbortProcessAction);
            _moduleWithExternalFunctions.Verify(LLVMVerifierFailureAction.LLVMAbortProcessAction);

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
                LLVMValueRef llvmUsedGlobal = _module.AddGlobal(llvmUsedType, "llvm.used");
                llvmUsedGlobal.Linkage = LLVMLinkage.LLVMAppendingLinkage;
                llvmUsedGlobal.Section = "llvm.metadata";
                llvmUsedGlobal.Initializer = LLVMValueRef.CreateConstArray(_ptrType, llvmUsedSymbols);
            }
        }

        private void EmitRuntimeExportThunk(LLVMMethodCodeNode methodNode)
        {
            MethodDesc method = methodNode.Method;
            Debug.Assert(method.HasCustomAttribute("System.Runtime", "RuntimeExportAttribute") && methodNode.CompilationCompleted);

            LLVMValueRef managedFunc = GetOrCreateLLVMFunction(methodNode);
            LLVMTypeRef managedFuncType = managedFunc.GetValueType();

            // Native signature: managed minus the shadow stack.
            LLVMTypeRef[] managedFuncParamTypes = managedFuncType.ParamTypes;
            LLVMTypeRef[] nativeFuncParamTypes = new LLVMTypeRef[managedFuncParamTypes.Length - 1];
            Array.Copy(managedFuncParamTypes, 1, nativeFuncParamTypes, 0, nativeFuncParamTypes.Length);

            LLVMTypeRef nativeFuncType = LLVMTypeRef.CreateFunction(managedFuncType.ReturnType, nativeFuncParamTypes, false);
            using Utf8Name nativeFuncName = GetUtf8Name(_compilation.NodeFactory.GetSymbolAlternateName(methodNode));
            LLVMValueRef nativeFunc = GetOrCreateLLVMFunction(nativeFuncName, nativeFuncType);

            using LLVMBuilderRef builder = _module.Context.CreateBuilder();
            LLVMBasicBlockRef block = nativeFunc.AppendBasicBlock("ManagedCallBlock");
            builder.PositionAtEnd(block);

            // Get the shadow stack. Since we are wrapping a runtime export, the caller is (by definition) managed, so we must have set up the shadow
            // stack already and can bypass the init check.
            LLVMTypeRef getShadowStackFuncSig = LLVMTypeRef.CreateFunction(_ptrType, Array.Empty<LLVMTypeRef>());
            LLVMValueRef getShadowStackFunc = GetOrCreateLLVMFunction("RhpGetShadowStackTop"u8, getShadowStackFuncSig);
            LLVMValueRef shadowStack = builder.BuildCall2(getShadowStackFuncSig, getShadowStackFunc, Array.Empty<LLVMValueRef>());

            LLVMValueRef[] args = new LLVMValueRef[managedFuncParamTypes.Length];
            args[0] = shadowStack;

            for (uint i = 0; i < nativeFuncParamTypes.Length; i++)
            {
                args[i + 1] = nativeFunc.GetParam(i);
            }

            LLVMValueRef returnValue = CreateCall(builder, managedFunc, args, "");

            if (method.Signature.ReturnType.IsVoid)
            {
                builder.BuildRetVoid();
            }
            else
            {
                builder.BuildRet(returnValue);
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
            LLVMTypeRef helperFuncType = LLVMTypeRef.CreateFunction(_ptrType, new[] { _ptrType /* shadow stack */, _ptrType /* generic context */ });
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(helperFuncName, helperFuncType);

            LLVMBuilderRef builder = _module.Context.CreateBuilder();
            builder.PositionAtEnd(helperFunc.AppendBasicBlock("GenericReadyToRunHelper"));

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

                        result = builder.BuildLoad2(_ptrType, result, "gcBase");
                    }
                    break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

                        if (TriggersLazyStaticConstructor(factory, target))
                        {
                            GenericLookupResult nonGcBaseLookup = factory.GenericLookup.TypeNonGCStaticBase(target);
                            LLVMValueRef nonGcStaticsBase = OutputCodeForDictionaryLookup(builder, factory, node, nonGcBaseLookup, dictionary);
                            OutputCodeForTriggerCctor(builder, nonGcStaticsBase);
                        }

                        result = OutputCodeForGetThreadStaticBase(builder, result);
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
                GetCodeForVirtualCallHelper(node);
                return;
            }
            if (node.Id == ReadyToRunHelperId.DelegateCtor)
            {
                GetCodeForDelegateCtorHelper(node);
                return;
            }

            using Utf8Name helperFuncName = GetMangledUtf8Name(node);
            LLVMTypeRef helperFuncType = LLVMTypeRef.CreateFunction(_ptrType, new[] { _ptrType /* shadow stack or "this" */ });
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(helperFuncName, helperFuncType);

            using LLVMBuilderRef builder = _module.Context.CreateBuilder();
            builder.PositionAtEnd(helperFunc.AppendBasicBlock("ReadyToRunHelper"));

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
                        result = builder.BuildLoad2(_ptrType, result, "gcBase");
                    }
                    break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

                        if (_compilation.HasLazyStaticConstructor(target))
                        {
                            LLVMValueRef nonGcBase = GetSymbolReferenceValue(factory.TypeNonGCStaticsSymbol(target));
                            OutputCodeForTriggerCctor(builder, nonGcBase);
                        }

                        LLVMValueRef pModuleDataSlot = GetSymbolReferenceValue(factory.TypeThreadStaticIndex(target));
                        result = OutputCodeForGetThreadStaticBase(builder, pModuleDataSlot);
                    }
                    break;

                case ReadyToRunHelperId.ResolveVirtualFunction:
                    {
                        MethodDesc targetMethod = (MethodDesc)node.Target;

                        if (targetMethod.OwningType.IsInterface)
                        {
                            // TODO-LLVM: would be nice to use pointers instead of IntPtr in "RhpResolveInterfaceMethod".
                            LLVMTypeRef resolveFuncType = LLVMTypeRef.CreateFunction(_intPtrType, new[] { _ptrType, _intPtrType });
                            LLVMValueRef resolveFunc = GetOrCreateLLVMFunction("RhpResolveInterfaceMethod"u8, resolveFuncType);

                            LLVMValueRef cell = GetSymbolReferenceValue(factory.InterfaceDispatchCell(targetMethod));
                            LLVMValueRef cellArg = builder.BuildPtrToInt(cell, _intPtrType, "cellArg");
                            result = builder.BuildCall2(resolveFuncType, resolveFunc, new[] { helperFunc.GetParam(0), cellArg });
                            result = builder.BuildIntToPtr(result, _ptrType, "pInterfaceFunc");
                        }
                        else
                        {
                            Debug.Assert(!targetMethod.CanMethodBeInSealedVTable());
                            result = OutputCodeForVTableLookup(builder, helperFunc.GetParam(0), targetMethod);
                        }
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }

            builder.BuildRet(result);
        }

        private void GetCodeForVirtualCallHelper(ReadyToRunHelperNode node)
        {
            MethodDesc targetMethod = (MethodDesc)node.Target;
            Debug.Assert(!targetMethod.OwningType.IsInterface);
            Debug.Assert(!targetMethod.CanMethodBeInSealedVTable());
            Debug.Assert(!targetMethod.RequiresInstArg());

            using Utf8Name helperFuncName = GetMangledUtf8Name(node);
            LLVMTypeRef funcType = _compilation.GetLLVMSignatureForMethod(targetMethod.Signature, hasHiddenParam: false);
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(helperFuncName, funcType);

            using LLVMBuilderRef builder = _module.Context.CreateBuilder();
            builder.PositionAtEnd(helperFunc.AppendBasicBlock("VirtualCall"));

            LLVMValueRef[] args = new LLVMValueRef[helperFunc.ParamsCount];
            for (uint i = 0; i < args.Length; i++)
            {
                args[i] = helperFunc.GetParam(i);
            }

            LLVMValueRef pTargetMethod = OutputCodeForVTableLookup(builder, helperFunc.GetParam(1), targetMethod);
            LLVMValueRef callTarget = builder.BuildCall2(funcType, pTargetMethod, args);
            if (funcType.ReturnType != LLVMTypeRef.Void)
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
            LLVMTypeRef[] helperFuncParams;
            if (genericNode != null)
            {
                delegateCreationInfo = (DelegateCreationInfo)genericNode.Target;
                helperFuncParams = new[] { _ptrType, _ptrType, _ptrType, _ptrType };
            }
            else
            {
                delegateCreationInfo = (DelegateCreationInfo)((ReadyToRunHelperNode)node).Target;
                helperFuncParams = new[] { _ptrType, _ptrType, _ptrType };
            }

            using Utf8Name helperFuncName = GetMangledUtf8Name(node);
            LLVMTypeRef helperFuncType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, helperFuncParams);
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(helperFuncName, helperFuncType);

            // Incoming parameters are: (SS, this, targetObj, [GenericContext]).
            // Outgoing parameters are: (SS, this, targetObj, pTarget, [InvokeThunk]).
            // Thus, our main responsibility is to computate the target.
            //
            LLVMValueRef initializerFunc = GetOrCreateLLVMFunction(delegateCreationInfo.Constructor);
            LLVMTypeRef[] initializerFuncParamTypes = initializerFunc.GetValueType().ParamTypes;

            using LLVMBuilderRef builder = _module.Context.CreateBuilder();
            builder.PositionAtEnd(helperFunc.AppendBasicBlock(genericNode != null ? "GenericDelegateCtor" : "DelegateCtor"));

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
                targetValue = builder.BuildPointerCast(targetValue, targetValueType, "pTarget");
            }

            LLVMValueRef[] initializerArgs = new LLVMValueRef[initializerFuncParamTypes.Length];
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

            using LLVMBuilderRef builder = _module.Context.CreateBuilder();
            LLVMBasicBlockRef block = tentativeStub.AppendBasicBlock("TentativeStub");
            builder.PositionAtEnd(block);

            CreateCall(builder, helperFunc, new[] { tentativeStub.GetParam(0) });
            builder.BuildUnreachable();
        }

        private void GetCodeForUnboxThunkMethod(UnboxingStubNode node)
        {
            NodeFactory factory = _compilation.NodeFactory;

            // This is the regular unboxing thunk that just does "Target(ref @this.Data, args...)".
            // Note how we perform a shadow tail call here while simultaneously overwriting "this".
            //
            LLVMValueRef unboxingLlvmFunc = GetOrCreateLLVMFunction(node);
            LLVMValueRef unboxedLlvmFunc = GetOrCreateLLVMFunction(node.GetUnderlyingMethodEntrypoint(factory));

            using LLVMBuilderRef builder = _module.Context.CreateBuilder();
            builder.PositionAtEnd(unboxingLlvmFunc.AppendBasicBlock("SimpleUnboxingThunk"));

            Debug.Assert(unboxingLlvmFunc.ParamsCount == unboxedLlvmFunc.ParamsCount);
            LLVMValueRef[] args = new LLVMValueRef[unboxingLlvmFunc.ParamsCount];
            for (uint i = 0; i < args.Length; i++)
            {
                LLVMValueRef arg = unboxingLlvmFunc.GetParam(i);
                if (i == 1)
                {
                    // Adjust "this" by the method table offset.
                    arg = CreateAddOffset(builder, arg, factory.Target.PointerSize, "dataThis");
                }

                args[i] = arg;
            }

            LLVMValueRef unboxedCall = CreateCall(builder, unboxedLlvmFunc, args);
            if (unboxedCall.TypeOf != LLVMTypeRef.Void)
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
            // TODO: use the Utf8 string directly here.
            string externFuncName = node.ExternMethodName.ToString();
            LLVMTypeRef externFuncType;

            if (node.Signature != null)
            {
                static LLVMTypeRef GetLlvmType(TargetAbiType abiType) => abiType switch
                {
                    TargetAbiType.Void => LLVMTypeRef.Void,
                    TargetAbiType.Int32 => LLVMTypeRef.Int32,
                    TargetAbiType.Int64 => LLVMTypeRef.Int64,
                    TargetAbiType.Float => LLVMTypeRef.Float,
                    TargetAbiType.Double => LLVMTypeRef.Double,
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
                string text = $"Signature mismatch detected: '{externFuncName}' will not be imported from the host environment";

                foreach (MethodDesc method in node.EnumerateMethods())
                {
                    text += $"\n Defined as: {method.Signature.ReturnType} {method}";
                }

                // Error code is just below the "AOT analysis" namespace.
                _compilation.Logger.LogWarning(text, 3049, Path.GetFileNameWithoutExtension(_objectFilePath));

                externFuncType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, Array.Empty<LLVMTypeRef>());
            }

            LLVMModuleRef externFuncModule = _moduleWithExternalFunctions;
            Debug.Assert(externFuncModule.GetNamedFunction(externFuncName).Handle == IntPtr.Zero);
            LLVMValueRef externFunc = externFuncModule.AddFunction(externFuncName, externFuncType);

            // Add import attributes if specified.
            if (_compilation.ConfigurableWasmImportPolicy.TryGetWasmModule(externFuncName, out string wasmModuleName))
            {
                externFunc.AddFunctionAttribute("wasm-import-name", externFuncName);
                externFunc.AddFunctionAttribute("wasm-import-module", wasmModuleName);
            }

            // Define the accessor function.
            string accessorFuncName = node.GetMangledName(_nodeFactory.NameMangler);
            LLVMTypeRef accessorFuncType = LLVMTypeRef.CreateFunction(externFunc.TypeOf, Array.Empty<LLVMTypeRef>());
            LLVMValueRef accessorFunc = externFuncModule.AddFunction(accessorFuncName, accessorFuncType);

            using LLVMBuilderRef builder = LLVMBuilderRef.Create(externFuncModule.Context);
            LLVMBasicBlockRef block = accessorFunc.AppendBasicBlock("GetExternFunc");
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
                var slotAddr = CreateAddOffset(builder, context, slotOffset, "dictionarySlotAddr");
                dictionary = builder.BuildLoad2(_ptrType, slotAddr, "dictionary");
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
                return LLVMValueRef.CreateConstPointerNull(_ptrType);
            }
            int offset = dictionarySlot * factory.Target.PointerSize;

            // Load the generic dictionary cell
            LLVMValueRef slotAddr = CreateAddOffset(builder, dictionary, offset, "dictionarySlotAddr");
            LLVMValueRef lookupResult = builder.BuildLoad2(_ptrType, slotAddr, "slotValue");

            if (node.HandlesInvalidEntries(factory))
            {
                LLVMBasicBlockRef currentBlock = builder.InsertBlock;
                LLVMValueRef currentFunc = currentBlock.Parent;
                LLVMBasicBlockRef slotNotAvailableBlock = currentFunc.AppendBasicBlock("DictionarySlotNotAvailable");
                LLVMBasicBlockRef slotAvailableBlock = currentFunc.AppendBasicBlock("DictionarySlotAvailable");
                slotAvailableBlock.MoveAfter(currentBlock);

                LLVMValueRef nullValue = LLVMValueRef.CreateConstPointerNull(_ptrType);
                LLVMValueRef isSlotNotAvailable = builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, lookupResult, nullValue, "isSlotNotAvailable");
                builder.BuildCondBr(isSlotNotAvailable, slotNotAvailableBlock, slotAvailableBlock);

                LLVMValueRef slotNotAvailableFunc = GetOrCreateLLVMFunction(ReadyToRunGenericHelperNode.GetBadSlotHelper(factory));
                builder.PositionAtEnd(slotNotAvailableBlock);
                CreateCall(builder, slotNotAvailableFunc, new[] { currentFunc.GetParam(0) });
                builder.BuildUnreachable();

                builder.PositionAtEnd(slotAvailableBlock);
            }

            switch (lookup.LookupResultReferenceType(factory))
            {
                case GenericLookupResultReferenceType.Indirect:
                    lookupResult = builder.BuildLoad2(_ptrType, lookupResult, "actualSlotValue");
                    break;

                case GenericLookupResultReferenceType.ConditionalIndirect:
                    throw new NotImplementedException();

                default:
                    break;
            }

            return lookupResult;
        }

        private LLVMValueRef OutputCodeForVTableLookup(LLVMBuilderRef builder, LLVMValueRef objThis, MethodDesc method)
        {
            LLVMValueRef pMethodTable = builder.BuildLoad2(_ptrType, objThis, "pMethodTable");

            int slot = VirtualMethodSlotHelper.GetVirtualMethodSlot(_nodeFactory, method, method.OwningType);
            int slotOffset = EETypeNode.GetVTableOffset(_nodeFactory.Target.PointerSize) + (slot * _nodeFactory.Target.PointerSize);
            LLVMValueRef pSlot = CreateAddOffset(builder, pMethodTable, slotOffset, "pSlot");
            LLVMValueRef slotValue = builder.BuildLoad2(_ptrType, pSlot, "pTarget");

            return slotValue;
        }

        private void OutputCodeForTriggerCctor(LLVMBuilderRef builder, LLVMValueRef nonGcStaticBaseValue)
        {
            // We need to trigger the cctor before returning the base. It is stored at the beginning of the non-GC statics region.
            int pContextOffset = -NonGCStaticsNode.GetClassConstructorContextSize(_nodeFactory.Target);
            LLVMValueRef pContext = CreateAddOffset(builder, nonGcStaticBaseValue, pContextOffset, "pContext");
            LLVMValueRef initialized = builder.BuildLoad2(_intPtrType, pContext, "initialized");
            LLVMValueRef isInitialized = builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, initialized, CreateConst(_intPtrType, 0), "isInitialized");

            LLVMValueRef func = builder.InsertBlock.Parent;
            LLVMBasicBlockRef callHelperBlock = func.AppendBasicBlock("CallHelper");
            LLVMBasicBlockRef returnBlock = func.AppendBasicBlock("Return");
            builder.BuildCondBr(isInitialized, returnBlock, callHelperBlock);

            builder.PositionAtEnd(callHelperBlock);
            IMethodNode helperFuncNode = (IMethodNode)_nodeFactory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnNonGCStaticBase);
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(helperFuncNode);
            LLVMValueRef nonGcStaticBaseValueArg = builder.BuildPtrToInt(nonGcStaticBaseValue, _intPtrType);
            LLVMValueRef[] helperCallArgs = new[] { func.GetParam(0), pContext, nonGcStaticBaseValueArg };

            CreateCall(builder, helperFunc, helperCallArgs);
            builder.BuildBr(returnBlock);

            builder.PositionAtEnd(returnBlock);
        }

        private LLVMValueRef OutputCodeForGetThreadStaticBase(LLVMBuilderRef builder, LLVMValueRef pModuleDataSlot)
        {
            LLVMValueRef shadowStack = builder.InsertBlock.Parent.GetParam(0);

            // First arg: address of the TypeManager slot that provides the helper with information about
            // module index and the type manager instance (which is used for initialization on first access).
            LLVMValueRef pModuleData = builder.BuildLoad2(_ptrType, pModuleDataSlot, "pModuleData");

            // Second arg: index of the type in the ThreadStatic section of the modules.
            LLVMValueRef pTypeTlsIndex = CreateAddOffset(builder, pModuleDataSlot, _nodeFactory.Target.PointerSize, "pTypeTlsIndex");
            LLVMValueRef typeTlsIndex = builder.BuildLoad2(LLVMTypeRef.Int32, pTypeTlsIndex, "typeTlsIndex");

            IMethodNode getBaseHelperNode = (IMethodNode)_nodeFactory.HelperEntrypoint(HelperEntrypoint.GetThreadStaticBaseForType);
            LLVMValueRef getBaseHelperFunc = GetOrCreateLLVMFunction(getBaseHelperNode);
            LLVMValueRef[] getBaseHelperArgs = new[] { shadowStack, pModuleData, typeTlsIndex };
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
                if (externFunc.Handle == IntPtr.Zero)
                {
                    LLVMTypeRef funcType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, Array.Empty<LLVMTypeRef>());
                    Debug.Assert(_module.GetNamedGlobal(externFuncName).Handle == IntPtr.Zero);
                    externFunc = _module.AddFunction(externFuncName, funcType);
                }

                symbolDefValue = externFunc;
            }
            else
            {
                using Utf8Name symbolDefName = GetMangledUtf8Name(symbolRef);
                symbolDefValue = _module.GetNamedAlias(symbolDefName);

                if (symbolDefValue.Handle == IntPtr.Zero)
                {
                    // Dummy aliasee; emission will fill in the real value.
                    LLVMValueRef aliasee = LLVMValueRef.CreateConstPointerNull(_ptrType);
                    symbolDefValue = _module.AddAlias(symbolDefName, LLVMTypeRef.Int8, aliasee);
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
                LLVMValueRef offsetValue = CreateConst(LLVMTypeRef.Int32, symbolRefOffset);
                symbolRefValue = LLVMValueRef.CreateConstGEP2(LLVMTypeRef.Int8, symbolRefValue, new[] { offsetValue });
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

            if (llvmFunction.Handle == IntPtr.Zero)
            {
                LLVMTypeRef llvmFuncType = _compilation.GetLLVMSignatureForMethod(signature, hasHiddenParam);
                return _module.AddFunction(mangledName, llvmFuncType);
            }
            return llvmFunction;
        }

        private LLVMValueRef GetOrCreateLLVMFunction(ReadOnlySpan<byte> mangledName, LLVMTypeRef functionType)
        {
            LLVMValueRef llvmFunction = _module.GetNamedFunction(mangledName);

            if (llvmFunction.Handle == IntPtr.Zero)
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

        private static LLVMValueRef CreateCall(LLVMBuilderRef builder, LLVMValueRef func, LLVMValueRef[] args, string name = "")
        {
            Debug.Assert(func.IsAFunction.Handle != IntPtr.Zero);
            return builder.BuildCall2(func.GetValueType(), func, args, name);
        }

        private static LLVMValueRef CreateAddOffset(LLVMBuilderRef builder, LLVMValueRef address, int offset, string name)
        {
            if (offset != 0)
            {
                LLVMValueRef offsetValue = CreateConst(LLVMTypeRef.Int32, offset);
                address = builder.BuildGEP2(LLVMTypeRef.Int8, address, new[] { offsetValue }, name);
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
                name._builder.UnderlyingArray.AsSpan(name._offset, name._length);
        }
    }
}
