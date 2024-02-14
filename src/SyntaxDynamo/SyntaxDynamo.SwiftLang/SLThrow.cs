// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.SwiftLang
{
	public class SLThrow : SLBaseExpr, ISLStatement, ISLLineable
	{
		public SLThrow (SLBaseExpr expr)
		{
			Expr = Exceptions.ThrowOnNull (expr, nameof(expr));
		}

		public SLBaseExpr Expr { get; private set; }

		#region implemented abstract members of DelegatedSimpleElem

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write ("throw ", true);
			Expr.WriteAll (writer);
		}

		#endregion
	}
}
