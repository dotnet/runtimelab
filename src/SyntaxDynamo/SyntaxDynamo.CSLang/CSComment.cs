// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace SyntaxDynamo.CSLang {
	public class CSComment : SimpleLineElement {
		public CSComment (string text)
			: base (Commentize (text), false, true, false)
		{
		}

		static string Commentize (string text)
		{
			text = text ?? "";
			if (text.Contains ("\n"))
				throw new ArgumentException ("Comment text must not contain new line characters.", "text");
			return "// " + text;
		}
	}

	public class CSCommentBlock : CodeElementCollection<CSComment> {
		public CSCommentBlock (params CSComment [] comments)
		{
			AddRange (comments);
		}

		public CSCommentBlock (params string [] text)
		{
			AddRange (Sanitize (Exceptions.ThrowOnNull (text, "text")));
		}

		static IEnumerable<CSComment> Sanitize (string [] text)
		{
			foreach (string s in text) {
				string [] lines = s.Split ('\n');
				foreach (string line in lines)
					yield return new CSComment (line);
			}
		}

		public CSCommentBlock And (string text)
		{
			AddRange (Sanitize (new string [] { text }));
			return this;
		}

		public CSCommentBlock And (CSComment comment)
		{
			Add (Exceptions.ThrowOnNull (comment, "comment"));
			return this;
		}
	}
}

