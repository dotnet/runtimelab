// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml;

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
    public class ILLinkDescriptorRootProvider : ICompilationRootProvider
    {
        private TypeSystemContext _context;
        private string _rdXmlFileName;
        private IReadOnlyDictionary<string, bool> _featureSwitchValues;

        public ILLinkDescriptorRootProvider(TypeSystemContext context, string rdXmlFileName, IReadOnlyDictionary<string, bool> featureSwitchValues)
        {
            _context = context;
            _rdXmlFileName = rdXmlFileName;
            _featureSwitchValues = featureSwitchValues;
        }

        public void AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            var xmlReader = new XmlTextReader(_rdXmlFileName);
            var processor = new ILLinkDescriptorProcessor(_context, xmlReader, rootProvider, _featureSwitchValues);
            processor.ProcessXml();
        }

        private class ILLinkDescriptorProcessor : ProcessLinkerXmlBase
        {
            private IRootingServiceProvider _rootProvider;

            internal ILLinkDescriptorProcessor(TypeSystemContext context, XmlReader reader, IRootingServiceProvider rootProvider, IReadOnlyDictionary<string, bool> featureSwitchValues)
                : base(context, reader, null, featureSwitchValues)
            {
                _rootProvider = rootProvider;
            }

            protected override bool ProcessAssembly(ModuleDesc assembly)
            {
                _rootProvider.RootModuleMetadata(assembly, "ILLink.Descriptors.xml root");
                bool includeAllTypes = false;
                var preserveAttribute = _reader.GetAttribute("preserve");
                var hasContent = base.ProcessAssembly(assembly);

                if (preserveAttribute != null)
                {
                    if (preserveAttribute != "all")
                        throw new NotSupportedException($"\"{preserveAttribute}\" is not a supported value for the \"preserve\" attribute of the \"assembly\" ILLink Directive. Supported values are \"all\".");

                    includeAllTypes = true;
                }
                else
                {
                    includeAllTypes = !hasContent;
                }

                if (includeAllTypes)
                {
                    foreach (TypeDesc type in ((EcmaModule)assembly).GetAllTypes())
                    {
                        ProcessType(assembly);
                    }
                }

                return hasContent;
            }

            protected override void ProcessType(TypeDesc type, string preserveAttribute)
            {
                if (preserveAttribute != null)
                {
                    if (preserveAttribute != "all" && preserveAttribute != "nothing")
                        throw new NotSupportedException($"\"{preserveAttribute}\" is not a supported value for the \"preserve\" attribute of the \"type\" ILLink Directive. Supported values are \"all\",\"nothing\".");

                    RootingHelpers.TryRootType(_rootProvider, type, "ILLink.Descriptors.xml root");
                    if (type is DefType defType)
                    {
                        _rootProvider.RootStructMarshallingData(defType, "ILLink.Descriptors.xml root");
                    }
                }

                if (preserveAttribute == null || preserveAttribute != "nothing")
                {
                    base.ProcessType(type, preserveAttribute);
                }
            }
        }
    }
}
