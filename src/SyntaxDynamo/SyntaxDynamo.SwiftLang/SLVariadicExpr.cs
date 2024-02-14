// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.SwiftLang {
	public class SLVariadicExpr : SLBaseExpr {
		public SLVariadicExpr (DelegatedCommaListElemCollection<SLBaseExpr> paramList)
		{
			Parameters = paramList ?? new DelegatedCommaListElemCollection<SLBaseExpr> (WriteElement);
		}

		public SLVariadicExpr (params SLBaseExpr [] paramList)
			: this (new DelegatedCommaListElemCollection<SLBaseExpr> (WriteElement, paramList))
		{
		}

		public static void WriteElement (ICodeWriter writer, int i, SLBaseExpr arg)
		{
			arg.WriteAll (writer);
		}

		public DelegatedCommaListElemCollection<SLBaseExpr> Parameters { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			Parameters.WriteAll (writer);
		}
	}
}
