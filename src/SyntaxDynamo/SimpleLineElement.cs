// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo {
	public class SimpleLineElement : ICodeElement {
		bool indent, prependIndents, allowSplit;

		public SimpleLineElement (string contents, bool indent, bool prependIdents, bool allowSplit)
		{
			Contents = contents ?? "";
			this.indent = indent;
			this.prependIndents = prependIdents;
			this.allowSplit = allowSplit;
		}

		public string Contents { get; private set; }

		#region ICodeElem implementation

		public event EventHandler<WriteEventArgs> Begin = (s, e) => { };

		public event EventHandler<WriteEventArgs> End = (s, e) => { };

		public object BeginWrite (ICodeWriter writer)
		{
			OnBegin (new WriteEventArgs (writer));
			return null;
		}

		protected virtual void OnBegin (WriteEventArgs args)
		{
			Begin (this, args);
		}

		public void Write (ICodeWriter writer, object o)
		{
			if (indent)
				writer.Indent ();
			writer.BeginNewLine (prependIndents);
			writer.Write (Contents, allowSplit);
			writer.EndLine ();
		}

		public void EndWrite (ICodeWriter writer, object o)
		{
			OnEnd (new WriteEventArgs (writer));
		}

		protected virtual void OnEnd (WriteEventArgs args)
		{
			End.FireInReverse (this, args);
		}

		#endregion

		public override string ToString ()
		{
			return Contents;
		}
	}
}

