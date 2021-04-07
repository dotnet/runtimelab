// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

using Internal.TypeSystem;
using Internal.TypeSystem.Ecma;

using AssemblyName = System.Reflection.AssemblyName;

namespace ILCompiler
{
    /// <summary>
    /// Compilation root provider that provides roots based on the ILLink descriptor file format.
    /// Only supports a subset of the ILLink Descriptor file format.
    /// </summary>
    /// <remarks>https://github.com/mono/linker/blob/main/docs/data-formats.md</remarks>
    internal class ILLinkDescriptorRootProvider : ICompilationRootProvider
    {
        private XElement _documentRoot;
        private TypeSystemContext _context;

        public ILLinkDescriptorRootProvider(TypeSystemContext context, string rdXmlFileName)
        {
            _context = context;
            _documentRoot = XElement.Load(rdXmlFileName);
        }

        public void AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            var linker = _documentRoot.Elements().Single();

            if (linker.Name.LocalName != "linker")
                throw new NotSupportedException($"{linker.Name.LocalName} is not a supported top level ILLink Directive. ILLink descriptor file should starts with \"linker\" tag.");

            foreach (var element in linker.Elements())
            {
                switch (element.Name.LocalName)
                {
                    case "assembly":
                        ProcessAssemblyDirective(rootProvider, element);
                        break;

                    default:
                        throw new NotSupportedException($"\"{element.Name.LocalName}\" is not a supported ILLink Directive.");
                }
            }
        }

        private void ProcessAssemblyDirective(IRootingServiceProvider rootProvider, XElement assemblyElement)
        {
            var fullNameAttribute = assemblyElement.Attribute("fullname");
            if (fullNameAttribute == null)
                throw new Exception("The \"fullname\" attribute is required on the \"assembly\" ILLink Directive.");

            ModuleDesc assembly = _context.ResolveAssembly(new AssemblyName(fullNameAttribute.Value));

            rootProvider.RootModuleMetadata(assembly, "ILLink.Descriptors.xml root");

            var preserveAttribute = assemblyElement.Attribute("preserve");
            bool includeAllTypes = false;
            if (preserveAttribute != null)
            {
                if (preserveAttribute.Value != "all")
                    throw new NotSupportedException($"\"{preserveAttribute.Value}\" is not a supported value for the \"preserve\" attribute of the \"assembly\" ILLink Directive. Supported values are \"all\".");

                includeAllTypes = true;
            }
            else
            {
                includeAllTypes = !assemblyElement.HasElements;
            }

            if (includeAllTypes)
            {
                foreach (TypeDesc type in ((EcmaModule)assembly).GetAllTypes())
                {
                    RootingHelpers.TryRootType(rootProvider, type, "ILLink.Descriptors.xml root");
                }
            }
            else
            {
                foreach (var element in assemblyElement.Elements())
                {
                    switch (element.Name.LocalName)
                    {
                        case "type":
                            ProcessTypeDirective(rootProvider, assembly, element);
                            break;
                        case "resource":
                            break;
                        default:
                            throw new NotSupportedException($"\"{element.Name.LocalName}\" is not a supported ILLink Directive.");
                    }
                }
            }
        }

        private void ProcessTypeDirective(IRootingServiceProvider rootProvider, ModuleDesc containingModule, XElement typeElement)
        {
            var fullNameAttribute = typeElement.Attribute("fullname");
            if (fullNameAttribute == null)
                throw new Exception("The \"fullname\" attribute is required on the \"type\" ILLink Directive.");

            string typeName = fullNameAttribute.Value;
            TypeDesc type = containingModule.GetTypeByCustomAttributeTypeName(typeName);

            var preserveAttribute = typeElement.Attribute("preserve");
            if (preserveAttribute != null)
            {
                if (preserveAttribute.Value != "all" && preserveAttribute.Value != "nothing")
                    throw new NotSupportedException($"\"{preserveAttribute.Value}\" is not a supported value for the \"preserve\" attribute of the \"type\" ILLink Directive. Supported values are \"all\",\"nothing\".");

                RootingHelpers.RootType(rootProvider, type, "ILLink.Descriptors.xml root");
                if (type is DefType defType)
                {
                    rootProvider.RootStructMarshallingData(defType, "ILLink.Descriptors.xml root");
                }
            }

            if (preserveAttribute == null || preserveAttribute.Value != "nothing")
            {
                foreach (var element in typeElement.Elements())
                {
                    switch (element.Name.LocalName)
                    {
                        // TODO: Method directive do not supported, becasue it is require translation of the names
                        case "method":
                        default:
                            throw new NotSupportedException($"\"{element.Name.LocalName}\" is not a supported ILLink Directive.");
                    }
                }
            }
        }
    }
}
