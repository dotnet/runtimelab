// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SwiftReflector.ExceptionTools;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SwiftReflector.Demangling;

namespace SwiftReflector.Inventory {
	public class WitnessInventory {
		object valuesLock = new object ();
		Dictionary<string, TLFunction> values = new Dictionary<string, TLFunction> ();
		int sizeofMachinePointer;

		public WitnessInventory (int sizeofMachinePointer)
		{
			this.sizeofMachinePointer = sizeofMachinePointer;
		}

		public void Add (TLDefinition tld, Stream srcStm)
		{
			lock (valuesLock) {
				TLFunction tlf = tld as TLFunction;
				if (tlf == null)
					throw ErrorHelper.CreateError (ReflectorError.kInventoryBase + 9, $"Expected a TLFunction for a witness table but got a {tld.GetType ().Name}.");

				if (values.ContainsKey (tlf.MangledName))
					throw ErrorHelper.CreateError (ReflectorError.kInventoryBase + 10, $"Already received witness table entry for {tlf.MangledName}.");
				values.Add (tlf.MangledName, tlf);
				LoadWitnessTable (tlf, srcStm);
			}
		}

		public IEnumerable<string> MangledNames { get { return values.Keys; } }
		public IEnumerable<TLFunction> Functions { get { return values.Values; } }
		public IEnumerable<TLFunction> WitnessEntriesOfType (WitnessType wit)
		{
			return Functions.Where (fn => (fn.Signature is SwiftWitnessTableType) && ((SwiftWitnessTableType)fn.Signature).WitnessType == wit);
		}

		public ValueWitnessTable ValueWitnessTable { get; private set; }

		void LoadWitnessTable (TLFunction tlf, Stream stm)
		{
			WitnessType type = FromTLF (tlf);
			switch (type) {
			case WitnessType.Value:
				ValueWitnessTable = ValueWitnessTable.FromStream (stm, tlf, sizeofMachinePointer);
				break;
			default:
				break;
			}
		}

		WitnessType FromTLF (TLFunction tlf)
		{
			SwiftWitnessTableType wit = tlf.Signature as SwiftWitnessTableType;
			if (wit == null)
				throw new ArgumentException ("tlf");
			return wit.WitnessType;
		}
	}

}

