// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.SwiftLang {
	public class SLAddressOf : SLBaseExpr {
		public SLAddressOf (SLBaseExpr expr, bool addParens)
		{
			Expr = Exceptions.ThrowOnNull (expr, nameof(expr));
			AddParens = addParens;
		}

		public SLBaseExpr Expr { get; private set; }
		public bool AddParens { get; set; }

		#region implemented abstract members of DelegatedSimpleElem

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write ('&', false);
			if (AddParens)
				writer.Write ('(', false);
			Expr.WriteAll (writer);
			if (AddParens)
				writer.Write (')', false);
		}
		#endregion
	}
}

