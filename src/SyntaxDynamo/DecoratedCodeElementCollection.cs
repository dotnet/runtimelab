// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace SyntaxDynamo {
	public class DecoratedCodeElementCollection<T> : CodeElementCollection<T> where T : ICodeElement {
		bool startOnOwnLine, endOnOwnLine, indent;

		public DecoratedCodeElementCollection (string startDecoration, string endDecoration,
		                                    bool startOnOwnLine, bool endOnOwnLine, bool indent,
		                                    IEnumerable<T> elems)
			: base ()
		{
			StartDecoration = startDecoration;
			EndDecoration = endDecoration;
			this.startOnOwnLine = startOnOwnLine;
			this.endOnOwnLine = endOnOwnLine;
			this.indent = indent;
			if (elems != null)
				AddRange (elems);
		}

		public DecoratedCodeElementCollection (string startDecoration, string endDecoration,
		                                    bool startOnOwnLine, bool endOnOwnLine, bool indent)
			: this (startDecoration, endDecoration, startOnOwnLine, endOnOwnLine, indent, null)
		{
		}

		public string StartDecoration { get; private set; }
		public string EndDecoration { get; private set; }

		public override void Write (ICodeWriter writer, object o)
		{
			if (StartDecoration != null) {
				if (startOnOwnLine)
					writer.BeginNewLine (true);
				writer.Write (StartDecoration, true);
				if (startOnOwnLine)
					writer.EndLine ();
			}
			if (indent)
				writer.Indent ();
		}

		public override void EndWrite (ICodeWriter writer, object o)
		{
			if (indent)
				writer.Exdent ();
			if (EndDecoration != null) {
				if (endOnOwnLine)
					writer.BeginNewLine (true);
				writer.Write (EndDecoration, true);
				if (endOnOwnLine)
					writer.EndLine ();
			}

			base.EndWrite (writer, o);
		}

		public override string ToString ()
		{
			return string.Format ("{0}{1}{2}",
				StartDecoration ?? "",
				StartDecoration != null && EndDecoration != null ? "..." : "",
				EndDecoration ?? "");
		}


		public static DecoratedCodeElementCollection<T> CBlock (IEnumerable<T> elems)
		{
			var col = new DecoratedCodeElementCollection<T> ("{", "}", true, true, true);
			if (elems != null)
				col.AddRange (elems);
			return col;
		}
	}
}

