// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.CSLang {
	public class CSReturn : DelegatedSimpleElement, ICSExpression, ICSLineable {
		public CSReturn (ICSExpression expr)
		{
			Value = expr;
		}

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write ("return ", true);
			if (Value != null)
				Value.WriteAll (writer);
		}

		public ICSExpression Value { get; private set; }

		public static CSLine ReturnLine (ICSExpression expr)
		{
			return new CSLine (new CSReturn (expr));
		}
	}
}

