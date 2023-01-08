// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using ILCompiler.DependencyAnalysis.Wasm;
using Internal.IL;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    partial class ReadyToRunGenericHelperNode
    {
        protected sealed override void EmitCode(NodeFactory factory, ref WasmEmitter encoder, bool relocsOnly)
        {
            switch (_id)
            {
                case ReadyToRunHelperId.GetNonGCStaticBase:
                    {
                        MetadataType target = (MetadataType)_target;

                        if (factory.PreinitializationManager.HasLazyStaticConstructor(target))
                        {
                            encoder.Builder.EmitReloc(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnNonGCStaticBase), RelocType.IMAGE_REL_BASED_REL32);
                        }
                    }
                    break;

                case ReadyToRunHelperId.GetGCStaticBase:
                    {
                        MetadataType target = (MetadataType)_target;

                        if (factory.PreinitializationManager.HasLazyStaticConstructor(target))
                        {
                            encoder.Builder.EmitReloc(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnGCStaticBase), RelocType.IMAGE_REL_BASED_REL32);
                        }
                    }
                    break;

                case ReadyToRunHelperId.GetThreadStaticBase:
                    {
                        MetadataType target = (MetadataType)_target;

                        ISymbolNode helperEntrypoint;
                        if (factory.PreinitializationManager.HasLazyStaticConstructor(target))
                        {
                            helperEntrypoint = factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnThreadStaticBase);
                        }
                        else
                        {
                            helperEntrypoint = factory.HelperEntrypoint(HelperEntrypoint.GetThreadStaticBaseForType);
                        }

                        encoder.Builder.EmitReloc(helperEntrypoint, RelocType.IMAGE_REL_BASED_REL32);
                    }
                    break;

                case ReadyToRunHelperId.DelegateCtor:
                    {
                        var target = (DelegateCreationInfo)_target;

                        if (target.Thunk != null)
                        {
                            encoder.Builder.EmitReloc(target.Thunk, RelocType.IMAGE_REL_BASED_REL32);
                        }
                        else
                        {
                            Debug.Assert(target.Constructor.Method.Signature.Length == 2);
                        }

                        encoder.Builder.EmitReloc(target.Constructor, RelocType.IMAGE_REL_BASED_REL32);
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
                    // TODO-LLVM: should use GetBadSlotHelper/ThrowUnavailableType in base class when merged
                    break;
                default:
                    throw new NotImplementedException();
            }
        }
    }
}
