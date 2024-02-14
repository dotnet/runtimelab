// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace SyntaxDynamo.SwiftLang {
	public class SLSubscriptExpr : SLBaseExpr {
		public SLSubscriptExpr (SLIdentifier ident, CommaListElementCollection<SLBaseExpr> paramList)
		{
			Name = Exceptions.ThrowOnNull (ident, nameof(ident));
			Parameters = Exceptions.ThrowOnNull (paramList, nameof(paramList));
		}

		public SLSubscriptExpr (string identifier, params SLBaseExpr [] parameters)
			: this (new SLIdentifier (identifier), new CommaListElementCollection<SLBaseExpr> (parameters))
		{
		}

		public SLSubscriptExpr (SLIdentifier identifier, IEnumerable<SLBaseExpr> parameters)
			: this (identifier, new CommaListElementCollection<SLBaseExpr> (parameters))
		{
		}

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			Name.WriteAll (writer);
			writer.Write ("[", false);
			Parameters.WriteAll (writer);
			writer.Write ("]", false);
		}

		public SLIdentifier Name { get; private set; }
		public CommaListElementCollection<SLBaseExpr> Parameters { get; private set; }
	}
}

