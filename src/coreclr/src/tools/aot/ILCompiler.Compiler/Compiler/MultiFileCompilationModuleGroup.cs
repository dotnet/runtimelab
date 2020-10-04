// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;

using ILCompiler.DependencyAnalysis;
using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

namespace ILCompiler
{
    public abstract class MultiFileCompilationModuleGroup : CompilationModuleGroup
    {
        private HashSet<ModuleDesc> _compilationModuleSet;

        public MultiFileCompilationModuleGroup(TypeSystemContext context, IEnumerable<ModuleDesc> compilationModuleSet)
        {
            _compilationModuleSet = new HashSet<ModuleDesc>(compilationModuleSet);

            // The fake assembly that holds compiler generated types is part of the compilation.
            _compilationModuleSet.Add(context.GeneratedAssembly);
        }

        public sealed override bool ContainsType(TypeDesc type)
        {
            EcmaType ecmaType = type as EcmaType;

            if (ecmaType == null)
                return true;

            if (!IsModuleInCompilationGroup(ecmaType.EcmaModule))
            {
                return false;
            }

            return true;
        }

        public sealed override bool ContainsTypeDictionary(TypeDesc type)
        {
            return ContainsType(type);
        }

        public sealed override bool ContainsMethodBody(MethodDesc method, bool unboxingStub)
        {
            if (method.HasInstantiation)
                return true;

            return ContainsType(method.OwningType);
        }

        public sealed override bool ContainsMethodDictionary(MethodDesc method)
        {
            Debug.Assert(method.GetCanonMethodTarget(CanonicalFormKind.Specific) != method);
            return ContainsMethodBody(method, false);
        }

        public sealed override bool ImportsMethod(MethodDesc method, bool unboxingStub)
        {
            return false;
        }
        
        public sealed override ExportForm GetExportTypeForm(TypeDesc type)
        {
            return ExportForm.None;
        }

        public sealed override ExportForm GetExportTypeFormDictionary(TypeDesc type)
        {
            return ExportForm.None;
        }

        public sealed override ExportForm GetExportMethodForm(MethodDesc method, bool unboxingStub)
        {
            return ExportForm.None;
        }

        public override ExportForm GetExportMethodDictionaryForm(MethodDesc method)
        {
            return ExportForm.None;
        }

        protected bool IsModuleInCompilationGroup(EcmaModule module)
        {
            return _compilationModuleSet.Contains(module);
        }

        public sealed override bool IsSingleFileCompilation
        {
            get
            {
                return false;
            }
        }

        public sealed override bool ShouldReferenceThroughImportTable(TypeDesc type)
        {
            return false;
        }

        public override bool CanHaveReferenceThroughImportTable
        {
            get
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Represents a non-leaf multifile compilation group where types contained in the group are always fully expanded.
    /// </summary>
    public class MultiFileSharedCompilationModuleGroup : MultiFileCompilationModuleGroup
    {
        public MultiFileSharedCompilationModuleGroup(TypeSystemContext context, IEnumerable<ModuleDesc> compilationModuleSet)
            : base(context, compilationModuleSet)
        {
        }

        public override bool ShouldProduceFullVTable(TypeDesc type)
        {
            return ConstructedEETypeNode.CreationAllowed(type);
        }

        public override bool ShouldPromoteToFullType(TypeDesc type)
        {
            return ShouldProduceFullVTable(type);
        }

        public override bool PresenceOfEETypeImpliesAllMethodsOnType(TypeDesc type)
        {
            return (type.HasInstantiation || type.IsArray) && ShouldProduceFullVTable(type) && 
                   type.ConvertToCanonForm(CanonicalFormKind.Specific).IsCanonicalSubtype(CanonicalFormKind.Any);
        }

        public override bool GeneratesMethodBodyIntoOutput(MethodDesc method)
        {
            // As a size on disk optimization, don't generate method bodies for fully canonical methods outside
            // their home module. We define the home module as the module that contains the definition.
            // The compilation root provider is required to ensure that the canonical bodies are generated
            // in their home module. This prevents situations where common types like List<__Canon>..ctor have
            // their bodies generated into dozens of object files.
            //
            // We have to use this kludge instead of reporting this from ContainsMethodBody because this
            // method body still needs to be compiled/analyzed to figure out the generic dictionary dependencies
            // because if there's a canonical body, there is a dictionary. We just don't need to write it out.

            Debug.Assert(ContainsMethodBody(method, unboxingStub: false));

            if (!method.IsCanonicalMethod(CanonicalFormKind.Any) || method.HasInstantiation)
                return true;

            TypeDesc owningType = method.OwningType;
            foreach (var p in owningType.Instantiation)
            {
                if (!method.Context.IsCanonicalDefinitionType(p, CanonicalFormKind.Any))
                    return true;
            }

            EcmaType ecmaOwningType = owningType.GetTypeDefinition() as EcmaType;
            if (ecmaOwningType == null)
                return true;

            return IsModuleInCompilationGroup(ecmaOwningType.EcmaModule);
        }
    }
}
