// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace SyntaxDynamo.CSLang {
	public class CSFixedCodeBlock : CSCodeBlock, ICSStatement {
		public CSFixedCodeBlock (CSType type, CSIdentifier ident, CSBaseExpression expr, IEnumerable<ICodeElement> body)
			: base (body)
		{
			Type = Exceptions.ThrowOnNull (type, "type");
			Identifier = Exceptions.ThrowOnNull (ident, "ident");
			Expr = Exceptions.ThrowOnNull (expr, "expr");
		}

		public override void Write (ICodeWriter writer, object o)
		{
			writer.BeginNewLine (true);
			writer.Write ("fixed (", true);
			Type.Write (writer, o);
			writer.Write (' ', false);
			Identifier.Write (writer, o);
			writer.Write (" = ", true);
			Expr.Write (writer, o);
			writer.Write (") ", true);
			base.Write (writer, o);
		}

		public CSType Type { get; private set; }
		public CSIdentifier Identifier { get; private set; }
		public CSBaseExpression Expr { get; private set; }
	}
}

