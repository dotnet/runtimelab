// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.SwiftLang {
	public class SLReturn : DelegatedSimpleElement, ISLExpr, ISLLineable {
		public SLReturn (ISLExpr expr)
		{
			Value = expr;
		}

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write ("return ", true);
			if (Value != null)
				Value.WriteAll (writer);
		}

		public ISLExpr Value { get; private set; }

		public static SLLine ReturnLine (ISLExpr expr)
		{
			return new SLLine (new SLReturn (expr));
		}
	}
}

