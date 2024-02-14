// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
namespace SyntaxDynamo.SwiftLang {
	public class SLParenthesisExpression : SLBaseExpr {
		public SLParenthesisExpression (SLBaseExpr within)
		{
			Within = Exceptions.ThrowOnNull (within, "within");
		}

		public SLBaseExpr Within { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write ('(', true);
			Within.WriteAll (writer);
			writer.Write (')', true);
		}
	}
}
