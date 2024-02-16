// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Collections.Generic;
using SwiftReflector.ExceptionTools;
using SwiftReflector.Demangling;

namespace SwiftReflector.Inventory
{
    public class ProtocolContents
    {
        int sizeofMachinePointer;
        public ProtocolContents(SwiftClassName className, int sizeofMachiinePointer)
        {
            this.sizeofMachinePointer = sizeofMachiinePointer;
            WitnessTable = new WitnessInventory(sizeofMachinePointer);
            FunctionsOfUnknownDestination = new List<TLFunction>();
            DefinitionsOfUnknownDestination = new List<TLDefinition>();
            BaseDescriptors = new List<TLProtocolRequirementsBaseDescriptor>();
            BaseConformanceDescriptors = new List<TLBaseConformanceDescriptor>();
            Name = className;

        }

        public TLMetaclass Metaclass { get; private set; }
        public TLDirectMetadata DirectMetadata { get; private set; }
        public TLProtocolTypeDescriptor TypeDescriptor { get; private set; }
        public WitnessInventory WitnessTable { get; private set; }
        public List<TLFunction> FunctionsOfUnknownDestination { get; private set; }
        public List<TLDefinition> DefinitionsOfUnknownDestination { get; private set; }
        public List<TLProtocolRequirementsBaseDescriptor> BaseDescriptors { get; private set; }
        public List<TLBaseConformanceDescriptor> BaseConformanceDescriptors { get; private set; }
        public SwiftClassName Name { get; private set; }

        public void Add(TLDefinition tld, Stream srcStm)
        {
            TLFunction tlf = tld as TLFunction;
            if (tlf != null)
            {
                if (ClassContents.IsWitnessTable(tlf.Signature, tlf.Class))
                {
                    WitnessTable.Add(tlf, srcStm);
                }
                FunctionsOfUnknownDestination.Add(tlf);
                return;
            }

            TLDirectMetadata meta = tld as TLDirectMetadata;
            if (meta != null)
            {
                if (DirectMetadata != null)
                    throw ErrorHelper.CreateError(ReflectorError.kInventoryBase + 1, $"duplicate direct metadata in protocol {DirectMetadata.Class.ClassName.ToFullyQualifiedName()}");
                DirectMetadata = meta;
                return;
            }
            TLMetaclass mc = tld as TLMetaclass;
            if (mc != null)
            {
                if (Metaclass != null)
                {
                    throw ErrorHelper.CreateError(ReflectorError.kInventoryBase + 2, $"duplicate type meta data descriptor in protocol {Metaclass.Class.ClassName.ToFullyQualifiedName()}");
                }
                Metaclass = mc;
                return;
            }

            TLProtocolTypeDescriptor ptd = tld as TLProtocolTypeDescriptor;
            if (ptd != null)
            {
                if (TypeDescriptor != null)
                {
                    throw ErrorHelper.CreateError(ReflectorError.kInventoryBase + 3, $"duplicate protocol type descriptor in protocol {TypeDescriptor.Class.ClassName.ToFullyQualifiedName()}");
                }
                TypeDescriptor = ptd;
                return;
            }

            if (tld is TLProtocolRequirementsBaseDescriptor baseDescriptor)
            {
                BaseDescriptors.Add(baseDescriptor);
                return;
            }

            if (tld is TLBaseConformanceDescriptor baseConformanceDescriptor)
            {
                BaseConformanceDescriptors.Add(baseConformanceDescriptor);
                return;
            }

            DefinitionsOfUnknownDestination.Add(tld);
        }
    }
}

