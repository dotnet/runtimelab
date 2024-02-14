// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SwiftReflector.ExceptionTools;
using System.IO;
using SwiftReflector.Demangling;

namespace SwiftReflector.Inventory {
	public class VariableInventory : Inventory<VariableContents> {
		int sizeofMachinePointer;
		public VariableInventory (int sizeofMachinePointer)
		{
			this.sizeofMachinePointer = sizeofMachinePointer;
		}
		public override void Add (TLDefinition tld, Stream srcStm)
		{
			lock (valuesLock) {
				TLVariable vari = tld as TLVariable;
				if (vari != null) {
					VariableContents contents = GoGetIt (vari.Name);
					if (contents.Variable != null)
						throw ErrorHelper.CreateError (ReflectorError.kInventoryBase + 4, $"duplicate variable {vari.Name.Name}.");
					contents.Variable = vari;
					return;
				}

				TLFunction tlf = tld as TLFunction;
				if (tlf != null) {
					VariableContents contents = GoGetIt (tlf.Name);
					contents.Addressors.Add (tlf);
					return;
				}

				throw ErrorHelper.CreateError (ReflectorError.kInventoryBase + 5, $"expected a top-level function or top-level variable but got a {tld.GetType ().Name}");
			}

		}

		VariableContents GoGetIt (SwiftName name)
		{
			VariableContents vari = null;
			if (!values.TryGetValue (name, out vari)) {
				vari = new VariableContents (name, sizeofMachinePointer);
				values.Add (name, vari);
			}
			return vari;
		}
	}
}

