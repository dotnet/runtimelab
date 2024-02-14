// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SwiftReflector.ExceptionTools;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SwiftReflector.Demangling;

namespace SwiftReflector.Inventory {
	public class PropertyInventory : Inventory<PropertyContents> {
		int sizeofMachinePointer;
		public PropertyInventory (int sizeofMachinePointer)
		{
			this.sizeofMachinePointer = sizeofMachinePointer;
		}

		public override void Add (TLDefinition tld, Stream srcStm)
		{
			lock (valuesLock) {
				TLFunction tlf = tld as TLFunction;
				if (tlf == null)
					throw ErrorHelper.CreateError (ReflectorError.kInventoryBase + 7, $"Expected a TLFunction for a property but got a {tld.GetType ().Name}.");

				SwiftPropertyType prop = tlf.Signature as SwiftPropertyType;
				if (prop == null)
					throw ErrorHelper.CreateError (ReflectorError.kInventoryBase + 8, $"Expected a function of property type but got a {tlf.Signature.GetType ().Name}.");

				PropertyContents contents = null;
				SwiftName nameToUse = prop.PrivateName ?? prop.Name;
				if (!values.TryGetValue (nameToUse, out contents)) {
					contents = new PropertyContents (tlf.Class, nameToUse, sizeofMachinePointer);
					values.Add (nameToUse, contents);
				}
				contents.Add (tlf, prop);
			}
		}

		public PropertyContents PropertyWithName (SwiftName name)
		{
			PropertyContents pc = Values.Where (oi => oi.Name.Equals (name)).FirstOrDefault ();
			return pc;
		}

		public PropertyContents PropertyWithName (string name)
		{
			SwiftName sn = new SwiftName (name, false);
			return PropertyWithName (sn);
		}

	}
}

