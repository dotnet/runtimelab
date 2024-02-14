// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.SwiftLang {
	public class SLNamedClosureCall : SLBaseExpr, ISLLineable {

		public SLNamedClosureCall (SLBaseExpr closureExpr, CommaListElementCollection<SLBaseExpr> paramList)
		{
			Closure = Exceptions.ThrowOnNull (closureExpr, "closure");
			Parameters = paramList ?? new CommaListElementCollection<SLBaseExpr> ();
		}

		public SLBaseExpr Closure { get; private set; }
		public CommaListElementCollection<SLBaseExpr> Parameters { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			Closure.WriteAll (writer);
			writer.Write ("(", false);
			Parameters.WriteAll (writer);
			writer.Write (")", false);
		}

	}
}

