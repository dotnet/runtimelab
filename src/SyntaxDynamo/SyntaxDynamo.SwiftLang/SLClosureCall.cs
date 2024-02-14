// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.SwiftLang {
	public class SLClosureCall : SLBaseExpr, ISLLineable {

		public SLClosureCall (SLClosure closure, DelegatedCommaListElemCollection<SLArgument> paramList)
		{
			Closure = Exceptions.ThrowOnNull (closure, "closure");
			Parameters = paramList ?? new DelegatedCommaListElemCollection<SLArgument> (SLFunctionCall.WriteElement);
		}

		public SLClosure Closure { get; private set; }
		public DelegatedCommaListElemCollection<SLArgument> Parameters { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			Closure.WriteAll (writer);
			writer.Write ("(", false);
			Parameters.WriteAll (writer);
			writer.Write (")", false);
		}

	}
}

