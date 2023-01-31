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

        // Module with the external functions and getters for them. Must be separate from the main module so as to avoid
        // direct calls with mismatched signatures at the LLVM level.
        private readonly LLVMModuleRef _moduleWithExternalFunctions;

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
            Module = LLVMCodegenCompilation.Module;
            DIBuilder = compilation.DIBuilder;

            _moduleWithExternalFunctions = LLVMModuleRef.CreateWithName("external");
            _moduleWithExternalFunctions.Target = Module.Target;
            _moduleWithExternalFunctions.DataLayout = Module.DataLayout;
            _nodeFactory = compilation.NodeFactory;
            _objectFilePath = objectFilePath;
            _nativeLib = compilation.NativeLib;
        }

        public void Dispose() => Dispose(true);

        public virtual void Dispose(bool bDisposing)
        {
            FinishObjWriter();

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
                    if (depNode is ExternSymbolNode externSymbolNode)
                    {
                        objectWriter.EmitExternalSymbol(externSymbolNode);
                        continue;
                    }

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

                    if (node is UnboxingStubNode unboxStubNode)
                    {
                        objectWriter.GetCodeForUnboxThunkMethod(compilation, unboxStubNode);
                        continue;
                    }

                    if (node is ExternMethodAccessorNode accessorNode)
                    {
                        objectWriter.GetCodeForExternMethodAccessor(compilation, accessorNode);
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

        private void EmitExternalSymbol(ExternSymbolNode node)
        {
            // Most external symbols (functions) have already been referenced by codegen. However, some are only
            // referenced by the compiler itself, in its data structures. Emit the declarations for them now.
            //
            string name = node.GetMangledName(_nodeFactory.NameMangler);
            if (Module.GetNamedFunction(name).Handle == IntPtr.Zero)
            {
                Debug.Assert(Module.GetNamedGlobal(name).Handle == IntPtr.Zero);
                Module.AddFunction(name, LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, Array.Empty<LLVMTypeRef>()));
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
                using LLVMBuilderRef builder = LLVMBuilderRef.Create(Module.Context);
                LLVMBasicBlockRef block = func.AppendBasicBlock("thunk");
                builder.PositionAtEnd(block);
                var ret = builder.BuildCall(Module.GetNamedFunction(unmanagedSymbolName), new LLVMValueRef[] { func.Params[2] });
                builder.BuildStore(ret, ILImporter.CastIfNecessary(builder, func.Params[1], LLVMTypeRef.CreatePointer(ret.TypeOf, 0)));
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
                nodeData.Fill(Module, _nodeFactory);
            }

            EmitReversePInvokesAndShadowStackBottom();

            LLVMContextRef context = Module.Context;
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
            _moduleWithExternalFunctions.Verify(LLVMVerifierFailureAction.LLVMAbortProcessAction);

            Module.WriteBitcodeToFile(_objectFilePath);
            _moduleWithExternalFunctions.WriteBitcodeToFile(Path.ChangeExtension(_objectFilePath, "external.bc"));
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

        private void GetCodeForReadyToRunGenericHelper(LLVMCodegenCompilation compilation, ReadyToRunGenericHelperNode node, NodeFactory factory)
        {
            if (node.Id == ReadyToRunHelperId.DelegateCtor)
            {
                GetCodeForDelegateCtorHelper(node);
                return;
            }

            LLVMTypeRef ptrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
            LLVMTypeRef helperFuncType = LLVMTypeRef.CreateFunction(ptrType, new[] { ptrType /* shadow stack */, ptrType /* generic context */ });
            LLVMValueRef helperFunc = ILImporter.GetOrCreateLLVMFunction(Module, node.GetMangledName(factory.NameMangler), helperFuncType);

            LLVMBuilderRef builder = LLVMCodegenCompilation.Module.Context.CreateBuilder();
            builder.PositionAtEnd(helperFunc.AppendBasicBlock("GenericReadyToRunHelper"));
            var importer = new ILImporter(builder, compilation, Module, helperFunc);

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

            builder.BuildRet(resVar);
        }

        private void GetCodeForReadyToRunHelper(LLVMCodegenCompilation compilation, ReadyToRunHelperNode node, NodeFactory factory)
        {
            if (node.Id == ReadyToRunHelperId.DelegateCtor)
            {
                GetCodeForDelegateCtorHelper(node);
                return;
            }

            LLVMTypeRef ptrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
            LLVMTypeRef helperFuncType = LLVMTypeRef.CreateFunction(ptrType, new[] { ptrType /* shadow stack */ });
            LLVMValueRef helperFunc = ILImporter.GetOrCreateLLVMFunction(Module, node.GetMangledName(factory.NameMangler), helperFuncType);

            using LLVMBuilderRef builder = LLVMCodegenCompilation.Module.Context.CreateBuilder();
            builder.PositionAtEnd(helperFunc.AppendBasicBlock("ReadyToRunHelper"));
            var importer = new ILImporter(builder, compilation, Module, helperFunc);

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

                default:
                    throw new NotImplementedException();
            }

            builder.BuildRet(resVar);
        }

        private void GetCodeForDelegateCtorHelper(AssemblyStubNode node)
        {
            NodeFactory factory = _nodeFactory;
            ReadyToRunGenericHelperNode genericNode = node as ReadyToRunGenericHelperNode;
            LLVMTypeRef ptrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);

            DelegateCreationInfo delegateCreationInfo;
            LLVMTypeRef[] helperFuncParams;
            if (genericNode != null)
            {
                delegateCreationInfo = (DelegateCreationInfo)genericNode.Target;
                helperFuncParams = new[] { ptrType /* shadow stack */, ptrType /* generic context */ };
            }
            else
            {
                delegateCreationInfo = (DelegateCreationInfo)((ReadyToRunHelperNode)node).Target;
                helperFuncParams = new[] { ptrType /* shadow stack */ };
            }
            LLVMTypeRef helperFuncType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, helperFuncParams);
            LLVMValueRef helperFunc = ILImporter.GetOrCreateLLVMFunction(Module, node.GetMangledName(factory.NameMangler), helperFuncType);

            // Incoming parameters are: (SS, [this], [targetObj], <GenericContext>). We will shadow tail call
            // the initializer routine, passing the "target" pointer as well as the invoke thunk, if present.
            //
            LLVMValueRef initializerFunc = GetOrCreateLLVMFunction(delegateCreationInfo.Constructor);
            LLVMTypeRef[] initializerFuncParamTypes = initializerFunc.GetValueType().ParamTypes;

            using LLVMBuilderRef builder = Module.Context.CreateBuilder();
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
                LLVMValueRef targetObjStackOffset = LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)factory.Target.PointerSize);
                LLVMValueRef addrOfTargetObj = builder.BuildGEP(shadowStack, new[] { targetObjStackOffset }, "addrOfTargetObj");
                LLVMValueRef addrOfTargetObjForLoad = builder.BuildBitCast(addrOfTargetObj,
                    LLVMTypeRef.CreatePointer(LLVMTypeRef.CreatePointer(ptrType, 0), 0), "addrOfTargetObjForLoad");
                LLVMValueRef targetObjThis = builder.BuildLoad(addrOfTargetObjForLoad, "targetObjThis");
                LLVMValueRef pMethodTable = builder.BuildLoad(targetObjThis, "pMethodTable");

                int slot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, delegateCreationInfo.TargetMethod, delegateCreationInfo.TargetMethod.OwningType);
                int slotOffset = EETypeNode.GetVTableOffset(factory.Target.PointerSize) + (slot * factory.Target.PointerSize);
                LLVMValueRef pSlot = builder.BuildGEP(pMethodTable, new[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, (ulong)slotOffset) }, "pSlot");
                LLVMValueRef pSlotForLoad = builder.BuildBitCast(pSlot, LLVMTypeRef.CreatePointer(targetValueType, 0), "pSlotForLoad");

                targetValue = builder.BuildLoad(pSlotForLoad, "pTarget");
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

        private void GetCodeForTentativeMethod(LLVMCodegenCompilation compilation, TentativeMethodNode node, NodeFactory factory)
        {
            LLVMValueRef tentativeStub = GetOrCreateLLVMFunction(node);
            LLVMValueRef helperFunc = GetOrCreateLLVMFunction(node.GetTarget(factory));

            using LLVMBuilderRef builder = Module.Context.CreateBuilder();
            LLVMBasicBlockRef block = tentativeStub.AppendBasicBlock("TentativeStub");
            builder.PositionAtEnd(block);

            builder.BuildCall(helperFunc, new LLVMValueRef[] { tentativeStub.GetParam(0) }, string.Empty);
            builder.BuildUnreachable();
        }

        private void GetCodeForUnboxThunkMethod(LLVMCodegenCompilation compilation, UnboxingStubNode node)
        {
            NodeFactory factory = compilation.NodeFactory;

            // This is the regular unboxing thunk that just does "Target(ref @this.Data, args...)".
            // Note how we perform a shadow tail call here while simultaneously overwriting "this".
            //
            LLVMValueRef unboxingLlvmFunc = GetOrCreateLLVMFunction(node);
            LLVMValueRef unboxedLlvmFunc = GetOrCreateLLVMFunction(node.GetUnderlyingMethodEntrypoint(factory));

            using LLVMBuilderRef builder = Module.Context.CreateBuilder();
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

        private void GetCodeForExternMethodAccessor(LLVMCodegenCompilation compilation, ExternMethodAccessorNode node)
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
                compilation.Logger.LogWarning(text, 3049, Path.GetFileNameWithoutExtension(_objectFilePath));

                externFuncType = LLVMTypeRef.CreateFunction(LLVMTypeRef.Void, Array.Empty<LLVMTypeRef>());
            }

            LLVMModuleRef externFuncModule = _moduleWithExternalFunctions;
            Debug.Assert(externFuncModule.GetNamedFunction(externFuncName).Handle == IntPtr.Zero);
            LLVMValueRef externFunc = externFuncModule.AddFunction(externFuncName, externFuncType);

            // Add import attributes if specified.
            if (compilation.ConfigurableWasmImportPolicy.TryGetWasmModule(externFuncName, out string wasmModuleName))
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

        private LLVMValueRef GetOrCreateLLVMFunction(IMethodNode methodNode)
        {
            MethodDesc method = methodNode.Method;
            string methodName = methodNode.GetMangledName(_nodeFactory.NameMangler);
            LLVMValueRef methodFunc = ILImporter.GetOrCreateLLVMFunction(Module, methodName, method.Signature, method.RequiresInstArg());

            return methodFunc;
        }

        private LLVMValueRef GetSymbolReferenceValue(ISymbolNode symbolRef)
        {
            if (symbolRef is IMethodNode { Offset: 0 } methodNode)
            {
                return GetOrCreateLLVMFunction(methodNode);
            }

            LLVMValueRef symbolRefValue = GetSymbolValuePointer(Module, symbolRef, _nodeFactory.NameMangler);
            uint offset = (uint)symbolRef.Offset;
            if (offset != 0)
            {
                LLVMTypeRef int8PtrType = LLVMTypeRef.CreatePointer(LLVMTypeRef.Int8, 0);
                LLVMValueRef bitCast = LLVMValueRef.CreateConstBitCast(symbolRefValue, int8PtrType);
                symbolRefValue = LLVMValueRef.CreateConstGEP(bitCast, new[] { LLVMValueRef.CreateConstInt(LLVMTypeRef.Int32, offset) });
            }

            return symbolRefValue;
        }
    }
}

namespace Internal.IL
{
    partial class ILImporter
    {
        public ILImporter(LLVMBuilderRef builder, LLVMCodegenCompilation compilation, LLVMModuleRef module, LLVMValueRef helperFunc)
        {
            this._builder = builder;
            this._compilation = compilation;
            this.Module = module;
            this._currentFunclet = helperFunc;
            _locals = new LocalVariableDefinition[0];
            _signature = new MethodSignature(MethodSignatureFlags.None, 0, GetWellKnownType(WellKnownType.Void), new TypeDesc[0]);
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
    }
}
