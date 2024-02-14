// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace SyntaxDynamo.SwiftLang {
	public class SLTupleExpr : SLBaseExpr {
		public SLTupleExpr (IEnumerable<SLBaseExpr> values)
		{
			Values = new List<SLBaseExpr> ();
			Values.AddRange (Exceptions.ThrowOnNull (values, nameof(values)));
		}

		public List<SLBaseExpr> Values { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write ('(', true);
			for (int i = 0; i < Values.Count; i++) {
				if (i > 0) {
					writer.Write (", ", true);
				}
				Values [i].WriteAll (writer);
			}
			writer.Write (')', true);
		}
	}
}

