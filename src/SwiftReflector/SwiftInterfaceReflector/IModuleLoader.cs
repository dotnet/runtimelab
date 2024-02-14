// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using SwiftReflector.TypeMapping;

namespace SwiftReflector.SwiftInterfaceReflector {
	public interface IModuleLoader {
		bool Load (string moduleName, TypeDatabase into);
	}

	// this is only useful for tests, really.
	public class NoLoadLoader : IModuleLoader {
		public NoLoadLoader ()
		{
		}

		public bool Load (string moduleName, TypeDatabase into)
		{
			if (moduleName == "_Concurrency")
				return true;
			// only returns true is the module is already loaded
			return into.ModuleNames.Contains (moduleName);			
		}
	}
}
