// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

namespace SyntaxDynamo.SwiftLang {
	public class SLInheritance : CommaListElementCollection<SLIdentifier> {
		public SLInheritance (IEnumerable<SLIdentifier> identifiers)
		{
			if (identifiers != null)
				AddRange (identifiers);
		}

		public SLInheritance (params string [] identifiers)
			: this (identifiers.Select (str => new SLIdentifier (str)))
		{
		}
	}
}

