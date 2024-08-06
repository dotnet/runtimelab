// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;

using ILCompiler.DependencyAnalysis.Wasm;

using Internal.IL;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public partial class ReadyToRunHelperNode
    {
        protected override void EmitCode(NodeFactory factory, ref WasmEmitter encoder, bool relocsOnly)
        {
            WasmValueType wasmPointerType = encoder.GetNaturalIntType();

            switch (_id)
            {
                case ReadyToRunHelperId.GetNonGCStaticBase:
                {
                    MetadataType target = (MetadataType)Target;
                    bool hasLazyStaticConstructor = factory.PreinitializationManager.HasLazyStaticConstructor(target);

                    if (hasLazyStaticConstructor)
                    {
                        encoder.DefineLocals([(2, wasmPointerType)]);
                        const uint NonGcStaticBaseLocal = 1;
                        const uint CctorContextLocal = 2;

                        encoder.EmitNaturalConst(factory.TypeNonGCStaticsSymbol(target));
                        encoder.EmitCctorCheck(factory, NonGcStaticBaseLocal, CctorContextLocal);
                        encoder.EmitLocalGet(NonGcStaticBaseLocal);
                    }
                    else
                    {
                        encoder.DefineLocals();
                        encoder.EmitNaturalConst(factory.TypeNonGCStaticsSymbol(target));
                    }
                    encoder.EmitEnd();
                }
                break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                {
                    MetadataType target = (MetadataType)Target;
                    bool hasLazyStaticConstructor = factory.PreinitializationManager.HasLazyStaticConstructor(target);

                    encoder.DefineLocals([(1, wasmPointerType)]);
                    const uint TypeTlsInfoLocal = 1;

                    encoder.EmitLocalGet(0); // Shadow stack.
                    encoder.EmitNaturalConst(factory.TypeThreadStaticIndex(target));
                    encoder.EmitLocalTee(TypeTlsInfoLocal);
                    encoder.EmitLoad(wasmPointerType); // Address of the TypeManager slot.
                    encoder.EmitLocalGet(TypeTlsInfoLocal);
                    encoder.EmitLoad(WasmValueType.I32, (uint)factory.Target.PointerSize); // Index of the type in the ThreadStatic section.

                    if (hasLazyStaticConstructor)
                    {
                        // TODO-LLVM-CQ: implement the inline check.
                        encoder.EmitNaturalConst(factory.TypeNonGCStaticsSymbol(target), -NonGCStaticsNode.GetClassConstructorContextSize(factory.Target));
                        encoder.EmitCall(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnThreadStaticBase));
                    }
                    else
                    {
                        encoder.EmitCall(factory.HelperEntrypoint(HelperEntrypoint.GetThreadStaticBaseForType));
                    }
                    encoder.EmitEnd();
                }
                break;

                case ReadyToRunHelperId.GetGCStaticBase:
                {
                    MetadataType target = (MetadataType)Target;
                    bool hasLazyStaticConstructor = factory.PreinitializationManager.HasLazyStaticConstructor(target);

                    if (hasLazyStaticConstructor)
                    {
                        encoder.DefineLocals([(2, wasmPointerType)]);
                        encoder.EmitNaturalConst(factory.TypeNonGCStaticsSymbol(target));
                        encoder.EmitCctorCheck(factory, nonGcStaticBaseLocal: 1, cctorContextLocal: 2);
                    }
                    else
                    {
                        encoder.DefineLocals();
                    }
                    encoder.EmitNaturalConst(factory.TypeGCStaticsSymbol(target));
                    encoder.EmitLoad(wasmPointerType);
                    encoder.EmitEnd();
                }
                break;

                case ReadyToRunHelperId.DelegateCtor:
                {
                    DelegateCreationInfo target = (DelegateCreationInfo)Target;
                    const uint TargetObjArg = 2;

                    encoder.DefineLocals();
                    encoder.EmitLocalGet(0); // Shadow stack.
                    encoder.EmitLocalGet(1); // Delegate's "this".
                    encoder.EmitLocalGet(TargetObjArg); // Target object.

                    if (target.TargetNeedsVTableLookup)
                    {
                        if (!relocsOnly)
                        {
                            EmitVTableLookup(factory, ref encoder, TargetObjArg, target.TargetMethod);
                        }
                    }
                    else
                    {
                        encoder.EmitNaturalConst(target.GetTargetNode(factory));
                    }

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

                case ReadyToRunHelperId.ResolveVirtualFunction:
                {
                    MethodDesc targetMethod = (MethodDesc)Target;

                    encoder.DefineLocals();
                    const uint ObjThisArg = 1;

                    if (targetMethod.OwningType.IsInterface)
                    {
                        MetadataType helperType = factory.TypeSystemContext.SystemModule.GetKnownType("System.Runtime", "RuntimeImports");
                        MethodDesc helperMethod = helperType.GetKnownMethod("RhpResolveInterfaceMethod", null);

                        encoder.EmitLocalGet(0); // Shadow stack.
                        encoder.EmitLocalGet(ObjThisArg); // "this".
                        encoder.EmitNaturalConst(factory.InterfaceDispatchCell(targetMethod));
                        encoder.EmitCall(factory.MethodEntrypoint(helperMethod));
                    }
                    else if (!relocsOnly)
                    {
                        EmitVTableLookup(factory, ref encoder, ObjThisArg, targetMethod);
                    }
                    encoder.EmitEnd();
                }
                break;

                default:
                    throw new NotImplementedException();
            }
        }

        public override WasmFunctionType GetWasmFunctionType(NodeFactory factory)
        {
            WasmValueType wasmPointerType = WasmAbi.GetNaturalIntType(factory.Target);
            switch (_id)
            {
                case ReadyToRunHelperId.GetNonGCStaticBase:
                case ReadyToRunHelperId.GetThreadStaticBase:
                case ReadyToRunHelperId.GetGCStaticBase:
                    return new WasmFunctionType(wasmPointerType, [wasmPointerType]);
                case ReadyToRunHelperId.DelegateCtor:
                    // (Shadow stack, this, targetObj).
                    return new WasmFunctionType(WasmValueType.Invalid, [wasmPointerType, wasmPointerType, wasmPointerType]);
                case ReadyToRunHelperId.ResolveVirtualFunction:
                    return new WasmFunctionType(wasmPointerType, [wasmPointerType, wasmPointerType]);
                default:
                    throw new NotImplementedException();
            }
        }

        private static void EmitVTableLookup(NodeFactory factory, ref WasmEmitter encoder, uint thisObjLocal, MethodDesc method)
        {
            Debug.Assert(!method.CanMethodBeInSealedVTable(factory));
            int slot = VirtualMethodSlotHelper.GetVirtualMethodSlot(factory, method, method.OwningType);
            int slotOffset = EETypeNode.GetVTableOffset(factory.Target.PointerSize) + (slot * factory.Target.PointerSize);
            Debug.Assert(slot != -1);

            WasmValueType wasmPointerType = encoder.GetNaturalIntType();
            encoder.EmitLocalGet(thisObjLocal);
            encoder.EmitLoad(wasmPointerType); // [this] -> MethodTable*.
            encoder.EmitLoad(wasmPointerType, checked((uint)slotOffset)); // MethodTable*[slot] -> value.
        }
    }
}
