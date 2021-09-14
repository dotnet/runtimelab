// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using ILCompiler.DependencyAnalysisFramework;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public sealed class LLVMCodegenNodeFactory : NodeFactory
    {
        private NodeCache<MethodDesc, LLVMVTableSlotNode> _vTableSlotNodes;

        public LLVMCodegenNodeFactory(CompilerTypeSystemContext context, CompilationModuleGroup compilationModuleGroup, MetadataManager metadataManager,
            InteropStubManager interopStubManager, NameMangler nameMangler, VTableSliceProvider vtableSliceProvider, DictionaryLayoutProvider dictionaryLayoutProvider, PreinitializationManager preinitializationManager)
            : base(context, 
                  compilationModuleGroup, 
                  metadataManager, 
                  interopStubManager, 
                  nameMangler, 
                  new LazyGenericsDisabledPolicy(), 
                  vtableSliceProvider, 
                  dictionaryLayoutProvider, 
                  new ImportedNodeProviderThrowing(),
                  preinitializationManager)
        {
            _vTableSlotNodes = new NodeCache<MethodDesc, LLVMVTableSlotNode>(methodKey =>
            {
                return new LLVMVTableSlotNode(methodKey);
            });
        }

        public override bool IsCppCodegenTemporaryWorkaround => true;

        protected override IMethodNode CreateMethodEntrypointNode(MethodDesc method)
        {
            if (method.IsInternalCall)
            {
                if (TypeSystemContext.IsSpecialUnboxingThunkTargetMethod(method))
                {
                    return MethodEntrypoint(TypeSystemContext.GetRealSpecialUnboxingThunkTargetMethod(method));
                }
                if (method.IsArrayAddressMethod())
                {
                    return new LlvmMethodBodyNode(((ArrayType)method.OwningType).GetArrayMethod(ArrayMethodKind.AddressWithHiddenArg));
                }
            }
            if (CompilationModuleGroup.ContainsMethodBody(method, false))
            {
                // We might be able to optimize the method body away if the owning type was never seen as allocated.
                if (method.NotCallableWithoutOwningEEType() && CompilationModuleGroup.AllowInstanceMethodOptimization(method))
                    return new TentativeInstanceMethodNode(new LlvmMethodBodyNode(method));

                return new LlvmMethodBodyNode(method);
            }
            else
            {
                return new ExternMethodSymbolNode(this, method);
            }
        }

        public LLVMVTableSlotNode VTableSlot(MethodDesc method)
        {
            return _vTableSlotNodes.GetOrAdd(method);
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            if (method.IsCanonicalMethod(CanonicalFormKind.Specific) && !method.HasInstantiation)
            {
                // Unboxing stubs to canonical instance methods need a special unboxing stub that unboxes
                // 'this' and also provides an instantiation argument (we do a calling convention conversion).
                // We don't do this for generic instance methods though because they don't use the MethodTable
                // for the generic context anyway.
                return new LlvmMethodBodyNode(TypeSystemContext.GetSpecialUnboxingThunk(method, TypeSystemContext.GeneratedAssembly));
            }
            else
            {
                // Otherwise we just unbox 'this' and don't touch anything else.
                return new LlvmUnboxingThunkNode(TypeSystemContext.GetUnboxingThunk(method, TypeSystemContext.GeneratedAssembly));
            }
        }

        protected override ISymbolNode CreateReadyToRunHelperNode(ReadyToRunHelperKey helperCall)
        {
            return new ReadyToRunHelperNode(helperCall.HelperId, helperCall.Target);
        }

        protected override ISymbolNode CreateGenericLookupFromDictionaryNode(ReadyToRunGenericHelperKey helperKey)
        {
            return new LLVMReadyToRunGenericLookupFromDictionaryNode(this, helperKey.HelperId, helperKey.Target, helperKey.DictionaryOwner);
        }

        protected override ISymbolNode CreateGenericLookupFromTypeNode(ReadyToRunGenericHelperKey helperKey)
        {
            return new LLVMReadyToRunGenericLookupFromTypeNode(this, helperKey.HelperId, helperKey.Target, helperKey.DictionaryOwner);
        }
    }
}
