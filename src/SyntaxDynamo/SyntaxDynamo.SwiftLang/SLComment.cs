// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
namespace SyntaxDynamo.SwiftLang {
	public class SLComment : DelegatedSimpleElement {
		public SLComment (string contents, bool onOwnLine)
		{
			Contents = contents;
			OnOwnLine = onOwnLine;
		}

		public bool OnOwnLine { get; set; }
		public string Contents { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			if (OnOwnLine) {
				writer.BeginNewLine (true);
			} else {
				SimpleElement.Spacer.Write (writer, o);
			}
			writer.Write ("// ", false);
			writer.Write (Contents, false);
			writer.EndLine ();
		}

		public void AttachBefore (ICodeElement item)
		{
			item.Begin += (s, writeEventArgs) => {
				this.WriteAll (writeEventArgs.Writer);
			};
		}

		public void AttachAfter (ICodeElement item)
		{
			item.End += (s, writeEventArgs) => {
				this.WriteAll (writeEventArgs.Writer);
			};
		}
	}
}
