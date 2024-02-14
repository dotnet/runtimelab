// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using SwiftReflector.Demangling;
using SwiftRuntimeLibrary;

namespace SwiftReflector.Inventory {
	public class ModuleContents {
		public ModuleContents (SwiftName name, int sizeofMachinePointer)
		{
			Name = Exceptions.ThrowOnNull (name, nameof(name));
			Classes = new ClassInventory (sizeofMachinePointer);
			Functions = new FunctionInventory (sizeofMachinePointer);
			Variables = new VariableInventory (sizeofMachinePointer);
			WitnessTables = new WitnessInventory (sizeofMachinePointer);
			Protocols = new ProtocolInventory (sizeofMachinePointer);
			Extensions = new FunctionInventory (sizeofMachinePointer);
			MetadataFieldDescriptor = new List<TLMetadataDescriptor> ();
			MetadataBuiltInDescriptor = new List<TLMetadataDescriptor> ();
			PropertyDescriptors = new VariableInventory (sizeofMachinePointer);
			ExtensionDescriptors = new List<TLPropertyDescriptor> ();
		}

		public void Add (TLDefinition tld, Stream srcStm)
		{
			TLFunction tlf = tld as TLFunction;
			if (tlf != null) {
				if (tlf.Signature.IsExtension) {
					Extensions.Add (tlf, srcStm);	
				} else if (tlf.IsTopLevelFunction) {
					if (tlf.Signature is SwiftAddressorType) {
						Variables.Add (tlf, srcStm);
					} else if (tlf.Signature is SwiftWitnessTableType) {
						WitnessTables.Add (tld, srcStm);
					} else {
						Functions.Add (tlf, srcStm);
					}
				} else {
					if (tlf.Class.EntityKind == MemberNesting.Protocol) {
						Protocols.Add (tlf, srcStm);
					} else {
						Classes.Add (tld, srcStm);
					}
				}
			} else {
				if (tld is TLVariable tlvar && ((TLVariable)tld).Class == null) {
					if (tlvar is TLPropertyDescriptor propDesc) {
						if (propDesc.ExtensionOn != null)
							ExtensionDescriptors.Add (propDesc);
						else
							PropertyDescriptors.Add (tlvar, srcStm);
					} else {
						Variables.Add (tld, srcStm);
					}
				} else if (tld is TLProtocolTypeDescriptor || tld is TLProtocolRequirementsBaseDescriptor) {
					Protocols.Add (tld, srcStm);
				} else if (tld is TLModuleDescriptor module) {
					ModuleDescriptor = module;
				} else if (tld is TLMetadataDescriptor metadata) {
					if (metadata.IsBuiltIn)
						MetadataBuiltInDescriptor.Add (metadata);
					else
						MetadataFieldDescriptor.Add (metadata);
				} else {
					// global unsafe mutable addressors get ignored. We don't need/use them.
					if (tld is TLUnsafeMutableAddressor addressor && addressor.Class == null)
						return;
					Classes.Add (tld, srcStm);
				}
			}
		}

		public TLModuleDescriptor ModuleDescriptor { get; private set; }
		public SwiftName Name { get; private set; }

		public WitnessInventory WitnessTables { get; private set;  }
		public ProtocolInventory Protocols { get; private set; }
		public ClassInventory Classes { get; private set; }
		public FunctionInventory Functions { get; private set; }
		public VariableInventory Variables { get; private set; }
		public VariableInventory PropertyDescriptors { get; private set; }
		public List<TLPropertyDescriptor> ExtensionDescriptors { get; private set; }
		public FunctionInventory Extensions { get; private set; }
		public List<TLMetadataDescriptor> MetadataFieldDescriptor { get; private set; }
		public List<TLMetadataDescriptor> MetadataBuiltInDescriptor { get; private set; }
	}

}

