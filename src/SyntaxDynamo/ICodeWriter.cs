// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo {
	public interface ICodeWriter {
		void BeginNewLine (bool prependIndents);
		void EndLine ();
		void Write (char c, bool allowSplit);
		void Write (string code, bool allowSplit);
		void Indent ();
		void Exdent ();
		int IndentLevel { get; }
		bool IsAtLineStart { get; }
	}
}

