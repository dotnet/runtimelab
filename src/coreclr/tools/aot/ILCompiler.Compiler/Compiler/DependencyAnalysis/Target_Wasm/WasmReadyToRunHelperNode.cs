// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using ILCompiler.DependencyAnalysis.Wasm;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public partial class ReadyToRunHelperNode
    {
        protected override void EmitCode(NodeFactory factory, ref WasmEmitter encoder, bool relocsOnly)
        {
            switch (Id)
            {
                case ReadyToRunHelperId.VirtualCall:
                    break;

                case ReadyToRunHelperId.GetNonGCStaticBase:
                    {
                        MetadataType target = (MetadataType)Target;

                        bool hasLazyStaticConstructor = factory.PreinitializationManager.HasLazyStaticConstructor(target);
                        encoder.Builder.EmitReloc(factory.TypeNonGCStaticsSymbol(target), RelocType.IMAGE_REL_BASED_REL32);

                        if (hasLazyStaticConstructor)
                        {
                            encoder.Builder.EmitReloc(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnNonGCStaticBase), RelocType.IMAGE_REL_BASED_REL32);
                        }
                    }
                    break;
                case ReadyToRunHelperId.GetThreadStaticBase:
                    {
                        MetadataType target = (MetadataType)Target;

                        encoder.Builder.EmitReloc(factory.TypeThreadStaticIndex(target), RelocType.IMAGE_REL_BASED_REL32);

                        if (!factory.PreinitializationManager.HasLazyStaticConstructor(target))
                        {
                            encoder.Builder.EmitReloc(factory.HelperEntrypoint(HelperEntrypoint.GetThreadStaticBaseForType), RelocType.IMAGE_REL_BASED_REL32);
                        }
                        else
                        {
                            encoder.Builder.EmitReloc(factory.TypeNonGCStaticsSymbol(target), RelocType.IMAGE_REL_BASED_REL32);
                            encoder.Builder.EmitReloc(factory.HelperEntrypoint(HelperEntrypoint.GetThreadStaticBaseForType), RelocType.IMAGE_REL_BASED_REL32);
                            encoder.Builder.EmitReloc(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnThreadStaticBase), RelocType.IMAGE_REL_BASED_REL32);
                        }
                    }
                    break;
                case ReadyToRunHelperId.GetGCStaticBase:
                    {
                        MetadataType target = (MetadataType)Target;

                        encoder.Builder.EmitReloc(factory.TypeGCStaticsSymbol(target), RelocType.IMAGE_REL_BASED_REL32);

                        if (factory.PreinitializationManager.HasLazyStaticConstructor(target))
                        {
                            encoder.Builder.EmitReloc(factory.TypeNonGCStaticsSymbol(target), RelocType.IMAGE_REL_BASED_REL32);
                            encoder.Builder.EmitReloc(factory.HelperEntrypoint(HelperEntrypoint.EnsureClassConstructorRunAndReturnGCStaticBase), RelocType.IMAGE_REL_BASED_REL32);
                        }
                    }
                    break;

                case ReadyToRunHelperId.DelegateCtor:
                    {
                        DelegateCreationInfo target = (DelegateCreationInfo)Target;

                        if (!target.TargetNeedsVTableLookup)
                        {
                            encoder.Builder.EmitReloc(target.GetTargetNode(factory), RelocType.IMAGE_REL_BASED_REL32);
                        }

                        if (target.Thunk != null)
                        {
                            Debug.Assert(target.Constructor.Method.Signature.Length == 3);
                            encoder.Builder.EmitReloc(target.Thunk, RelocType.IMAGE_REL_BASED_REL32);
                        }
                        else
                        {
                            Debug.Assert(target.Constructor.Method.Signature.Length == 2);
                        }

                        encoder.Builder.EmitReloc(target.Constructor, RelocType.IMAGE_REL_BASED_REL32);
                    }
                    break;

                case ReadyToRunHelperId.ResolveVirtualFunction:
                    {
                        MethodDesc targetMethod = (MethodDesc)Target;
                        if (targetMethod.OwningType.IsInterface)
                        {
                            encoder.Builder.EmitReloc(factory.InterfaceDispatchCell(targetMethod), RelocType.IMAGE_REL_BASED_REL32);
                            encoder.Builder.EmitReloc(factory.ExternSymbol("RhpResolveInterfaceMethod"), RelocType.IMAGE_REL_BASED_REL32);
                        }
                    }
                    break;

                default:
                    throw new NotImplementedException();
            }
        }
    }
}
