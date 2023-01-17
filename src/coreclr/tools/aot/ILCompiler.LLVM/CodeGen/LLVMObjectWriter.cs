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

namespace ILCompiler.DependencyAnalysis
{
    /// <summary>
    /// Object writer that emits LLVM (bitcode).
    /// </summary>
    internal unsafe class LLVMObjectWriter : IDisposable
    {
        public static LLVMValueRef GetSymbolValuePointer(LLVMModuleRef module, ISymbolNode symbol, NameMangler nameMangler)
        {
            if (symbol is LLVMMethodCodeNode)
            {
                ThrowHelper.ThrowInvalidProgramException();
            }

            string symbolName = symbol.GetMangledName(nameMangler);
            LLVMValueRef symbolAddress = module.GetNamedAlias(symbolName);
            if (symbolAddress.Handle != IntPtr.Zero)
            {
                return symbolAddress;
            }

            // Dummy aliasee; object writer will fill in the real value.
            LLVMValueRef aliasee = LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 0);
            symbolAddress = module.AddAlias(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0), aliasee, symbolName);
            return symbolAddress;
        }

        // this is the llvm instance.
        public LLVMModuleRef Module { get; }

        public LLVMDIBuilderRef DIBuilder { get; }

        // Nodefactory for which ObjectWriter is instantiated for.
        private readonly NodeFactory _nodeFactory;

        // Path to the bitcode file we're emitting.
        private readonly string _objectFilePath;

        // Whether we are emitting a library (as opposed to executable).
        private readonly bool _nativeLib;

        // Raw data emitted for the current object node.
        private ArrayBuilder<byte> _currentObjectData = new ArrayBuilder<byte>();

        // References (pointers) to symbols the current object node contains (and thus depends on).
        private Dictionary<int, SymbolRefData> _currentObjectSymbolRefs = new Dictionary<int, SymbolRefData>();

        // Data to be emitted as LLVM bitcode after all of the object nodes have been processed.
        private readonly List<ObjectNodeDataEmission> _dataToFill = new List<ObjectNodeDataEmission>();

#if DEBUG
        private static readonly Dictionary<string, ISymbolNode> _previouslyWrittenNodeNames = new Dictionary<string, ISymbolNode>();
