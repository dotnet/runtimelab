// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
namespace SyntaxDynamo.SwiftLang {
	public class SLUnaryExpr : SLBaseExpr {
		public SLUnaryExpr (string op, ISLExpr expr, bool isPrefix)
		{
			Operation = Exceptions.ThrowOnNull (op, nameof (op));
			Expr = Exceptions.ThrowOnNull (expr, nameof (expr));
			IsPrefix = isPrefix;
		}

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			if (IsPrefix) {
				writer.Write (' ', false);
				writer.Write (Operation, false);
				Expr.WriteAll (writer);
			} else {
				Expr.WriteAll (writer);
				writer.Write (Operation, false);
				writer.Write (' ', false);
			}
		}

		public bool IsPrefix { get; private set; }
		public string Operation { get; private set; }
		public ISLExpr Expr { get; private set; }

		public static SLUnaryExpr Await (ISLExpr expr)
		{
			return new SLUnaryExpr ("await ", expr, true);
		}
	}
}
