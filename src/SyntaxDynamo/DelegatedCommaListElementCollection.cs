// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace SyntaxDynamo {
	public class DelegatedCommaListElemCollection<T> : List<T>, ICodeElement where T : ICodeElement {
		Action<ICodeWriter, int, T> elementWriter;
		public DelegatedCommaListElemCollection (Action<ICodeWriter, int, T> elementWriter)
			: base ()
		{
			this.elementWriter = Exceptions.ThrowOnNull (elementWriter, nameof(elementWriter));
		}

		public DelegatedCommaListElemCollection (Action<ICodeWriter, int, T> elementWriter, IEnumerable<T> objs)
			: base ()
		{
			this.elementWriter = Exceptions.ThrowOnNull (elementWriter, nameof (elementWriter));
			AddRange (Exceptions.ThrowOnNull (objs, nameof(objs)));
		}

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
			for (int i = 0; i < Count; i++) {
				elementWriter (writer, i, this [i]);
				if (i < Count - 1)
					writer.Write (", ", true);
			}
		}

		public void EndWrite (ICodeWriter writer, object o)
		{
			OnEnd (new WriteEventArgs (writer));
		}


		protected virtual void OnEnd (WriteEventArgs args)
		{
			End.FireInReverse (this, args);
		}
	}
}