#endif

        public LLVMObjectWriter(string objectFilePath, LLVMCodegenCompilation compilation)
        {
            _nodeFactory = compilation.NodeFactory;
            _objectFilePath = objectFilePath;
            Module = LLVMCodegenCompilation.Module;
            DIBuilder = compilation.DIBuilder;
            _nativeLib = compilation.NativeLib;
        }

        public void Dispose() => Dispose(true);

        public virtual void Dispose(bool bDisposing)
        {
            FinishObjWriter(Module.Context);

            if (bDisposing)
            {
                GC.SuppressFinalize(this);
            }
        }

        ~LLVMObjectWriter()
        {
            Dispose(false);
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
                    ObjectNode node = depNode as ObjectNode;
                    if (node == null)
                        continue;

                    if (node.ShouldSkipEmittingObjectNode(factory))
                        continue;

                    if (node is ReadyToRunGenericHelperNode readyToRunGenericHelperNode)
                    {
                        objectWriter.GetCodeForReadyToRunGenericHelper(compilation, readyToRunGenericHelperNode, factory);
                        continue;
                    }

                    if (node is ReadyToRunHelperNode readyToRunHelperNode)
                    {
                        objectWriter.GetCodeForReadyToRunHelper(compilation, readyToRunHelperNode, factory);
                        continue;
                    }

                    if (node is TentativeMethodNode tentativeMethodNode)
                    {
                        objectWriter.GetCodeForTentativeMethod(compilation, tentativeMethodNode, factory);
                        continue;
                    }

                    if (node is ModulesSectionNode modulesSectionNode)
                    {
                        objectWriter.EmitReadyToRunHeaderCallback(modulesSectionNode, LLVMCodegenCompilation.Module.Context);
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
                objectWriter.Dispose();

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

            LLVMTypeRef intPtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0);
            LLVMTypeRef symbolType = LLVMTypeRef.CreateArray(intPtrType, (uint)countOfPointerSizedElements);
            LLVMValueRef dataSymbol = Module.AddGlobal(symbolType, dataSymbolName);

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

            LLVMValueRef symbolDef = Module.GetNamedAlias(symbolIdentifier);
            if (symbolDef.Handle == IntPtr.Zero)
            {
                Module.AddAlias(intPtrType, symbolAddress, symbolIdentifier);
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

            if (symbolName == null)
            {
                Console.WriteLine("Unable to generate symbolRef to " + target.GetMangledName(_nodeFactory.NameMangler));
                return;
            }

            if (symbolName == "RhpInitialDynamicInterfaceDispatch")
            {
                CreateDummyRhpInitialDynamicInterfaceDispatch();
            }

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
                Debug.Assert(valRef.Handle != IntPtr.Zero, $"Missing symbol: {SymbolName}");

                if (Offset != 0)
                {
                    LLVMTypeRef int8PtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
                    LLVMValueRef bitCast = LLVMValueRef.CreateConstBitCast(valRef, int8PtrType);
                    LLVMValueRef[] index = new LLVMValueRef[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, Offset, false) };
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
            var func = Module.GetNamedFunction(thunkSymbolName);
            if (func.Handle == IntPtr.Zero)
            {
                LLVMValueRef callee = Module.GetNamedFunction(unmanagedSymbolName);
                func = Module.AddFunction(thunkSymbolName,
                    LLVMTypeRef.CreateFunction(LLVMTypeRef.Void,
                        new LLVMTypeRef[] {
                            LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) /* shadow stack not used */,
                            LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) /* return spill slot */,
                            callee.TypeOf.ElementType.ParamTypes[0] /* MethodTable* */ }));
                LLVMBuilderRef builder = LLVMBuilderRef.Create(Module.Context);
                LLVMBasicBlockRef block = func.AppendBasicBlock("thunk");
                builder.PositionAtEnd(block);
                var ret = builder.BuildCall(Module.GetNamedFunction(unmanagedSymbolName), new LLVMValueRef[] { func.Params[2] });
                builder.BuildStore(ret, ILImporter.CastIfNecessary(builder, func.Params[1], LLVMTypeRef.CreatePointer(ret.TypeOf, 0)));
                builder.BuildRetVoid();
                builder.Dispose();
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

        private void CreateDummyRhpInitialDynamicInterfaceDispatch()
        {
            LLVMValueRef dummyFunc = Module.GetNamedFunction("RhpInitialDynamicInterfaceDispatch");

            if (dummyFunc.Handle != IntPtr.Zero) return;

            Module.AddFunction("RhpInitialDynamicInterfaceDispatch", LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { }));
        }

        private void FinishObjWriter(LLVMContextRef context)
        {
            // Since emission to llvm is delayed until after all nodes are emitted... emit now.
            foreach (var nodeData in _dataToFill)
            {
                nodeData.Fill(Module, _nodeFactory);
            }

            EmitReversePInvokesAndShadowStackBottom();

            if (_nativeLib)
            {
                EmitNativeStartup(context);
            }
            else
            {
                EmitNativeMain(context);
            }

            EmitDebugMetadata(context);
#if DEBUG
            Module.PrintToFile(Path.ChangeExtension(_objectFilePath, ".txt"));
#endif
            Module.Verify(LLVMVerifierFailureAction.LLVMAbortProcessAction);

            Module.WriteBitcodeToFile(_objectFilePath);
        }

        private void EmitDebugMetadata(LLVMContextRef context)
        {
            var dwarfVersion = LLVMValueRef.CreateMDNode(new[]
            {
                LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 2, false),
                context.GetMDString("Dwarf Version", 13),
                LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 4, false)
            });
            var dwarfSchemaVersion = LLVMValueRef.CreateMDNode(new[]
            {
                LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 2, false),
                context.GetMDString("Debug Info Version", 18),
                LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, 3, false)
            });
            Module.AddNamedMetadataOperand("llvm.module.flags", dwarfVersion);
            Module.AddNamedMetadataOperand("llvm.module.flags", dwarfSchemaVersion);
            DIBuilder.DIBuilderFinalize();
        }

        private void EmitReadyToRunHeaderCallback(ModulesSectionNode node, LLVMContextRef context)
        {
            LLVMTypeRef intPtr = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int32, 0);
            LLVMTypeRef intPtrPtr = LLVMTypeRef.CreatePointer(intPtr, 0);
            var callback = Module.AddFunction("RtRHeaderWrapper", LLVMTypeRef.CreateFunction(intPtrPtr, new LLVMTypeRef[0], false));
            var builder = context.CreateBuilder();
            var block = callback.AppendBasicBlock("Block");
            builder.PositionAtEnd(block);

            LLVMValueRef headerAddress = GetSymbolValuePointer(Module, node, _nodeFactory.NameMangler);
            LLVMValueRef castHeaderAddress = builder.BuildPointerCast(headerAddress, intPtrPtr, "castRtrHeaderPtr");
            builder.BuildRet(castHeaderAddress);
        }

        private void EmitReversePInvokesAndShadowStackBottom()
        {
            LLVMTypeRef reversePInvokeFrameType = LLVMTypeRef.CreateStruct(new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) }, false);
            LLVMValueRef rhpReversePInvoke = Module.GetNamedFunction("RhpReversePInvoke");

            if (rhpReversePInvoke.Handle == IntPtr.Zero)
            {
                Module.AddFunction("RhpReversePInvoke", LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(reversePInvokeFrameType, 0) }, false));
            }

            var shadowStackBottom = Module.AddGlobal(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), "t_pShadowStackBottom");
            shadowStackBottom.Linkage = LLVMLinkage.LLVMExternalLinkage;
            shadowStackBottom.Initializer = LLVMValueRef.CreateConstPointerNull(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
            shadowStackBottom.ThreadLocalMode = LLVMThreadLocalMode.LLVMLocalDynamicTLSModel;

            LLVMValueRef rhpReversePInvokeReturn = Module.GetNamedFunction("RhpReversePInvokeReturn");
            if (rhpReversePInvokeReturn.Handle == IntPtr.Zero)
            {
                LLVMTypeRef reversePInvokeFunctionType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(reversePInvokeFrameType, 0) }, false);
                Module.AddFunction("RhpReversePInvokeReturn", reversePInvokeFunctionType);
            }
        }

        private void EmitNativeMain(LLVMContextRef context)
        {
            LLVMValueRef shadowStackTop = Module.GetNamedGlobal("t_pShadowStackTop");

            LLVMBuilderRef builder = context.CreateBuilder();
            var mainSignature = LLVMTypeRef.CreateFunction(LLVMTypeRef.Int32, new LLVMTypeRef[] { LLVMTypeRef.Int32, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) }, false);
            var mainFunc = Module.AddFunction("__managed__Main", mainSignature);
            var mainEntryBlock = mainFunc.AppendBasicBlock("entry");
            builder.PositionAtEnd(mainEntryBlock);
            LLVMValueRef managedMain = Module.GetNamedFunction("StartupCodeMain");
            if (managedMain.Handle == IntPtr.Zero)
            {
                throw new Exception("Main not found");
            }

            LLVMTypeRef reversePInvokeFrameType = LLVMTypeRef.CreateStruct(new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) }, false);
            LLVMValueRef reversePinvokeFrame = builder.BuildAlloca(reversePInvokeFrameType, "ReversePInvokeFrame");
            LLVMValueRef rhpReversePInvoke = Module.GetNamedFunction("RhpReversePInvoke");

            builder.BuildCall(rhpReversePInvoke, new LLVMValueRef[] { reversePinvokeFrame }, "");

            var shadowStack = builder.BuildMalloc(LLVMTypeRef.CreateArray(LLVMTypeRef.Int8, 1000000), String.Empty);
            var castShadowStack = builder.BuildPointerCast(shadowStack, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), String.Empty);
            builder.BuildStore(castShadowStack, shadowStackTop);

            var shadowStackBottom = Module.GetNamedGlobal("t_pShadowStackBottom");
            builder.BuildStore(castShadowStack, shadowStackBottom);

            // Pass on main arguments
            LLVMValueRef argc = mainFunc.GetParam(0);
            LLVMValueRef argv = mainFunc.GetParam(1);

            LLVMValueRef mainReturn = builder.BuildCall(managedMain, new LLVMValueRef[]
            {
                castShadowStack,
                argc,
                argv,
            },
            "returnValue");

            LLVMValueRef rhpReversePInvokeReturn = Module.GetNamedFunction("RhpReversePInvokeReturn");
            LLVMTypeRef reversePInvokeFunctionType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(reversePInvokeFrameType, 0) }, false);
            builder.BuildCall(rhpReversePInvokeReturn, new LLVMValueRef[] { reversePinvokeFrame }, "");

            builder.BuildRet(mainReturn);
            mainFunc.Linkage = LLVMLinkage.LLVMExternalLinkage;
        }

        private void EmitNativeStartup(LLVMContextRef context)
        {
            LLVMValueRef shadowStackTop = Module.GetNamedGlobal("t_pShadowStackTop");

            LLVMBuilderRef builder = context.CreateBuilder();
            var startupSignature = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, new LLVMTypeRef[] { }, false);
            var startupFunc = Module.AddFunction("__managed__Startup", startupSignature);
            var mainEntryBlock = startupFunc.AppendBasicBlock("entry");
            builder.PositionAtEnd(mainEntryBlock);
            LLVMValueRef nativeLibStartup = Module.GetNamedFunction("Internal_CompilerGenerated__Module___NativeLibraryStartup");
            if (nativeLibStartup.Handle == IntPtr.Zero)
            {
                throw new Exception("NativeLibraryStartup not found");
            }

            LLVMTypeRef reversePInvokeFrameType = LLVMTypeRef.CreateStruct(new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) }, false);
            LLVMValueRef reversePinvokeFrame = builder.BuildAlloca(reversePInvokeFrameType, "ReversePInvokeFrame");
            LLVMValueRef rhpReversePInvoke = Module.GetNamedFunction("RhpReversePInvoke");

            builder.BuildCall(rhpReversePInvoke, new LLVMValueRef[] { reversePinvokeFrame }, "");

            var shadowStack = builder.BuildMalloc(LLVMTypeRef.CreateArray(LLVMTypeRef.Int8, 1000000), String.Empty);
            var castShadowStack = builder.BuildPointerCast(shadowStack, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), String.Empty);
            builder.BuildStore(castShadowStack, shadowStackTop);

            var shadowStackBottom = Module.GetNamedGlobal("t_pShadowStackBottom");
            builder.BuildStore(castShadowStack, shadowStackBottom);


            builder.BuildCall(nativeLibStartup, new LLVMValueRef[]
            {
                castShadowStack,
            });

            LLVMValueRef rhpReversePInvokeReturn = Module.GetNamedFunction("RhpReversePInvokeReturn");
            builder.BuildCall(rhpReversePInvokeReturn, new LLVMValueRef[] { reversePinvokeFrame }, "");

            builder.BuildRetVoid();
            startupFunc.Linkage = LLVMLinkage.LLVMExternalLinkage;
        }

        private static int GetVTableSlotsCount(NodeFactory factory, TypeDesc type)
        {
            if (type == null)
                return 0;
            int slotsOnCurrentType = factory.VTable(type).Slots.Count;
            return slotsOnCurrentType + GetVTableSlotsCount(factory, type.BaseType);
        }

        private void GetCodeForReadyToRunGenericHelper(LLVMCodegenCompilation compilation, ReadyToRunGenericHelperNode node, NodeFactory factory)
        {
            LLVMBuilderRef builder = LLVMCodegenCompilation.Module.Context.CreateBuilder();
            var args = new List<LLVMTypeRef>();
            MethodDesc delegateCtor = null;
            if (node.Id == ReadyToRunHelperId.DelegateCtor)
            {
                DelegateCreationInfo target = (DelegateCreationInfo)node.Target;
                delegateCtor = target.Constructor.Method;
                bool isStatic = delegateCtor.Signature.IsStatic;
                int argCount = delegateCtor.Signature.Length;
                if (!isStatic) argCount++;
                for (int i = 0; i < argCount; i++)
                {
                    TypeDesc argType;
                    if (i == 0 && !isStatic)
                    {
                        argType = delegateCtor.OwningType;
                    }
                    else
                    {
                        argType = delegateCtor.Signature[i - (isStatic ? 0 : 1)];
                    }
                    args.Add(ILImporter.GetLLVMTypeForTypeDesc(argType));
                }
            }

            LLVMValueRef helperFunc = Module.GetNamedFunction(node.GetMangledName(factory.NameMangler));

            if (helperFunc.Handle == IntPtr.Zero)
            {
                throw new Exception("if the function is requested here, it should have been created earlier");
            }
            var helperBlock = helperFunc.AppendBasicBlock("genericHelper");
            builder.PositionAtEnd(helperBlock);
            var importer = new ILImporter(builder, compilation, Module, helperFunc, delegateCtor);
            LLVMValueRef ctx;
            string gepName;
            if (node is ReadyToRunGenericLookupFromTypeNode)
            {
                // Locate the VTable slot that points to the dictionary
                int vtableSlot = VirtualMethodSlotHelper.GetGenericDictionarySlot(factory, (TypeDesc)node.DictionaryOwner);
            
                int pointerSize = factory.Target.PointerSize;
                // Load the dictionary pointer from the VTable
                int slotOffset = EETypeNode.GetVTableOffset(pointerSize) + (vtableSlot * pointerSize);
                var slotGep = builder.BuildGEP(helperFunc.GetParam(1), new[] {LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)slotOffset, false)}, "slotGep");
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

            LLVMValueRef resVar = OutputCodeForDictionaryLookup(builder, factory, node, node.LookupSignature, ctx, gepName);
            
            switch (node.Id)
            {
                case ReadyToRunHelperId.GetNonGCStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;
            
                        if (compilation.HasLazyStaticConstructor(target))
                        {
                            importer.OutputCodeForTriggerCctor(target, resVar);
                        }
                    }
                    break;
            
                case ReadyToRunHelperId.GetGCStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

                        var ptrPtr = builder.BuildBitCast(resVar, LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), "ptrPtr");

                        resVar = builder.BuildLoad(ptrPtr, "ind");
            
                        if (compilation.HasLazyStaticConstructor(target))
                        {
                            GenericLookupResult nonGcRegionLookup = factory.GenericLookup.TypeNonGCStaticBase(target);
                            var nonGcStaticsBase = OutputCodeForDictionaryLookup(builder, factory, node, nonGcRegionLookup, ctx, "lazyGep");
                            importer.OutputCodeForTriggerCctor(target, nonGcStaticsBase);
                        }
                    }
                    break;
            
                case ReadyToRunHelperId.GetThreadStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;
            
                        if (compilation.HasLazyStaticConstructor(target))
                        {
                            GenericLookupResult nonGcRegionLookup = factory.GenericLookup.TypeNonGCStaticBase(target);
                            var threadStaticBase = OutputCodeForDictionaryLookup(builder, factory, node, nonGcRegionLookup, ctx, "tsGep");
                            importer.OutputCodeForTriggerCctor(target, threadStaticBase);
                        }
                        resVar = importer.OutputCodeForGetThreadStaticBaseForType(resVar).ValueAsType(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), builder);
                    }
                    break;
            
                case ReadyToRunHelperId.DelegateCtor:
                    {
                        DelegateCreationInfo target = (DelegateCreationInfo)node.Target;
                        MethodDesc constructor = target.Constructor.Method;
                        var fatPtr = ILImporter.MakeFatPointer(builder, resVar, compilation);
                        importer.OutputCodeForDelegateCtorInit(builder, helperFunc, constructor, fatPtr);
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
                    break;
                default:
                    throw new NotImplementedException();
            }

            if (node.Id != ReadyToRunHelperId.DelegateCtor)
            {
                builder.BuildRet(resVar);
            }
            else
            {
                builder.BuildRetVoid();
            }
        }

        private void GetCodeForReadyToRunHelper(LLVMCodegenCompilation compilation, ReadyToRunHelperNode node, NodeFactory factory)
        {
            LLVMBuilderRef builder = LLVMCodegenCompilation.Module.Context.CreateBuilder();

            LLVMValueRef helperFunc = Module.AddFunction(node.GetMangledName(factory.NameMangler), LLVMTypeRef.CreateFunction(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), new LLVMTypeRef[] { LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0) /* shadow stack */}));

            var helperBlock = helperFunc.AppendBasicBlock("readyToRunHelper");
            builder.PositionAtEnd(helperBlock);
            var importer = new ILImporter(builder, compilation, Module, helperFunc, null);

            LLVMValueRef resVar;
            switch (node.Id)
            {
                case ReadyToRunHelperId.GetNonGCStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;
                        
                        ISymbolNode symbolNode = factory.TypeNonGCStaticsSymbol(target);
                        LLVMValueRef symbolAddress = GetSymbolValuePointer(Module, symbolNode, factory.NameMangler);

                        if (compilation.HasLazyStaticConstructor(target))
                        {
                            importer.OutputCodeForTriggerCctor(target, symbolAddress);
                        }
                        resVar = builder.BuildPointerCast(symbolAddress, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
                    }
                    break;

                case ReadyToRunHelperId.GetGCStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

                        if (compilation.HasLazyStaticConstructor(target))
                        {
                            ISymbolNode nonGcSymbolNode = factory.TypeNonGCStaticsSymbol(target);
                            LLVMValueRef nonGcBase = GetSymbolValuePointer(Module, nonGcSymbolNode, factory.NameMangler);

                            importer.OutputCodeForTriggerCctor(target, nonGcBase);
                        }

                        var symbolNode = factory.TypeGCStaticsSymbol(target);
                        LLVMValueRef basePtrPtr = GetSymbolValuePointer(Module, symbolNode, factory.NameMangler);
                        LLVMValueRef ptr = builder.BuildLoad(builder.BuildPointerCast(basePtrPtr, LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), "basePtr"), "base");
                        
                        resVar = builder.BuildPointerCast(ptr, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0));
                    }
                    break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                    {
                        MetadataType target = (MetadataType)node.Target;

                        resVar = importer.OutputCodeForTriggerCctorWithThreadStatic(target);
                    }
                    break;

                case ReadyToRunHelperId.DelegateCtor:
                    {
                        throw new NotImplementedException();
                        // DelegateCreationInfo target = (DelegateCreationInfo)node.Target;
                        // MethodDesc constructor = target.Constructor.Method;
                        // var fatPtr = ILImporter.MakeFatPointer(builder, resVar, compilation);
                        // importer.OutputCodeForDelegateCtorInit(builder, helperFunc, constructor, fatPtr);
                    }

                default:
                    throw new NotImplementedException();
            }

            if (node.Id == ReadyToRunHelperId.DelegateCtor)
            {
                builder.BuildRetVoid();
            }
            else
            {
                builder.BuildRet(resVar);
            }
        }

        private void GetCodeForTentativeMethod(LLVMCodegenCompilation compilation, TentativeMethodNode node, NodeFactory factory)
        {
            IMethodNode helperMethodNode = node.GetTarget(factory);
            string helperMangledName = helperMethodNode.GetMangledName(compilation.NameMangler);

            LLVMBuilderRef builder = LLVMCodegenCompilation.Module.Context.CreateBuilder();
            MethodDesc method = node.Method;
            string mangledName = node.GetMangledName(factory.NameMangler);
            LLVMValueRef tentativeStub = ILImporter.GetOrCreateLLVMFunction(Module, mangledName, method.Signature, method.RequiresInstArg());

            LLVMBasicBlockRef block = tentativeStub.AppendBasicBlock("tentativeStub");
            builder.PositionAtEnd(block);
            LLVMValueRef fn = ILImporter.GetOrCreateLLVMFunction(Module, helperMangledName, helperMethodNode.Method.Signature, false);
            builder.BuildCall(fn, new LLVMValueRef[] { tentativeStub.GetParam(0) }, string.Empty);
            builder.BuildUnreachable();
        }

        private LLVMValueRef OutputCodeForDictionaryLookup(LLVMBuilderRef builder, NodeFactory factory,
            ReadyToRunGenericHelperNode node, GenericLookupResult lookup, LLVMValueRef ctx, string gepName)
        {
            // Find the generic dictionary slot
            int dictionarySlot = factory.GenericDictionaryLayout(node.DictionaryOwner).GetSlotForEntry(lookup);
            int offset = dictionarySlot * factory.Target.PointerSize;

            // Load the generic dictionary cell
            LLVMValueRef retGep = builder.BuildGEP(ctx, new[] {LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)offset, false)}, "retGep");
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

    }
}

