// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using ILCompiler;
using ILCompiler.DependencyAnalysisFramework;
using Internal.Text;
using Internal.TypeSystem;
using ObjectData = ILCompiler.DependencyAnalysis.ObjectNode.ObjectData;

using LLVMSharp.Interop;
using ILCompiler.DependencyAnalysis;
using Internal.IL;
using Internal.TypeSystem.Ecma;
using Internal.JitInterface;
using System.Runtime.InteropServices;

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Object writer that emits LLVM (bitcode).
    /// </summary>
    internal unsafe class LLVMObjectWriter
    {
        // Module with ILC-generated code and data.
        private readonly LLVMModuleRef _module;

        // Module with the external functions and getters for them. Must be separate from the main module so as to avoid
        // direct calls with mismatched signatures at the LLVM level.
        private readonly LLVMModuleRef _moduleWithExternalFunctions;

        // Node factory and compilation for which ObjectWriter was instantiated.
        private readonly LLVMCodegenCompilation _compilation;
        private readonly NodeFactory _nodeFactory;

        // Path to the bitcode file we're emitting.
        private readonly string _objectFilePath;

        // Raw data emitted for the current object node.
        private ArrayBuilder<byte> _currentObjectData = new ArrayBuilder<byte>();

        // References (pointers) to symbols the current object node contains (and thus depends on).
        private Dictionary<int, SymbolRefData> _currentObjectSymbolRefs = new Dictionary<int, SymbolRefData>();

        // Data to be emitted as LLVM bitcode after all of the object nodes have been processed.
        private readonly List<ObjectNodeDataEmission> _dataToFill = new List<ObjectNodeDataEmission>();

#if DEBUG
        private static readonly Dictionary<string, ISymbolNode> _previouslyWrittenNodeNames = new Dictionary<string, ISymbolNode>();
#endif

        private LLVMObjectWriter(string objectFilePath, LLVMCodegenCompilation compilation)
        {
            [DllImport("libLLVM", CallingConvention = CallingConvention.Cdecl)]
            static extern LLVMContextRef LLVMContextSetOpaquePointers(LLVMContextRef C, bool OpaquePointers);

            _module = LLVMModuleRef.CreateWithName(compilation.ModuleName);
            _module.Target = compilation.Target;
            _module.DataLayout = compilation.DataLayout;

            // TODO-LLVM: move to opaque pointers.
            LLVMContextSetOpaquePointers(_module.Context, false);

            _moduleWithExternalFunctions = LLVMModuleRef.CreateWithName("external");
            _moduleWithExternalFunctions.Target = compilation.Target;
            _moduleWithExternalFunctions.DataLayout = compilation.DataLayout;

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
                    if (depNode is ExternSymbolNode externSymbolNode)
                    {
                        objectWriter.EmitExternalSymbol(externSymbolNode);
                        continue;
                    }

                    if (depNode is LLVMMethodCodeNode { Method: { IsRuntimeExport: true } } runtimeExportNode)
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

                    if (dumper != null)
                    {
                        dumper.DumpObjectNode(factory.NameMangler, node, nodeContents);
                    }
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

        private LLVMValueRef EmitExternalSymbol(ExternSymbolNode node)
        {
            // Most external symbols (functions) have already been referenced by codegen. However, some are only
            // referenced by the compiler itself, in its data structures. Emit the declarations for them now.
            //
            string externFuncName = node.GetMangledName(_nodeFactory.NameMangler);
            LLVMValueRef externFunc = _module.GetNamedFunction(externFuncName);
            if (externFunc.Handle == IntPtr.Zero)
            {
                LLVMTypeRef funcType;
                if (IsRhpUnmanagedIndirection(externFuncName))
                {
                    LLVMTypeRef ptrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
                    funcType = LLVMTypeRef.CreateFunction(ptrType, new[] { ptrType });
                }
                else
                {
                    funcType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, Array.Empty<LLVMTypeRef>());
                }

                Debug.Assert(_module.GetNamedGlobal(externFuncName).Handle == IntPtr.Zero);
                externFunc = _module.AddFunction(externFuncName, funcType);
            }

            return externFunc;
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

            LLVMTypeRef intPtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0);
            LLVMTypeRef symbolType = LLVMTypeRef.CreateArray(intPtrType, (uint)countOfPointerSizedElements);
            LLVMValueRef dataSymbol = _module.AddGlobal(symbolType, dataSymbolName);

            // Indirections to RhpNew* unmanaged functions are made using delegate*s so get a shadow stack first argument which is not present in the function.
            // This IsRhpUnmanagedIndirection condition identifies those indirections and replaces the destination with a thunk which removes the shadow stack argument.
            if (IsRhpUnmanagedIndirection(symbolName))
            {
                _dataToFill.Add(new ObjectNodeDataEmission(dataSymbol, _currentObjectData.ToArray(), ReplaceIndirectionSymbolsWithThunks(_currentObjectSymbolRefs)));
            }
            else if (node is MethodGenericDictionaryNode)
            {
                _dataToFill.Add(new ObjectNodeDataEmission(dataSymbol, _currentObjectData.ToArray(), ReplaceMethodDictionaryUnmanagedSymbolsWithThunks(_currentObjectSymbolRefs)));
            }
            else
            {
                _dataToFill.Add(new ObjectNodeDataEmission(dataSymbol, _currentObjectData.ToArray(), _currentObjectSymbolRefs));
            }

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
            _currentObjectData = new ArrayBuilder<byte>();
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
            LLVMTypeRef intType = LLVMTypeRef.Int32;
            LLVMTypeRef int8PtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
            LLVMTypeRef intPtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0);

            LLVMValueRef symbolAddress;
            if (offsetFromBaseSymbol != 0)
            {
                LLVMValueRef baseSymbolAddress = LLVMValueRef.CreateConstBitCast(baseSymbol, int8PtrType);
                LLVMValueRef offsetValue = LLVMValueRef.CreateConstInt(intType, (uint)offsetFromBaseSymbol);
                symbolAddress = LLVMValueRef.CreateConstGEP(baseSymbolAddress, new LLVMValueRef[] { offsetValue });
            }
            else
            {
                symbolAddress = baseSymbol;
            }
            symbolAddress = LLVMValueRef.CreateConstBitCast(symbolAddress, intPtrType);

            LLVMValueRef symbolDef = _module.GetNamedAlias(symbolIdentifier);
            if (symbolDef.Handle == IntPtr.Zero)
            {
                _module.AddAlias(intPtrType, symbolAddress, symbolIdentifier);
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
                    LLVMTypeRef int8PtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
                    LLVMValueRef bitCast = LLVMValueRef.CreateConstBitCast(valRef, int8PtrType);
                    LLVMValueRef[] index = new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, Offset) };
                    valRef = LLVMValueRef.CreateConstGEP(bitCast, index);
                }

                return valRef;
            }
        }

        private struct ObjectNodeDataEmission
        {
            public ObjectNodeDataEmission(LLVMValueRef node, byte[] data, Dictionary<int, SymbolRefData> objectSymbolRefs)
            {
                Node = node;
                Data = data;
                ObjectSymbolRefs = objectSymbolRefs;
            }
            LLVMValueRef Node;
            readonly byte[] Data;
            readonly Dictionary<int, SymbolRefData> ObjectSymbolRefs;

            public void Fill(LLVMModuleRef module, NodeFactory nodeFactory)
            {
                List<LLVMValueRef> entries = new List<LLVMValueRef>();
                int pointerSize = nodeFactory.Target.PointerSize;

                int countOfPointerSizedElements = Data.Length / pointerSize;

                byte[] currentObjectData = Data;
                var intPtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0);

                for (int i = 0; i < countOfPointerSizedElements; i++)
                {
                    int curOffset = (i * pointerSize);
                    SymbolRefData symbolRef;
                    if (ObjectSymbolRefs.TryGetValue(curOffset, out symbolRef))
                    {
                        LLVMValueRef pointedAtValue = symbolRef.ToLLVMValueRef(module);
                        var ptrValue = LLVMValueRef.CreateConstBitCast(pointedAtValue, intPtrType);
                        entries.Add(ptrValue);
                    }
                    else
                    {
                        int value = BitConverter.ToInt32(currentObjectData, curOffset);
                        entries.Add(LLVMValueRef.CreateConstIntToPtr(LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (uint)value, false), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0)));
                    }
                }

                var funcptrarray = LLVMValueRef.CreateConstArray(intPtrType, entries.ToArray());
                Node.Initializer = funcptrarray;
            }
        }

        private Dictionary<int, SymbolRefData> ReplaceMethodDictionaryUnmanagedSymbolsWithThunks(Dictionary<int, SymbolRefData> unmanagedSymbolRefs)
        {
            foreach (KeyValuePair<int, SymbolRefData> keyValuePair in unmanagedSymbolRefs)
            {
                if (IsRhpUnmanagedIndirection(keyValuePair.Value.SymbolName))
                {
                    unmanagedSymbolRefs[keyValuePair.Key] = new SymbolRefData(EnsureIndirectionThunk(keyValuePair.Value.SymbolName), keyValuePair.Value.Offset);
                }
            }

            return unmanagedSymbolRefs;
        }

        private Dictionary<int, SymbolRefData> ReplaceIndirectionSymbolsWithThunks(Dictionary<int, SymbolRefData> unmanagedSymbolRefs)
        {
            Dictionary<int, SymbolRefData> thunks = new Dictionary<int, SymbolRefData>();
            foreach (KeyValuePair<int, SymbolRefData> keyValuePair in unmanagedSymbolRefs)
            {
                thunks[keyValuePair.Key] = new SymbolRefData(EnsureIndirectionThunk(keyValuePair.Value.SymbolName), keyValuePair.Value.Offset);
            }

            return thunks;
        }

        private string EnsureIndirectionThunk(string unmanagedSymbolName)
        {
            string thunkSymbolName = unmanagedSymbolName + "_Thunk";
            var thunkFunc = _module.GetNamedFunction(thunkSymbolName);
            if (thunkFunc.Handle == IntPtr.Zero)
            {
                LLVMValueRef callee = _module.GetNamedFunction(unmanagedSymbolName);
                if (callee.Handle == IntPtr.Zero)
                {
                    callee = EmitExternalSymbol(new ExternSymbolNode(unmanagedSymbolName));
                }

                thunkFunc = _module.AddFunction(thunkSymbolName,
                    LLVMTypeRef.CreateFunction(LLVMTypeRef.Void,
                        new[] { PtrType /* shadow stack, not used */, PtrType /* return spill slot */, PtrType /* MethodTable* */ }));
                using LLVMBuilderRef builder = LLVMBuilderRef.Create(_module.Context);
                builder.PositionAtEnd(thunkFunc.AppendBasicBlock("Thunk"));

                LLVMValueRef allocatedObj = builder.BuildCall(callee, new[] { thunkFunc.Params[2] });
                CreateStore(builder, thunkFunc.Params[1], allocatedObj);
                builder.BuildRetVoid();
            }

            return thunkSymbolName;
        }

        // hack to identify unmanaged symbols which dont accept a shadowstack arg.  Copy of names from JitHelper.GetNewObjectHelperForType
        private static bool IsRhpUnmanagedIndirection(string realName)
        {
            return realName.EndsWith("RhpNewFast")
                   || realName.EndsWith("RhpNewFinalizableAlign8")
                   || realName.EndsWith("RhpNewFastMisalign")
                   || realName.EndsWith("RhpNewFastAlign8")
                   || realName.EndsWith("RhpNewFinalizable");
        }

        private void FinishObjWriter()
        {
            // Since emission to llvm is delayed until after all nodes are emitted... emit now.
            foreach (var nodeData in _dataToFill)
            {
                nodeData.Fill(_module, _nodeFactory);
            }

#if DEBUG
            _module.PrintToFile(Path.ChangeExtension(_objectFilePath, ".txt"));
#endif
            _module.Verify(LLVMVerifierFailureAction.LLVMAbortProcessAction);
            _moduleWithExternalFunctions.Verify(LLVMVerifierFailureAction.LLVMAbortProcessAction);

            _module.WriteBitcodeToFile(_objectFilePath);
            _moduleWithExternalFunctions.WriteBitcodeToFile(Path.ChangeExtension(_objectFilePath, "external.bc"));
        }

        private void EmitRuntimeExportThunk(LLVMMethodCodeNode methodNode)
        {
            MethodDesc method = methodNode.Method;
            Debug.Assert(method.IsRuntimeExport && methodNode.CompilationCompleted);

            LLVMTypeRef[] llvmParams = new LLVMTypeRef[method.Signature.Length];
            for (int i = 0; i < llvmParams.Length; i++)
            {
                llvmParams[i] = _compilation.GetLLVMTypeForTypeDesc(method.Signature[i]);
            }

            string nativeName = _compilation.NodeFactory.GetSymbolAlternateName(methodNode);
            LLVMTypeRef thunkSig = LLVMTypeRef.CreateFunction(_compilation.GetLLVMTypeForTypeDesc(method.Signature.ReturnType), llvmParams, false);
            LLVMValueRef thunkFunc = GetOrCreateLLVMFunction(nativeName, thunkSig);

            using LLVMBuilderRef builder = _module.Context.CreateBuilder();
            LLVMBasicBlockRef block = thunkFunc.AppendBasicBlock("ManagedCallBlock");
            builder.PositionAtEnd(block);

            // Get the shadow stack. Since we are wrapping a runtime export, the caller is (by definition) managed, so we must have set up the shadow
            // stack already and can bypass the init check.
            LLVMTypeRef getShadowStackFuncSig = LLVMTypeRef.CreateFunction(PtrType, Array.Empty<LLVMTypeRef>());
            LLVMValueRef getShadowStackFunc = GetOrCreateLLVMFunction("RhpGetShadowStackTop", getShadowStackFuncSig);
            LLVMValueRef shadowStack = builder.BuildCall(getShadowStackFunc, Array.Empty<LLVMValueRef>());

            bool needsReturnSlot = LLVMCodegenCompilation.NeedsReturnStackSlot(method.Signature);

            int curOffset = 0;
            LLVMValueRef calleeFrame;
            if (needsReturnSlot)
            {
                curOffset = _compilation.PadNextOffset(method.Signature.ReturnType, curOffset);
                curOffset = _compilation.PadOffset(_compilation.TypeSystemContext.GetWellKnownType(WellKnownType.Object), curOffset); // Align the stack to pointer size.

                // Clear any uncovered object references for GC.Collect.
                LLVMValueRef lengthValue = CreateConst(LLVMTypeRef.Int32, curOffset);
                LLVMValueRef fillValue = CreateConst(LLVMTypeRef.Int8, 0);
                LLVMValueRef isVolatileValue = CreateConst(LLVMTypeRef.Int1, 0);
                LLVMTypeRef memsetFuncType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new[] { PtrType, LLVMTypeRef.Int8, LLVMTypeRef.Int32, LLVMTypeRef.Int1 });
                builder.BuildCall(GetOrCreateLLVMFunction("llvm.memset.p0i8.i32", memsetFuncType), new[] { shadowStack, fillValue, lengthValue, isVolatileValue }, "");

                calleeFrame = CreateAddOffset(builder, shadowStack, curOffset, "calleeFrame");
            }
            else
            {
                calleeFrame = shadowStack;
            }

            List<LLVMValueRef> llvmArgs = new List<LLVMValueRef>();
            llvmArgs.Add(calleeFrame);

            if (needsReturnSlot)
            {
                // Slot for return value if necessary
                llvmArgs.Add(shadowStack);
            }

            for (int i = 0; i < llvmParams.Length; i++)
            {
                LLVMValueRef argValue = thunkFunc.GetParam((uint)i);

                if (LLVMCodegenCompilation.CanStoreTypeOnStack(method.Signature[i]))
                {
                    llvmArgs.Add(argValue);
                }
                else
                {
                    curOffset = _compilation.PadOffset(method.Signature[i], curOffset);
                    LLVMValueRef argAddr = CreateAddOffset(builder, shadowStack, curOffset, $"arg{i}");
                    CreateStore(builder, argAddr, argValue);
                    curOffset = _compilation.PadNextOffset(method.Signature[i], curOffset);
                }
            }

            LLVMValueRef managedFunction = GetOrCreateLLVMFunction(methodNode);
            LLVMValueRef llvmReturnValue = builder.BuildCall(managedFunction, llvmArgs.ToArray(), "");

            if (!method.Signature.ReturnType.IsVoid)
            {
                if (needsReturnSlot)
                {
                    LLVMValueRef returnValue = CreateLoad(builder, shadowStack, managedFunction.GetValueType().ReturnType, "returnValue");
                    builder.BuildRet(returnValue);
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
            LLVMValueRef callback = _module.AddFunction("RtRHeaderWrapper", LLVMTypeRef.CreateFunction(PtrType, Array.Empty<LLVMTypeRef>()));
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

            LLVMTypeRef helperFuncType = LLVMTypeRef.CreateFunction(PtrType, new[] { PtrType /* shadow stack */, PtrType /* generic context */ });
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(node.GetMangledName(factory.NameMangler), helperFuncType);

            LLVMBuilderRef builder = _module.Context.CreateBuilder();
            builder.PositionAtEnd(helperFunc.AppendBasicBlock("GenericReadyToRunHelper"));

            LLVMValueRef ctx;
            string gepName;
            if (node is ReadyToRunGenericLookupFromTypeNode)
            {
                // Locate the VTable slot that points to the dictionary
                int vtableSlot = VirtualMethodSlotHelper.GetGenericDictionarySlot(factory, (TypeDesc)node.DictionaryOwner);

                int pointerSize = factory.Target.PointerSize;
                // Load the dictionary pointer from the VTable
                int slotOffset = EETypeNode.GetVTableOffset(pointerSize) + (vtableSlot * pointerSize);
                var slotGep = builder.BuildGEP(helperFunc.GetParam(1), new[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)slotOffset, false) }, "slotGep");
                var slotGepPtrPtr = builder.BuildPointerCast(slotGep,
                    LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), "slotGepPtrPtr");
                ctx = builder.BuildLoad(slotGepPtrPtr, "dictGep");
                gepName = "typeNodeGep";
            }
            else
            {
                ctx = helperFunc.GetParam(1);
                gepName = "paramGep";
            }

            LLVMValueRef result = OutputCodeForDictionaryLookup(builder, factory, node, node.LookupSignature, ctx, gepName);

            switch (node.Id)
            {
                case ReadyToRunHelperId.GetNonGCStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

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
                            GenericLookupResult nonGcBaseLookup = factory.GenericLookup.TypeNonGCStaticBase(target);
                            LLVMValueRef nonGcStaticsBase = OutputCodeForDictionaryLookup(builder, factory, node, nonGcBaseLookup, ctx, "lazyGep");
                            OutputCodeForTriggerCctor(builder, nonGcStaticsBase);
                        }

                        result = CreateLoad(builder, result, PtrType, "gcBase");
                    }
                    break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

                        if (_compilation.HasLazyStaticConstructor(target))
                        {
                            GenericLookupResult nonGcBaseLookup = factory.GenericLookup.TypeNonGCStaticBase(target);
                            LLVMValueRef nonGcStaticsBase = OutputCodeForDictionaryLookup(builder, factory, node, nonGcBaseLookup, ctx, "tsGep");
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

        private void GetCodeForReadyToRunHelper(ReadyToRunHelperNode node, NodeFactory factory)
        {
            if (node.Id == ReadyToRunHelperId.DelegateCtor)
            {
                GetCodeForDelegateCtorHelper(node);
                return;
            }

            LLVMTypeRef ptrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
            LLVMTypeRef helperFuncType = LLVMTypeRef.CreateFunction(ptrType, new[] { ptrType /* shadow stack */ });
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
                        result = CreateLoad(builder, result, PtrType, "gcBase");
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

                default:
                    throw new NotImplementedException();
            }

            builder.BuildRet(result);
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
                helperFuncParams = new[] { PtrType /* shadow stack */, PtrType /* generic context */ };
            }
            else
            {
                delegateCreationInfo = (DelegateCreationInfo)((ReadyToRunHelperNode)node).Target;
                helperFuncParams = new[] { PtrType /* shadow stack */ };
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
                targetValue = OutputCodeForDictionaryLookup(builder, factory, genericNode, genericNode.LookupSignature, helperFunc.GetParam(1), "pTarget");
            }
            else if (delegateCreationInfo.TargetNeedsVTableLookup)
            {
                LLVMValueRef addrOfTargetObj = CreateAddOffset(builder, shadowStack, factory.Target.PointerSize, "addrOfTargetObj");
                LLVMValueRef targetObjThis = CreateLoad(builder, addrOfTargetObj, PtrType, "targetObjThis");
                LLVMValueRef pMethodTable = CreateLoad(builder, targetObjThis, PtrType, "pMethodTable");

                int slot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, delegateCreationInfo.TargetMethod, delegateCreationInfo.TargetMethod.OwningType);
                int slotOffset = EETypeNode.GetVTableOffset(factory.Target.PointerSize) + (slot * factory.Target.PointerSize);
                LLVMValueRef pSlot = CreateAddOffset(builder, pMethodTable, slotOffset, "pSlot");

                targetValue = CreateLoad(builder, pSlot, targetValueType, "pTarget");
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

            builder.BuildCall(initializerFunc, initializerArgs);
            builder.BuildRetVoid();
        }

        private void GetCodeForTentativeMethod(TentativeMethodNode node, NodeFactory factory)
        {
            LLVMValueRef tentativeStub = GetOrCreateLLVMFunction(node);
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(node.GetTarget(factory));

            using LLVMBuilderRef builder = _module.Context.CreateBuilder();
            LLVMBasicBlockRef block = tentativeStub.AppendBasicBlock("TentativeStub");
            builder.PositionAtEnd(block);

            builder.BuildCall(helperFunc, new LLVMValueRef[] { tentativeStub.GetParam(0) }, string.Empty);
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
            LLVMValueRef addrOfThis = builder.BuildBitCast(shadowStack, LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), "addrOfThis");
            LLVMValueRef objThis = builder.BuildLoad(addrOfThis, "objThis");
            LLVMValueRef dataThis = builder.BuildGEP(objThis, new[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)factory.Target.PointerSize) }, "dataThis");
            builder.BuildStore(dataThis, addrOfThis);

            // Pass the rest of arguments as-is.
            Debug.Assert(unboxingLlvmFunc.ParamsCount == unboxedLlvmFunc.ParamsCount);
            LLVMValueRef[] args = new LLVMValueRef[unboxingLlvmFunc.ParamsCount];
            for (uint i = 0; i < args.Length; i++)
            {
                args[i] = unboxingLlvmFunc.GetParam(i);
            }

            LLVMValueRef unboxedCall = builder.BuildCall(unboxedLlvmFunc, args);
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
            LLVMTypeRef externFuncType = default;

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

        private LLVMValueRef OutputCodeForDictionaryLookup(LLVMBuilderRef builder, NodeFactory factory,
            ReadyToRunGenericHelperNode node, GenericLookupResult lookup, LLVMValueRef ctx, string gepName)
        {
            // Find the generic dictionary slot
            int dictionarySlot = factory.GenericDictionaryLayout(node.DictionaryOwner).GetSlotForEntry(lookup);
            int offset = dictionarySlot * factory.Target.PointerSize;

            // Load the generic dictionary cell
            LLVMValueRef retGep = builder.BuildGEP(ctx, new[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)offset, false) }, "retGep");
            LLVMValueRef castGep = builder.BuildBitCast(retGep, LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), "ptrPtr");
            LLVMValueRef retRef = builder.BuildLoad(castGep, gepName);

            switch (lookup.LookupResultReferenceType(factory))
            {
                case GenericLookupResultReferenceType.Indirect:
                    var ptrPtr = builder.BuildBitCast(retRef, LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), "ptrPtr");
                    retRef = builder.BuildLoad(ptrPtr, "indLoad");
                    break;

                case GenericLookupResultReferenceType.ConditionalIndirect:
                    throw new NotImplementedException();

                default:
                    break;
            }

            return retRef;
        }

        private void OutputCodeForTriggerCctor(LLVMBuilderRef builder, LLVMValueRef nonGcStaticBaseValue)
        {
            // We need to trigger the cctor before returning the base. It is stored at the beginning of the non-GC statics region.
            int pContextOffset = -NonGCStaticsNode.GetClassConstructorContextSize(_nodeFactory.Target);
            LLVMValueRef pContext = CreateAddOffset(builder, nonGcStaticBaseValue, pContextOffset, "pContext");
            LLVMValueRef initialized = CreateLoad(builder, nonGcStaticBaseValue, IntPtrType, "initialized");
            LLVMValueRef isInitialized = builder.BuildICmp(LLVMIntPredicate.LLVMIntEQ, initialized, CreateConst(IntPtrType, 1), "isInitialized");

            LLVMValueRef func = builder.InsertBlock.Parent;
            LLVMBasicBlockRef callHelperBlock = func.AppendBasicBlock("CallHelper");
            LLVMBasicBlockRef nextBlock = func.AppendBasicBlock("Return");
            builder.BuildCondBr(isInitialized, nextBlock, callHelperBlock);

            builder.PositionAtEnd(callHelperBlock);
            IMethodNode helperFuncNode = (IMethodNode)_nodeFactory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnGCStaticBase);
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(helperFuncNode);
            LLVMValueRef nonGcStaticBaseValueArg = builder.BuildPtrToInt(nonGcStaticBaseValue, IntPtrType);
            LLVMValueRef[] helperCallArgs = new[] { func.GetParam(0), pContext, nonGcStaticBaseValueArg };

            builder.BuildCall(helperFunc, helperCallArgs);
            builder.BuildBr(nextBlock);

            builder.PositionAtEnd(nextBlock);
        }

        private LLVMValueRef OutputCodeForGetThreadStaticBase(LLVMBuilderRef builder, LLVMValueRef pModuleDataSlot)
        {
            // Unfortunately, the helper to get the thread static base returns the pointer indirectly, so we have to
            // allocate some space on the shadow stack for it (note: the base is an unpinned object). TODO-LLVM-ABI:
            // simplify once we stop returning GC primitives by reference.
            LLVMValueRef shadowStack = builder.InsertBlock.Parent.GetParam(0);
            LLVMValueRef calleeShadowStack = CreateAddOffset(builder, shadowStack, _nodeFactory.Target.PointerSize, "calleeShadowStack");

            // First arg: address of the TypeManager slot that provides the helper with information about
            // module index and the type manager instance (which is used for initialization on first access).
            LLVMValueRef pModuleData = CreateLoad(builder, pModuleDataSlot, PtrType, "pModuleData");

            // Second arg: index of the type in the ThreadStatic section of the modules.
            LLVMValueRef pTypeTlsIndex = CreateAddOffset(builder, pModuleDataSlot, _nodeFactory.Target.PointerSize, "pTypeTlsIndex");
            LLVMValueRef typeTlsIndex = CreateLoad(builder, pTypeTlsIndex, LLVMTypeRef.Int32, "typeTlsIndex");

            IMethodNode getBaseHelperNode = (IMethodNode)_nodeFactory.HelperEntrypoint(HelperEntrypoint.GetThreadStaticBaseForType);
            LLVMValueRef getBaseHelperFunc = GetOrCreateLLVMFunction(getBaseHelperNode);
            LLVMValueRef[] getBaseHelperArgs = new[] { calleeShadowStack, shadowStack, pModuleData, typeTlsIndex };
            builder.BuildCall(getBaseHelperFunc, getBaseHelperArgs);

            return CreateLoad(builder, shadowStack, PtrType, "threadStaticBase");
        }

        private LLVMValueRef GetSymbolReferenceValue(ISymbolNode symbolRef)
        {
            LLVMValueRef symbolRefValue;
            if (symbolRef is IMethodNode { Offset: 0 } methodNode)
            {
                symbolRefValue = GetOrCreateLLVMFunction(methodNode);
            }
            else
            {
                string symbolRefName = symbolRef.GetMangledName(_compilation.NameMangler);
                symbolRefValue = _module.GetNamedAlias(symbolRefName);

                if (symbolRefValue.Handle == IntPtr.Zero)
                {
                    // Dummy aliasee; emission will fill in the real value.
                    LLVMValueRef aliasee = LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0);
                    symbolRefValue = _module.AddAlias(LLVMTypeRef.Int8, aliasee, symbolRefName);
                }
            }

            return symbolRefValue;
        }

        private LLVMTypeRef PtrType { get; } = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
        private LLVMTypeRef IntPtrType { get; }

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
                return _module.AddFunction(mangledName, _compilation.GetLLVMSignatureForMethod(signature, hasHiddenParam));
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

        private LLVMValueRef CreateLoad(LLVMBuilderRef builder, LLVMValueRef address, LLVMTypeRef type, string name)
        {
            if (address.TypeOf.ElementType != type)
            {
                address = builder.BuildBitCast(address, LLVMTypeRef.CreatePointer(type, 0), address.Name + "ForLoad");
            }

            return builder.BuildLoad2(type, address, name);
        }

        private LLVMValueRef CreateStore(LLVMBuilderRef builder, LLVMValueRef address, LLVMValueRef value)
        {
            if (address.TypeOf.ElementType != value.TypeOf)
            {
                address = builder.BuildBitCast(address, LLVMTypeRef.CreatePointer(value.TypeOf, 0), address.Name + "ForStore");
            }

            return builder.BuildStore(value, address);
        }

        private LLVMValueRef CreateAddOffset(LLVMBuilderRef builder, LLVMValueRef address, int offset, string name)
        {
            if (address.TypeOf.ElementType != LLVMTypeRef.Int8)
            {
                address = builder.BuildBitCast(address, PtrType, address.Name + "Cast");
            }

            if (offset != 0)
            {
                LLVMValueRef offsetValue = LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)offset);
                address = builder.BuildGEP2(LLVMTypeRef.Int8, address, new[] { offsetValue }, name);
            }

            return address;
        }

        private LLVMValueRef CreateConst(LLVMTypeRef type, int value) => LLVMValueRef.CreateConstInt(type, (ulong)value);
    }
}
