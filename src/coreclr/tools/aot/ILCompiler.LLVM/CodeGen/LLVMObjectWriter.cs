// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using ILCompiler.DependencyAnalysisFramework;

using Internal.IL;
using Internal.JitInterface;
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

        // Raw data emitted for the current object node.
        private ArrayBuilder<byte> _currentObjectData;

        // References (pointers) to symbols the current object node contains (and thus depends on).
        private Dictionary<int, SymbolRefData> _currentObjectSymbolRefs = new Dictionary<int, SymbolRefData>();

        // Data to be emitted as LLVM bitcode after all of the object nodes have been processed.
        private readonly List<ObjectNodeDataEmission> _dataToFill = new List<ObjectNodeDataEmission>();

#if DEBUG
        private static readonly Dictionary<string, ISymbolNode> _previouslyWrittenNodeNames = new Dictionary<string, ISymbolNode>();
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
                        case ModulesSectionNode modulesSectionNode:
                            objectWriter.EmitReadyToRunHeaderCallback(modulesSectionNode);
                            goto default;
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
                    // Ensure alignment for the node.
                    objectWriter.EmitAlignment(nodeContents.Alignment);

                    objectWriter.EmitBytes(nodeContents.Data);

                    Relocation[] relocs = nodeContents.Relocs;
                    foreach (var reloc in relocs)
                    {
                        long delta;
                        unsafe
                        {
                            fixed (void* location = &nodeContents.Data[reloc.Offset])
                            {
                                delta = Relocation.ReadValue(reloc.RelocType, location);
                            }
                        }

                        ISymbolNode symbolToWrite = reloc.Target;
                        if (reloc.Target is EETypeNode eeTypeNode && eeTypeNode.ShouldSkipEmittingObjectNode(factory))
                        {
                            symbolToWrite = factory.ConstructedTypeSymbol(eeTypeNode.Type);
                        }

                        objectWriter.EmitSymbolReference(symbolToWrite, reloc.Offset, (int)delta);
                    }

                    objectWriter.DoneObjectNode(node, nodeContents.DefinedSymbols);
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
            }
        }

        private void DoneObjectNode(ObjectNode node, ISymbolDefinitionNode[] definedSymbols)
        {
            EmitAlignment(_nodeFactory.Target.PointerSize);
            Debug.Assert(_nodeFactory.Target.PointerSize == 4);
            int countOfPointerSizedElements = _currentObjectData.Count / _nodeFactory.Target.PointerSize;

            ISymbolNode symNode = node as ISymbolNode ?? ((IHasStartSymbol)node).StartSymbol;
            string symbolName = symNode.GetMangledName(_nodeFactory.NameMangler);

            // All references to this symbol are through "ordinarily named" aliases. Thus, we need to suffix the real definition.
            string dataSymbolName = symbolName + "__DATA";

            LLVMTypeRef dataSymbolType = LLVMTypeRef.CreateArray(_ptrType, (uint)countOfPointerSizedElements);
            LLVMValueRef dataSymbol = _module.AddGlobal(dataSymbolType, dataSymbolName);
            _dataToFill.Add(new ObjectNodeDataEmission(dataSymbol, _currentObjectData.ToArray(), _currentObjectSymbolRefs));

            foreach (ISymbolDefinitionNode definedSymbol in definedSymbols)
            {
                string definedSymbolName = definedSymbol.GetMangledName(_nodeFactory.NameMangler);
                int definedSymbolOffset = definedSymbol.Offset;
                EmitSymbolDef(dataSymbol, definedSymbolName, definedSymbolOffset);

                string alternateDefinedSymbolName = _nodeFactory.GetSymbolAlternateName(definedSymbol);
                if (alternateDefinedSymbolName != null)
                {
                    EmitSymbolDef(dataSymbol, alternateDefinedSymbolName, definedSymbolOffset);
                }
            }

            _currentObjectSymbolRefs = new Dictionary<int, SymbolRefData>();
            _currentObjectData = default;
        }

        private void EmitAlignment(int byteAlignment)
        {
            while ((_currentObjectData.Count % byteAlignment) != 0)
                _currentObjectData.Add(0);
        }

        private void EmitBytes(ReadOnlySpan<byte> bytes)
        {
            for (int i = 0; i < bytes.Length; i++)
            {
                _currentObjectData.Add(bytes[i]);
            }
        }

        private void EmitSymbolDef(LLVMValueRef baseSymbol, string symbolIdentifier, int offsetFromBaseSymbol)
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
                _module.AddAlias2(LLVMTypeRef.Int8, 0, symbolAddress, symbolIdentifier);
            }
            else
            {
                // Set the aliasee.
                LLVMSharp.Interop.LLVM.AliasSetAliasee(symbolDef, symbolAddress);
            }
        }

        private void EmitSymbolReference(ISymbolNode target, int offset, int delta)
        {
            string symbolName = target.GetMangledName(_nodeFactory.NameMangler);
            uint symbolRefOffset = checked(unchecked((uint)target.Offset) + (uint)delta);
            GetOrCreateSymbol(target); // Cause the symbol's declaration to be created (if needed).

            _currentObjectSymbolRefs.Add(offset, new SymbolRefData(symbolName, symbolRefOffset));
        }

        private struct SymbolRefData
        {
            public SymbolRefData(string symbolName, uint offset)
            {
                SymbolName = symbolName;
                Offset = offset;
            }

            internal readonly string SymbolName;
            internal readonly uint Offset;

            public LLVMValueRef ToLLVMValueRef(LLVMModuleRef module)
            {
                // Dont know if symbol is for an extern function or a variable, so check both
                LLVMValueRef valRef = module.GetNamedAlias(SymbolName);
                if (valRef.Handle == IntPtr.Zero)
                {
                    valRef = module.GetNamedFunction(SymbolName);
                }
                Debug.Assert(valRef.Handle != IntPtr.Zero, $"Undefined symbol: {SymbolName}");

                if (Offset != 0)
                {
                    LLVMValueRef[] index = new[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, Offset) };
                    valRef = LLVMValueRef.CreateConstGEP2(LLVMTypeRef.Int8, valRef, index);
                }

                return valRef;
            }
        }

        private struct ObjectNodeDataEmission
        {
            public ObjectNodeDataEmission(LLVMValueRef node, byte[] data, Dictionary<int, SymbolRefData> objectSymbolRefs)
            {
                _node = node;
                _data = data;
                _objectSymbolRefs = objectSymbolRefs;
            }
            private LLVMValueRef _node;
            private readonly byte[] _data;
            private readonly Dictionary<int, SymbolRefData> _objectSymbolRefs;

            public void Fill(LLVMObjectWriter writer)
            {
                int pointerSize = writer._nodeFactory.Target.PointerSize;
                ArrayBuilder<LLVMValueRef> entries = default;
                int countOfPointerSizedElements = _data.Length / pointerSize;

                for (int i = 0; i < countOfPointerSizedElements; i++)
                {
                    int curOffset = (i * pointerSize);
                    SymbolRefData symbolRef;
                    if (_objectSymbolRefs.TryGetValue(curOffset, out symbolRef))
                    {
                        entries.Add(symbolRef.ToLLVMValueRef(writer._module));
                    }
                    else
                    {
                        uint value = BitConverter.ToUInt32(_data, curOffset);
                        entries.Add(LLVMValueRef.CreateConstIntToPtr(LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, value), writer._ptrType));
                    }
                }

                _node.Initializer = LLVMValueRef.CreateConstArray(writer._ptrType, entries.ToArray());
            }
        }

        private void FinishObjWriter()
        {
            // Since emission to llvm is delayed until after all nodes are emitted... emit now.
            foreach (var nodeData in _dataToFill)
            {
                nodeData.Fill(this);
            }

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

        private void EmitRuntimeExportThunk(LLVMMethodCodeNode methodNode)
        {
            MethodDesc method = methodNode.Method;
            Debug.Assert(method.HasCustomAttribute("System.Runtime", "RuntimeExportAttribute") && methodNode.CompilationCompleted);

            LLVMTypeRef nativeReturnType = _compilation.GetLlvmReturnType(method.Signature.ReturnType, out bool isNativeReturnByRef);
            if (isNativeReturnByRef)
            {
                throw new NotSupportedException("Runtime export with a complex struct return");
            }

            LLVMTypeRef[] llvmParams = new LLVMTypeRef[method.Signature.Length];
            for (int i = 0; i < llvmParams.Length; i++)
            {
                _compilation.GetLlvmArgTypeForArg(isManagedAbi: false, method.Signature[i], out llvmParams[i], out bool isPassedByRef);
                if (isPassedByRef)
                {
                    throw new NotSupportedException("Runtime export with a complex struct argument"); // Not supported by the Jit.
                }
            }

            string nativeName = _compilation.NodeFactory.GetSymbolAlternateName(methodNode);
            LLVMTypeRef nativeSig = LLVMTypeRef.CreateFunction(nativeReturnType, llvmParams, false);
            LLVMValueRef nativeFunc = GetOrCreateLLVMFunction(nativeName, nativeSig);

            using LLVMBuilderRef builder = _module.Context.CreateBuilder();
            LLVMBasicBlockRef block = nativeFunc.AppendBasicBlock("ManagedCallBlock");
            builder.PositionAtEnd(block);

            // Get the shadow stack. Since we are wrapping a runtime export, the caller is (by definition) managed, so we must have set up the shadow
            // stack already and can bypass the init check.
            LLVMTypeRef getShadowStackFuncSig = LLVMTypeRef.CreateFunction(_ptrType, Array.Empty<LLVMTypeRef>());
            LLVMValueRef getShadowStackFunc = GetOrCreateLLVMFunction("RhpGetShadowStackTop", getShadowStackFuncSig);
            LLVMValueRef shadowStack = builder.BuildCall2(getShadowStackFuncSig, getShadowStackFunc, Array.Empty<LLVMValueRef>());

            _compilation.GetLlvmReturnType(method.Signature.ReturnType, out bool isManagedReturnByRef);

            int curOffset = 0;
            LLVMValueRef calleeFrame;
            if (isManagedReturnByRef)
            {
                curOffset = _compilation.PadNextOffset(method.Signature.ReturnType, curOffset);
                curOffset = curOffset.AlignUp(_nodeFactory.Target.PointerSize);

                // Clear any uncovered object references for GC.Collect.
                LLVMValueRef lengthValue = CreateConst(LLVMTypeRef.Int32, curOffset);
                LLVMValueRef fillValue = CreateConst(LLVMTypeRef.Int8, 0);
                LLVMValueRef isVolatileValue = CreateConst(LLVMTypeRef.Int1, 0);
                LLVMTypeRef memsetFuncType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new[] { _ptrType, LLVMTypeRef.Int8, LLVMTypeRef.Int32, LLVMTypeRef.Int1 });
                LLVMValueRef memsetFunc = GetOrCreateLLVMFunction("llvm.memset.p0.i32", memsetFuncType);
                builder.BuildCall2(memsetFuncType, memsetFunc, new[] { shadowStack, fillValue, lengthValue, isVolatileValue }, "");

                calleeFrame = CreateAddOffset(builder, shadowStack, curOffset, "calleeFrame");
            }
            else
            {
                calleeFrame = shadowStack;
            }

            List<LLVMValueRef> llvmArgs = new List<LLVMValueRef>();
            llvmArgs.Add(calleeFrame);

            if (isManagedReturnByRef)
            {
                // Slot for return value if necessary
                llvmArgs.Add(shadowStack);
            }

            for (int i = 0; i < llvmParams.Length; i++)
            {
                LLVMValueRef argValue = nativeFunc.GetParam((uint)i);

                if (LLVMCodegenCompilation.CanStoreTypeOnStack(method.Signature[i]))
                {
                    llvmArgs.Add(argValue);
                }
                else
                {
                    curOffset = _compilation.PadOffset(method.Signature[i], curOffset);
                    LLVMValueRef argAddr = CreateAddOffset(builder, shadowStack, curOffset, $"arg{i}");
                    builder.BuildStore(argValue, argAddr);
                    curOffset = _compilation.PadNextOffset(method.Signature[i], curOffset);
                }
            }

            LLVMValueRef managedFunction = GetOrCreateLLVMFunction(methodNode);
            LLVMValueRef llvmReturnValue = CreateCall(builder, managedFunction, llvmArgs.ToArray(), "");

            if (!method.Signature.ReturnType.IsVoid)
            {
                if (isManagedReturnByRef)
                {
                    builder.BuildRet(builder.BuildLoad2(nativeReturnType, shadowStack, "returnValue"));
                }
                else
                {
                    builder.BuildRet(llvmReturnValue);
                }
            }
            else
            {
                builder.BuildRetVoid();
            }
        }

        private void EmitReadyToRunHeaderCallback(ModulesSectionNode node)
        {
            LLVMValueRef callback = _module.AddFunction("RtRHeaderWrapper", LLVMTypeRef.CreateFunction(_ptrType, Array.Empty<LLVMTypeRef>()));
            using LLVMBuilderRef builder = _module.Context.CreateBuilder();
            LLVMBasicBlockRef block = callback.AppendBasicBlock("Block");
            builder.PositionAtEnd(block);

            LLVMValueRef headerAddress = GetSymbolReferenceValue(node);
            builder.BuildRet(headerAddress);
        }

        private void GetCodeForReadyToRunGenericHelper(ReadyToRunGenericHelperNode node, NodeFactory factory)
        {
            if (node.Id == ReadyToRunHelperId.DelegateCtor)
            {
                GetCodeForDelegateCtorHelper(node);
                return;
            }

            LLVMTypeRef helperFuncType = LLVMTypeRef.CreateFunction(_ptrType, new[] { _ptrType /* shadow stack */, _ptrType /* generic context */ });
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(node.GetMangledName(factory.NameMangler), helperFuncType);

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

            LLVMTypeRef helperFuncType = LLVMTypeRef.CreateFunction(_ptrType, new[] { _ptrType /* shadow stack or "this" */ });
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(node.GetMangledName(factory.NameMangler), helperFuncType);

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
                            LLVMValueRef resolveFunc = GetOrCreateLLVMFunction("RhpResolveInterfaceMethod", resolveFuncType);

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

            string helperFuncName = node.GetMangledName(_compilation.NameMangler);
            LLVMTypeRef funcType = _compilation.GetLLVMSignatureForMethod(isManagedAbi: true, targetMethod.Signature, hasHiddenParam: false);
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(helperFuncName, funcType);

            using LLVMBuilderRef builder = _module.Context.CreateBuilder();
            builder.PositionAtEnd(helperFunc.AppendBasicBlock("VirtualCall"));

            LLVMValueRef objThis = builder.BuildLoad2(_ptrType, helperFunc.GetParam(0), "objThis");
            LLVMValueRef pTargetMethod = OutputCodeForVTableLookup(builder, objThis, targetMethod);

            LLVMValueRef[] args = new LLVMValueRef[helperFunc.ParamsCount];
            for (uint i = 0; i < args.Length; i++)
            {
                args[i] = helperFunc.GetParam(i);
            }

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
                helperFuncParams = new[] { _ptrType /* shadow stack */, _ptrType /* generic context */ };
            }
            else
            {
                delegateCreationInfo = (DelegateCreationInfo)((ReadyToRunHelperNode)node).Target;
                helperFuncParams = new[] { _ptrType /* shadow stack */ };
            }
            LLVMTypeRef helperFuncType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, helperFuncParams);
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(node.GetMangledName(factory.NameMangler), helperFuncType);

            // Incoming parameters are: (SS, [this], [targetObj], <GenericContext>). We will shadow tail call
            // the initializer routine, passing the "target" pointer as well as the invoke thunk, if present.
            //
            LLVMValueRef initializerFunc = GetOrCreateLLVMFunction(delegateCreationInfo.Constructor);
            LLVMTypeRef[] initializerFuncParamTypes = initializerFunc.GetValueType().ParamTypes;

            using LLVMBuilderRef builder = _module.Context.CreateBuilder();
            builder.PositionAtEnd(helperFunc.AppendBasicBlock(genericNode != null ? "GenericDelegateCtor" : "DelegateCtor"));

            LLVMValueRef shadowStack = helperFunc.GetParam(0);
            LLVMTypeRef targetValueType = initializerFuncParamTypes[1];
            LLVMValueRef targetValue;
            if (genericNode != null)
            {
                LLVMValueRef dictionary = OutputCodeForGetGenericDictionary(builder, helperFunc.GetParam(1), genericNode);
                targetValue = OutputCodeForDictionaryLookup(builder, factory, genericNode, genericNode.LookupSignature, dictionary);
            }
            else if (delegateCreationInfo.TargetNeedsVTableLookup)
            {
                LLVMValueRef addrOfTargetObj = CreateAddOffset(builder, shadowStack, factory.Target.PointerSize, "addrOfTargetObj");
                LLVMValueRef targetObjThis = builder.BuildLoad2(_ptrType, addrOfTargetObj, "targetObjThis");
                targetValue = OutputCodeForVTableLookup(builder, targetObjThis, delegateCreationInfo.TargetMethod);
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
            initializerArgs[0] = shadowStack;
            initializerArgs[1] = targetValue;
            if (delegateCreationInfo.Thunk != null)
            {
                LLVMValueRef thunkValue = GetOrCreateLLVMFunction(delegateCreationInfo.Thunk);
                initializerArgs[2] = builder.BuildPointerCast(thunkValue, initializerFuncParamTypes[2]);
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

            // Adjust "this" by the method table offset.
            LLVMValueRef shadowStack = unboxingLlvmFunc.GetParam(0);
            LLVMValueRef objThis = builder.BuildLoad2(_ptrType, shadowStack, "objThis");
            LLVMValueRef dataThis = CreateAddOffset(builder, objThis, factory.Target.PointerSize, "dataThis");
            builder.BuildStore(dataThis, shadowStack);

            // Pass the rest of arguments as-is.
            Debug.Assert(unboxingLlvmFunc.ParamsCount == unboxedLlvmFunc.ParamsCount);
            LLVMValueRef[] args = new LLVMValueRef[unboxingLlvmFunc.ParamsCount];
            for (uint i = 0; i < args.Length; i++)
            {
                args[i] = unboxingLlvmFunc.GetParam(i);
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
                string externFuncName = symbolRef.GetMangledName(_nodeFactory.NameMangler);
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
                string symbolDefName = symbolRef.GetMangledName(_compilation.NameMangler);
                symbolDefValue = _module.GetNamedAlias(symbolDefName);

                if (symbolDefValue.Handle == IntPtr.Zero)
                {
                    // Dummy aliasee; emission will fill in the real value.
                    LLVMValueRef aliasee = LLVMValueRef.CreateConstPointerNull(_ptrType);
                    symbolDefValue = _module.AddAlias2(LLVMTypeRef.Int8, 0, aliasee, symbolDefName);
                }
            }

            return symbolDefValue;
        }

        private LLVMValueRef GetSymbolReferenceValue(ISymbolNode symbolRef)
        {
            LLVMValueRef symbolRefValue = GetOrCreateSymbol(symbolRef);
            if (symbolRef.Offset != 0)
            {
                LLVMValueRef offsetValue = CreateConst(LLVMTypeRef.Int32, symbolRef.Offset);
                symbolRefValue = LLVMValueRef.CreateConstGEP2(LLVMTypeRef.Int8, symbolRefValue, new[] { offsetValue });
            }

            return symbolRefValue;
        }

        private LLVMValueRef GetOrCreateLLVMFunction(IMethodNode methodNode)
        {
            MethodDesc method = methodNode.Method;
            string methodName = methodNode.GetMangledName(_nodeFactory.NameMangler);
            LLVMValueRef methodFunc = GetOrCreateLLVMFunction(methodName, method.Signature, method.RequiresInstArg());

            return methodFunc;
        }

        private LLVMValueRef GetOrCreateLLVMFunction(string mangledName, MethodSignature signature, bool hasHiddenParam)
        {
            LLVMValueRef llvmFunction = _module.GetNamedFunction(mangledName);

            if (llvmFunction.Handle == IntPtr.Zero)
            {
                LLVMTypeRef llvmFuncType = _compilation.GetLLVMSignatureForMethod(isManagedAbi: true, signature, hasHiddenParam);
                return _module.AddFunction(mangledName, llvmFuncType);
            }
            return llvmFunction;
        }

        private LLVMValueRef GetOrCreateLLVMFunction(string mangledName, LLVMTypeRef functionType)
        {
            LLVMValueRef llvmFunction = _module.GetNamedFunction(mangledName);

            if (llvmFunction.Handle == IntPtr.Zero)
            {
                return _module.AddFunction(mangledName, functionType);
            }
            return llvmFunction;
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
    }
}
