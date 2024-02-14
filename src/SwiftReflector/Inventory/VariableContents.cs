// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using SwiftReflector.Demangling;

namespace SwiftReflector.Inventory {
	public class VariableContents {
		int sizeofMachinePointer;
		public VariableContents (SwiftName name, int sizeofMachinePointer)
		{
			Name = name;
			Addressors = new List<TLFunction> ();
			this.sizeofMachinePointer = sizeofMachinePointer;
		}

		public TLVariable Variable { get; set; }
		public List<TLFunction> Addressors { get; private set; }
		public SwiftName Name { get; private set; }
		public int SizeofMachinePointer { get { return sizeofMachinePointer; } }
	}
}

