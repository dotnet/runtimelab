// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo.SwiftLang {

	public class SLTry : SLBaseExpr, ISLLineable {
		public SLTry (SLBaseExpr expr)
			: this (expr, false)
		{
		}

		public SLTry (SLBaseExpr expr, bool isTryBang)
		{
			Expr = Exceptions.ThrowOnNull (expr, nameof (expr));
			IsTryBang = isTryBang;
		}

		public SLBaseExpr Expr { get; private set; }
		public bool IsTryBang { get; private set; }
		protected override void LLWrite (ICodeWriter writer, object o)
		{
			string bang = IsTryBang ? "!" : "";
			writer.Write ($"try{bang} ", true);
			Expr.WriteAll (writer);
		}
	}
}

