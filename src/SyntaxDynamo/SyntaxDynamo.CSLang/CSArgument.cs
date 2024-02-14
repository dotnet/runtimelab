// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.CSLang {
	public class CSArgument : DelegatedSimpleElement {
		public CSArgument (ICSExpression expr)
		{
			Value = Exceptions.ThrowOnNull (expr, nameof(expr));
		}

		public ICSExpression Value { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			Value.WriteAll (writer);
		}
	}

	public class CSArgumentList : CommaListElementCollection<CSArgument> {

		public void Add (ICSExpression expr)
		{
			Add (new CSArgument (expr));
		}

		public static CSArgumentList FromExpressions (params ICSExpression [] exprs)
		{
			var al = new CSArgumentList ();
			foreach (var ex in exprs)
				al.Add (new CSArgument (ex));
			return al;
		}
	}
}

