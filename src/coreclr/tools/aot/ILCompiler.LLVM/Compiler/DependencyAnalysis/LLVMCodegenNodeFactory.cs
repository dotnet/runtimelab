// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ILCompiler.DependencyAnalysisFramework;

using Internal.JitInterface;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    public sealed class LLVMCodegenNodeFactory : NodeFactory
    {
        private readonly Dictionary<string, ExternMethodAccessorNode> _externSymbolsWithAccessors = new();
        private readonly NodeCache<MethodDesc, LLVMVTableSlotNode> _vTableSlotNodes;

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
                else if (TypeSystemContext.IsDefaultInterfaceMethodImplementationThunkTargetMethod(method))
                {
                    return MethodEntrypoint(TypeSystemContext.GetRealDefaultInterfaceMethodImplementationThunkTargetMethod(method));
                }
                else if (method.IsArrayAddressMethod())
                {
                    return MethodEntrypoint(((ArrayType)method.OwningType).GetArrayMethod(ArrayMethodKind.AddressWithHiddenArg));
                }
                else if (method.HasCustomAttribute("System.Runtime", "RuntimeImportAttribute"))
                {
                    return new RuntimeImportMethodNode(method);
                }
            }
            if (CompilationModuleGroup.ContainsMethodBody(method, false))
            {
                // TODO-LLVM: delete when merging https://github.com/dotnet/runtime/pull/80414 (Jan 10, 2023)
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

        internal ExternMethodAccessorNode ExternSymbolWithAccessor(string name, MethodDesc method, ReadOnlySpan<TargetAbiType> sig)
        {
            Dictionary<string, ExternMethodAccessorNode> map = _externSymbolsWithAccessors;

            // Not lockless since we mutate the node. Contention on this path is not expected.
            //
            lock (map)
            {
                ref ExternMethodAccessorNode node = ref CollectionsMarshal.GetValueRefOrAddDefault(map, name, out bool exists);

                if (!exists)
                {
                    node = new ExternMethodAccessorNode(name);
                    node.Signature = sig.ToArray();
                }
                else if (!node.Signature.AsSpan().SequenceEqual(sig))
                {
                    // We have already seen this name with a different signature. Currently, we don't try to disambiguate.
                    node.Signature = null;
                }

                node.AddMethod(method);
                return node;
            }
        }

        internal LLVMVTableSlotNode VTableSlot(MethodDesc method)
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
                return new UnboxingStubNode(method, Target);
            }
        }

        protected override ISymbolNode CreateReadyToRunHelperNode(ReadyToRunHelperKey helperCall)
        {
            return new ReadyToRunHelperNode(helperCall.HelperId, helperCall.Target);
        }
    }
}
