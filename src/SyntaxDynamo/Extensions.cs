// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace SyntaxDynamo {
	public static class Extensions {
		public static void WriteAll (this ICodeElement elem, ICodeWriter writer)
		{
			object memento = elem.BeginWrite (writer);
			elem.Write (writer, memento);
			ICodeElementSet set = elem as ICodeElementSet;
			if (set != null) {
				foreach (ICodeElement sub in set.Elements) {
					sub.WriteAll (writer);
				}
			}
			elem.EndWrite (writer, memento);
		}

		public static void FireInReverse<T> (this EventHandler<T> handler, object sender, EventArgs args) where T : EventArgs
		{
			var dels = handler.GetInvocationList ();
			for (int i = dels.Length - 1; i >= 0; i--) {
				dels [i].DynamicInvoke (new object [] { sender, args });
			}
		}

		public static IEnumerable<T> Interleave<T> (this IEnumerable<T> contents, T separator, bool includeSeparatorFirst = false)
		{
			bool first = true;
			foreach (T t in contents) {
				if (!first || includeSeparatorFirst)
					yield return separator;
				first = false;
				yield return t;
			}
		}

		public static IEnumerable<T> BracketInterleave<T> (this IEnumerable<T> contents, T start, T end, T separator, bool includeSeparatorFirst = false)
		{
			yield return start;
			foreach (T t in contents.Interleave (separator, includeSeparatorFirst))
				yield return t;
			yield return end;
		}

		public static T AttachBefore<T> (this T attacher, ICodeElement attachTo) where T : ICodeElement
		{
			Exceptions.ThrowOnNull (attachTo, nameof (attachTo));
			attachTo.Begin += (s, eventArgs) => {
				attacher.WriteAll (eventArgs.Writer);
			};
			return attacher;
		}

		public static T AttachAfter<T> (this T attacher, ICodeElement attachTo) where T : ICodeElement
		{
			Exceptions.ThrowOnNull (attachTo, nameof (attachTo));
			attachTo.End += (s, eventArgs) => {
				attacher.WriteAll (eventArgs.Writer);
			};
			return attacher;
		}
	}
}

