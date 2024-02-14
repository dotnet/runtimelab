// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace SyntaxDynamo.SwiftLang {
	public class SLCodeBlock : DecoratedCodeElementCollection<ICodeElement>, ISLStatement {
		public SLCodeBlock (IEnumerable<ICodeElement> statements)
			: this ("{", "}", statements)
		{
		}

		public SLCodeBlock (string start, string end, IEnumerable<ICodeElement> statements)
			: base (Exceptions.ThrowOnNull (start, nameof(start)),
			        Exceptions.ThrowOnNull (end, nameof(end)),
			        true, true, true)
		{
			if (statements != null) {
				foreach (ICodeElement elem in statements) {
					And (elem);
				}
			}
		}

		public SLCodeBlock And (ICodeElement elem)
		{
			if (!((elem is ISLStatement) || (elem is ISLLineable)))
				throw new ArgumentException ("contents must each be an ISLStatement or ISLLineable");
			Add (elem);
			return this;
		}
	}
}

