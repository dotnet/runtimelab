// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Internal.JitInterface;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler.DependencyAnalysis
{
    public sealed class LLVMCodegenNodeFactory : NodeFactory
    {
        private readonly Dictionary<string, ExternMethodAccessorNode> _externSymbolsWithAccessors = new();

        public LLVMCodegenNodeFactory(
            CompilerTypeSystemContext context,
            CompilationModuleGroup compilationModuleGroup,
            MetadataManager metadataManager,
            InteropStubManager interopStubManager,
            NameMangler nameMangler,
            VTableSliceProvider vtableSliceProvider,
            DictionaryLayoutProvider dictionaryLayoutProvider,
            InlinedThreadStatics inlinedThreadStatics,
            PreinitializationManager preinitializationManager,
            DevirtualizationManager devirtualizationManager,
            ObjectDataInterner dataInterner)
            : base(context,
                  compilationModuleGroup,
                  metadataManager,
                  interopStubManager,
                  nameMangler,
                  new LazyGenericsDisabledPolicy(),
                  vtableSliceProvider,
                  dictionaryLayoutProvider,
                  inlinedThreadStatics,
                  new ImportedNodeProviderThrowing(),
                  preinitializationManager,
                  devirtualizationManager,
                  dataInterner)
        {
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
                    return new RuntimeImportMethodNode(method, NameMangler);
                }
            }
            if (CompilationModuleGroup.ContainsMethodBody(method, false))
            {
                return new LLVMMethodCodeNode(method);
            }
            else
            {
                return new ExternMethodSymbolNode(this, method);
            }
        }

        protected override IMethodNode CreateUnboxingStubNode(MethodDesc method)
        {
            if (method.IsCanonicalMethod(CanonicalFormKind.Specific) && !method.HasInstantiation)
            {
                // Unboxing stubs to canonical instance methods need a special unboxing stub that unboxes
                // 'this' and also provides an instantiation argument (we do a calling convention conversion).
                // We don't do this for generic instance methods though because they don't use the MethodTable
                // for the generic context anyway.
                return new LLVMMethodCodeNode(TypeSystemContext.GetSpecialUnboxingThunk(method, TypeSystemContext.GeneratedAssembly));
            }
            else
            {
                // Otherwise we just unbox 'this' and don't touch anything else.
                return new UnboxingStubNode(method);
            }
        }

        protected override ISymbolNode CreateReadyToRunHelperNode(ReadyToRunHelperKey helperCall)
        {
            return new ReadyToRunHelperNode(helperCall.HelperId, helperCall.Target);
        }
    }
}
