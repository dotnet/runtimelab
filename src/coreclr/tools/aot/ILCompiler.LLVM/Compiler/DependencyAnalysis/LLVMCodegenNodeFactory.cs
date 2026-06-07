// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using ILCompiler.DependencyAnalysis.Wasm;

using Internal.JitInterface;
using Internal.Text;
using Internal.TypeSystem;

namespace ILCompiler.DependencyAnalysis
{
    internal sealed class LLVMCodegenNodeFactory : NodeFactory
    {
        private readonly CorInfoLlvmEHModel _ehModel;

        private readonly Dictionary<string, ExternMethodCellNode> _externMethodCells = new();
        private readonly NodeCache<ExternMethodCellNode, ExternWasmMethodNode> _externWasmMethods =
            new(methodCell => new ExternWasmMethodNode(methodCell));

        public LLVMCodegenNodeFactory(
            LLVMCodegenConfigProvider options,
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
            ObjectDataInterner dataInterner,
            TypeMapManager typeMapManager)
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
                  dataInterner,
                  typeMapManager)
        {
            _ehModel = options.ExceptionHandlingModel;
        }

        public override bool TargetsEmulatedEH() => _ehModel is CorInfoLlvmEHModel.CORINFO_LLVM_EH_EMULATED;

        internal ExternMethodCellNode ExternMethodCell(Utf8String name, MethodDesc method)
        {
            Dictionary<string, ExternMethodCellNode> map = _externMethodCells;

            // Not lockless since we mutate the node. Contention on this path is not expected.
            //
            lock (map)
            {
                ref ExternMethodCellNode node = ref CollectionsMarshal.GetValueRefOrAddDefault(map, name.ToString(), out bool exists);
                if (!exists)
                {
                    node = new ExternMethodCellNode(name);
                }
                node.AddMethod(method);
                return node;
            }
        }

        internal ExternWasmMethodNode ExternWasmMethod(ExternMethodCellNode methodCell) => _externWasmMethods.GetOrAdd(methodCell);

        protected override IMethodNode CreateMethodEntrypointNode(MethodDesc method)
        {
            if (method.IsInternalCall)
            {
                if (method.IsArrayAddressMethod())
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
