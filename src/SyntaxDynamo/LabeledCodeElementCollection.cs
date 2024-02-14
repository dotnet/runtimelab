// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace SyntaxDynamo {
	public class LabeledCodeElementCollection<T> : ICodeElementSet where T : ICodeElement {
		public LabeledCodeElementCollection (SimpleLineElement label, CodeElementCollection<T> block)
		{
			Label = Exceptions.ThrowOnNull (label, nameof(label));
			Block = Exceptions.ThrowOnNull (block, nameof(block));
		}

		public SimpleLineElement Label { get; private set; }
		public CodeElementCollection<T> Block { get; private set; }

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

		#region ICodeElemSet implementation

		public IEnumerable<ICodeElement> Elements {
			get {
				yield return Label;
				yield return Block;
			}
		}

		#endregion

	}
}

