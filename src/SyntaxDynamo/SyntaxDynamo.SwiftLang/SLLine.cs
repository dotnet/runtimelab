// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo.SwiftLang {
	public class SLLine : DelegatedSimpleElement, ISLStatement {
		public SLLine (ISLExpr contents, bool addSemicolon = true)
		{
			Contents = Exceptions.ThrowOnNull (contents, nameof(contents));
			if (!(contents is ISLLineable) && addSemicolon)
				throw new ArgumentException ("contents must be ISLineable and require a semicolon", nameof (contents));
			AddSemicolon = addSemicolon;
		}

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.BeginNewLine (true);
			Contents.WriteAll (writer);
			if (AddSemicolon)
				writer.Write (';', false);
			writer.EndLine ();
		}

		public ISLExpr Contents { get; private set; }
		public bool AddSemicolon { get; private set; }
	}
}

