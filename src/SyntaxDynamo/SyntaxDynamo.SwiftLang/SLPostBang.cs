// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo.SwiftLang {
	public class SLPostBang : SLBaseExpr, ISLLineable {
		public SLPostBang (SLBaseExpr expr, bool addParens)
		{
			Expr = Exceptions.ThrowOnNull (expr, nameof(expr));
			AddParens = addParens;
		}

		public SLBaseExpr Expr { get; private set; }
		public bool AddParens { get; set; }

		#region implemented abstract members of DelegatedSimpleElem

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			if (AddParens)
				writer.Write ('(', false);
			Expr.WriteAll (writer);
			if (AddParens)
				writer.Write (')', false);
			writer.Write ('!', false);
		}

		#endregion
	}
}

