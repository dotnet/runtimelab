// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System;

namespace SyntaxDynamo.SwiftLang {
	public class SLArgument : DelegatedSimpleElement {
		public SLArgument (SLIdentifier ident, SLBaseExpr expr, bool identifierIsRequired = false)
		{
			Identifier = identifierIsRequired ? Exceptions.ThrowOnNull (ident, nameof(ident)) : ident;
			Expr = Exceptions.ThrowOnNull (expr, nameof(expr));
			IdentifierIsRequired = identifierIsRequired;
		}

		public SLArgument (string ident, SLBaseExpr expr, bool identifierIsRequired = false)
			: this (!String.IsNullOrEmpty (ident) ? new SLIdentifier (ident) : null, expr, identifierIsRequired)
		{
		}

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			if (Identifier != null && IdentifierIsRequired) {
				Identifier.WriteAll (writer);
				writer.Write (": ", true);
			}
			Expr.WriteAll (writer);
		}



		public SLIdentifier Identifier { get; private set; }
		public SLBaseExpr Expr { get; private set; }
		public bool IdentifierIsRequired { get; private set; }
	}
}

