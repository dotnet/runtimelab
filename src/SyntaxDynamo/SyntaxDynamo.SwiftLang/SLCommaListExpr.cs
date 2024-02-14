// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SyntaxDynamo.SwiftLang {
	public class SLCommaListExpr : SLBaseExpr {
		public SLCommaListExpr (params SLBaseExpr [] exprs)
		{
			Exprs = new List<SLBaseExpr> ();
			if (exprs != null)
				Exprs.AddRange (exprs);
		}

		public List<SLBaseExpr> Exprs { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			bool isFirst = true;
			foreach (SLBaseExpr expr in Exprs) {
				if (!isFirst)
					writer.Write (", ", true);
				isFirst = false;
				expr.WriteAll (writer);
			}
		}
	}
}
