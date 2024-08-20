// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

using ILCompiler.DependencyAnalysis.Wasm;

using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public partial class ReadyToRunGenericHelperNode : IWasmFunctionNode
    {
        public WasmFunctionType GetWasmFunctionType(NodeFactory factory)
        {
            WasmValueType wasmPointerType = WasmAbi.GetNaturalIntType(factory.Target);
            switch (_id)
            {
                case ReadyToRunHelperId.GetNonGCStaticBase:
                case ReadyToRunHelperId.GetGCStaticBase:
                case ReadyToRunHelperId.GetThreadStaticBase:
                case ReadyToRunHelperId.TypeHandle:
                case ReadyToRunHelperId.MethodHandle:
                case ReadyToRunHelperId.FieldHandle:
                case ReadyToRunHelperId.MethodDictionary:
                case ReadyToRunHelperId.MethodEntry:
                case ReadyToRunHelperId.VirtualDispatchCell:
                case ReadyToRunHelperId.DefaultConstructor:
                case ReadyToRunHelperId.ObjectAllocator:
                case ReadyToRunHelperId.TypeHandleForCasting:
                case ReadyToRunHelperId.ConstrainedDirectCall:
                    return new WasmFunctionType(wasmPointerType, [wasmPointerType, wasmPointerType]);
                case ReadyToRunHelperId.DelegateCtor:
                    // (Shadow stack, this, targetObj, GenericContext).
                    return new WasmFunctionType(WasmValueType.Invalid, [wasmPointerType, wasmPointerType, wasmPointerType, wasmPointerType]);
                default:
                    throw new NotImplementedException();
            }
        }

        protected sealed override unsafe void EmitCode(NodeFactory factory, ref WasmEmitter encoder, bool relocsOnly)
        {
            uint InitializeLocals(uint argCount, uint* pHandlesInvalidEntriesLocal, uint* pNonGcStaticBaseLocal = null, uint* pCctorContextLocal = null)
            {
                uint localIndex = argCount;
                if (HandlesInvalidEntries(relocsOnly))
                {
                    *pHandlesInvalidEntriesLocal = localIndex++;
                }
                if ((pNonGcStaticBaseLocal != null) && TriggersLazyStaticConstructor(factory))
                {
                    *pNonGcStaticBaseLocal = localIndex++;
                    *pCctorContextLocal = localIndex++;
                }
                return localIndex - argCount;
            }

            const uint ContextArg = 1;

            WasmValueType wasmPointerType = encoder.GetNaturalIntType();
            uint localCount = 0;
            uint handlesInvalidEntriesLocal = WasmEmitter.InvalidIndex;
            uint nonGcStaticBaseLocal = WasmEmitter.InvalidIndex;
            uint cctorContextLocal = WasmEmitter.InvalidIndex;
            switch (_id)
            {
                case ReadyToRunHelperId.GetNonGCStaticBase:
                {
                    localCount = InitializeLocals(argCount: 2, &handlesInvalidEntriesLocal, &nonGcStaticBaseLocal, &cctorContextLocal);
                    encoder.DefineLocals([(localCount, wasmPointerType)]);
                    bool hasLazyStaticConstructor = cctorContextLocal != WasmEmitter.InvalidIndex;

                    EmitLoadGenericContext(factory, ref encoder, ContextArg, hasLazyStaticConstructor, relocsOnly);
                    EmitDictionaryLookup(factory, ref encoder, handlesInvalidEntriesLocal, _lookupSignature, relocsOnly);
                    if (hasLazyStaticConstructor)
                    {
                        encoder.EmitCctorCheck(factory, nonGcStaticBaseLocal, cctorContextLocal);
                        encoder.EmitLocalGet(nonGcStaticBaseLocal);
                    }
                    encoder.EmitEnd();
                }
                break;

                case ReadyToRunHelperId.GetGCStaticBase:
                {
                    MetadataType target = (MetadataType)_target;
                    localCount = InitializeLocals(argCount: 2, &handlesInvalidEntriesLocal, &nonGcStaticBaseLocal, &cctorContextLocal);
                    encoder.DefineLocals([(localCount, wasmPointerType)]);
                    bool hasLazyStaticConstructor = cctorContextLocal != WasmEmitter.InvalidIndex;

                    EmitLoadGenericContext(factory, ref encoder, ContextArg, hasLazyStaticConstructor, relocsOnly);
                    if (hasLazyStaticConstructor)
                    {
                        GenericLookupResult nonGcBaseLookup = factory.GenericLookup.TypeNonGCStaticBase(target);
                        EmitDictionaryLookup(factory, ref encoder, handlesInvalidEntriesLocal, nonGcBaseLookup, relocsOnly);
                        encoder.EmitCctorCheck(factory, nonGcStaticBaseLocal, cctorContextLocal);
                        encoder.EmitLocalGet(ContextArg);
                    }
                    EmitDictionaryLookup(factory, ref encoder, handlesInvalidEntriesLocal, _lookupSignature, relocsOnly);
                    encoder.EmitLoad(wasmPointerType);
                    encoder.EmitEnd();
                }
                break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                {
                    localCount = InitializeLocals(argCount: 2, &handlesInvalidEntriesLocal, &nonGcStaticBaseLocal, &cctorContextLocal);
                    uint typeTlsInfoLocal = 2 + localCount++;
                    encoder.DefineLocals([(localCount, wasmPointerType)]);
                    bool hasLazyStaticConstructor = cctorContextLocal != WasmEmitter.InvalidIndex;

                    encoder.EmitLocalGet(0);
                    EmitLoadGenericContext(factory, ref encoder, ContextArg, hasLazyStaticConstructor, relocsOnly);
                    EmitDictionaryLookup(factory, ref encoder, handlesInvalidEntriesLocal, _lookupSignature, relocsOnly);
                    encoder.EmitLocalTee(typeTlsInfoLocal);
                    encoder.EmitLoad(wasmPointerType); // Address of the TypeManager slot.
                    encoder.EmitLocalGet(typeTlsInfoLocal);
                    encoder.EmitLoad(WasmValueType.I32, (uint)factory.Target.PointerSize); // Index of the type in the ThreadStatic section.

                    if (hasLazyStaticConstructor)
                    {
                        GenericLookupResult nonGcRegionLookup = factory.GenericLookup.TypeNonGCStaticBase((MetadataType)_target);
                        encoder.EmitLocalGet(ContextArg);
                        EmitDictionaryLookup(factory, ref encoder, handlesInvalidEntriesLocal, nonGcRegionLookup, relocsOnly);
                        encoder.EmitNaturalAddConst(-NonGCStaticsNode.GetClassConstructorContextSize(factory.Target));
                        encoder.EmitCall(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnThreadStaticBase));
                    }
                    else
                    {
                        encoder.EmitCall(factory.HelperEntrypoint(HelperEntrypoint.GetThreadStaticBaseForType));
                    }
                    encoder.EmitEnd();
                }
                break;

                case ReadyToRunHelperId.DelegateCtor:
                {
                    // This is a weird helper. Codegen populated Arg0 and Arg1 with the values that the constructor
                    // method expects. Codegen also passed us the generic context in Arg2.
                    // We now need to load the delegate target method into Arg2 (using a dictionary lookup)
                    // and the optional 4th parameter, and call the ctor.
                    DelegateCreationInfo target = (DelegateCreationInfo)_target;
                    localCount = InitializeLocals(argCount: 4, &handlesInvalidEntriesLocal);
                    encoder.DefineLocals([(localCount, wasmPointerType)]);

                    const uint DelContextArg = 3;
                    encoder.EmitLocalGet(0); // Shadow stack.
                    encoder.EmitLocalGet(1); // Delegate "this".
                    encoder.EmitLocalGet(2); // Target object.
                    EmitLoadGenericContext(factory, ref encoder, DelContextArg, saveIntoContextLocal: false, relocsOnly);
                    EmitDictionaryLookup(factory, ref encoder, handlesInvalidEntriesLocal, _lookupSignature, relocsOnly);
                    if (target.Thunk != null)
                    {
                        Debug.Assert(target.Constructor.Method.Signature.Length == 3);
                        encoder.EmitNaturalConst(target.Thunk);
                    }
                    else
                    {
                        Debug.Assert(target.Constructor.Method.Signature.Length == 2);
                    }
                    encoder.EmitCall(target.Constructor);
                    encoder.EmitEnd();
                }
                break;

                case ReadyToRunHelperId.TypeHandle:
                case ReadyToRunHelperId.MethodHandle:
                case ReadyToRunHelperId.FieldHandle:
                case ReadyToRunHelperId.MethodDictionary:
                case ReadyToRunHelperId.MethodEntry:
                case ReadyToRunHelperId.VirtualDispatchCell:
                case ReadyToRunHelperId.DefaultConstructor:
                case ReadyToRunHelperId.ObjectAllocator:
                case ReadyToRunHelperId.TypeHandleForCasting:
                case ReadyToRunHelperId.ConstrainedDirectCall:
                    localCount = InitializeLocals(argCount: 2, &handlesInvalidEntriesLocal);
                    encoder.DefineLocals([(localCount, wasmPointerType)]);
                    EmitLoadGenericContext(factory, ref encoder, ContextArg, saveIntoContextLocal: false, relocsOnly);
                    EmitDictionaryLookup(factory, ref encoder, handlesInvalidEntriesLocal, _lookupSignature, relocsOnly);
                    encoder.EmitEnd();
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        protected virtual void EmitLoadGenericContext(NodeFactory factory, ref WasmEmitter encoder, uint contextLocal, bool saveIntoContextLocal, bool relocsOnly)
        {
            encoder.EmitLocalGet(contextLocal); // The context local is already a dictionary.
        }

        private void EmitDictionaryLookup(NodeFactory factory, ref WasmEmitter encoder, uint handlesBadEntriesLocal, GenericLookupResult lookup, bool relocsOnly)
        {
            // Find the generic dictionary slot
            int dictionarySlot = 0;
            if (!relocsOnly)
            {
                // The concrete slot won't be known until we're emitting data - don't ask for it in relocsOnly.
                if (!factory.GenericDictionaryLayout(_dictionaryOwner).TryGetSlotForEntry(lookup, out dictionarySlot))
                {
                    encoder.EmitNaturalConst(0);
                    return;
                }
            }

            // Load the generic dictionary cell. The caller passed the dictionary on the operand stack.
            WasmValueType wasmPointerType = encoder.GetNaturalIntType();
            encoder.EmitLoad(wasmPointerType, (uint)(dictionarySlot * factory.Target.PointerSize));

            // If there's any invalid entries, we need to test for them
            //
            // Skip this in relocsOnly to make it easier to weed out bugs - the _hasInvalidEntries
            // flag can change over the course of compilation and the bad slot helper dependency
            // should be reported by someone else - the system should not rely on it coming from here.
            if (HandlesInvalidEntries(relocsOnly))
            {
                encoder.EmitLocalTee(handlesBadEntriesLocal); // If only we had dup...
                encoder.EmitNaturalEQZ();
                encoder.EmitIf();
                {
                    WasmValueType callerReturnType = _id is ReadyToRunHelperId.DelegateCtor ? WasmValueType.Invalid : wasmPointerType;
                    WasmValueType calleeReturnType = WasmValueType.Invalid;
                    Debug.Assert(WasmAbi.GetWasmReturnType(GetBadSlotHelper(factory).Method, out _) == WasmValueType.Invalid);

                    encoder.EmitLocalGet(0);
                    encoder.EmitCall(GetBadSlotHelper(factory));
                    encoder.EmitReturnAfterAlwaysThrowCall(callerReturnType, calleeReturnType, isEnd: false);
                }
                encoder.EmitEnd();
                encoder.EmitLocalGet(handlesBadEntriesLocal);
            }
        }

        private bool HandlesInvalidEntries(bool relocsOnly) => !relocsOnly && _hasInvalidEntries;
    }

    public partial class ReadyToRunGenericLookupFromTypeNode
    {
        protected override void EmitLoadGenericContext(NodeFactory factory, ref WasmEmitter encoder, uint contextLocal, bool saveIntoContextLocal, bool relocsOnly)
        {
            if (relocsOnly)
            {
                return;
            }

            // Locate the VTable slot that points to the dictionary and load it from the VTable.
            int pointerSize = factory.Target.PointerSize;
            int vtableSlot = VirtualMethodSlotHelper.GetGenericDictionarySlot(factory, (TypeDesc)_dictionaryOwner);
            int slotOffset = EETypeNode.GetVTableOffset(pointerSize) + (vtableSlot * pointerSize);
            encoder.EmitLocalGet(contextLocal);
            encoder.EmitLoad(encoder.GetNaturalIntType(), checked((uint)slotOffset));
            if (saveIntoContextLocal)
            {
                encoder.EmitLocalTee(contextLocal);
            }
        }
    }
}