namespace Internal.IL
{
    partial class ILImporter
    {
        public ILImporter(LLVMBuilderRef builder, LLVMCodegenCompilation compilation, LLVMModuleRef module, LLVMValueRef helperFunc, MethodDesc delegateCtor)
        {
            this._builder = builder;
            this._compilation = compilation;
            this.Module = module;
            this._currentFunclet = helperFunc;
            _locals = new LocalVariableDefinition[0];
            if (delegateCtor == null)
            {
                _signature = new MethodSignature(MethodSignatureFlags.None, 0, GetWellKnownType(WellKnownType.Void),
                    new TypeDesc[0]);
            }
            else
            {
                _signature = delegateCtor.Signature;
                _argSlots = new LLVMValueRef[_signature.Length];
                int signatureIndex = 2; // past hidden param
                int thisOffset = 0;
                if (!_signature.IsStatic)
                {
                    thisOffset = 1;
                }
                for (int i = 0; i < _signature.Length; i++)
                {
                    if (CanStoreTypeOnStack(_signature[i]))
                    {
                        LLVMValueRef storageAddr;
                        LLVMValueRef argValue = helperFunc.GetParam((uint)signatureIndex);

                        // The caller will always pass the argument on the stack. If this function doesn't have 
                        // EH, we can put it in an alloca for efficiency and better debugging. Otherwise,
                        // copy it to the shadow stack so funclets can find it
                        int argOffset = i + thisOffset;
                        string argName = $"arg{argOffset}_";
                        storageAddr = _builder.BuildAlloca(GetLLVMTypeForTypeDesc(_signature[i]), argName);
                        _argSlots[i] = storageAddr;
                        _builder.BuildStore(argValue, storageAddr);
                        signatureIndex++;
                    }
                }
            }
            _thisType = GetWellKnownType(WellKnownType.Void);
            _pointerSize = compilation.NodeFactory.Target.PointerSize;
            _exceptionRegions = new ExceptionRegion[0];
            _handlerRegionsForOffsetLookup = new ExceptionRegion[0];
        }

