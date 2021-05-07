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

        public ILLinkDescriptorRootProvider(TypeSystemContext context, string rdXmlFileName)
        {
            _context = context;
            _rdXmlFileName = rdXmlFileName;
        }

        public void AddCompilationRoots(IRootingServiceProvider rootProvider)
        {
            var xmlReader = new XmlTextReader(_rdXmlFileName);
            var processor = new ILLinkDescriptorProcessor(_context, xmlReader, rootProvider);
            processor.ProcessXml();
        }

        private class ILLinkDescriptorProcessor : ProcessLinkerXmlBase
        {
            private IRootingServiceProvider _rootProvider;

            internal ILLinkDescriptorProcessor(TypeSystemContext context, XmlReader reader, IRootingServiceProvider rootProvider)
                : base(context, reader, null, ImmutableDictionary.CreateBuilder<string, bool>().ToImmutable())
            {
                _rootProvider = rootProvider;
            }

            protected override void ProcessModuleMetadata(ModuleDesc assembly)
            {
                _rootProvider.RootModuleMetadata(assembly, "ILLink.Descriptors.xml root");
            }

            protected override void ProcessType(TypeDesc type)
            {
                RootingHelpers.TryRootType(_rootProvider, type, "ILLink.Descriptors.xml root");
                if (type is DefType defType)
                {
                    _rootProvider.RootStructMarshallingData(defType, "ILLink.Descriptors.xml root");
                }
            }
        }
    }
}
