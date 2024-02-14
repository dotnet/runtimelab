// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using SwiftReflector.SwiftXmlReflection;

namespace SwiftReflector.TypeMapping {
	public class ModuleDatabase : Dictionary<string, Entity>{
		public ModuleDatabase ()
		{
			Operators = new List<OperatorDeclaration> ();
			TypeAliases = new List<TypeAliasDeclaration> ();
		}

		public List<OperatorDeclaration> Operators { get; private set; }
		public List<TypeAliasDeclaration> TypeAliases { get; private set; }
	}
}