        internal void OutputCodeForTriggerCctor(TypeDesc type, LLVMValueRef staticBaseValueRef)
        {
            IMethodNode helperNode = (IMethodNode)_compilation.NodeFactory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnNonGCStaticBase);
            TriggerCctor((MetadataType)helperNode.Method.OwningType, staticBaseValueRef, helperNode.Method.Name);
        }

        internal LLVMValueRef OutputCodeForTriggerCctorWithThreadStatic(MetadataType type)
        {
            bool needsCctorCheck = type.IsBeforeFieldInit && _compilation.HasLazyStaticConstructor(type); // TODO is this helpful for the helper method : || (!type.IsBeforeFieldInit && owningType != type);  For IL->LLVM, this is triggered at https://github.com/dotnet/runtimelab/blob/4632bfd7ef02878b387d878a121c35698eaa9af9/src/libraries/System.Private.CoreLib/src/System/Diagnostics/Tracing/EventSource.cs#L2678
            TriggerCctorWithThreadStaticStorage(type, needsCctorCheck, out ExpressionEntry returnExp);
            return returnExp.ValueAsType(returnExp.Type, _builder);
        }

        public void OutputCodeForDelegateCtorInit(LLVMBuilderRef builder, LLVMValueRef helperFunc,
            MethodDesc constructor,
            LLVMValueRef fatFunction)
        {
            StackEntry[] argValues = new StackEntry [constructor.Signature.Length + 1]; // for delegate this
            var shadowStack = helperFunc.GetParam(0);
            argValues[0] = new LoadExpressionEntry(StackValueKind.ObjRef, "this", shadowStack, GetWellKnownType(WellKnownType.Object));
            for (var i = 0; i < constructor.Signature.Length; i++)
            {
                if (i == 1)
                {
                    argValues[i + 1] = new ExpressionEntry(StackValueKind.Int32, "arg" + (i + 1),
                        builder.BuildIntToPtr(fatFunction, LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), "toPtr"), 
                        GetWellKnownType(WellKnownType.IntPtr));
                }
                else
                {
                    var argRef = LoadVarAddress(i + 1, LocalVarKind.Argument, out TypeDesc type);
                    var ptrPtr = builder.BuildPointerCast(argRef,
                        LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0), 0), "ptrPtr");
                    var loadArg = builder.BuildLoad(ptrPtr, "arg" + (i + 1));
                    argValues[i + 1] = new ExpressionEntry(GetStackValueKind(constructor.Signature[i]), "arg" + i + 1, loadArg,
                        constructor.Signature[i]);
                }
            }
            HandleCall(constructor, constructor.Signature, constructor, argValues, null);
        }
    }
}
