// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SyntaxDynamo.CSLang {
	public class CSEnum : ICodeElementSet, ICSTopLevelDeclaration {
		public CSEnum (CSVisibility vis, CSIdentifier name, CSType optionalType)
		{
			Values = new List<CSBinding> ();
			Name = Exceptions.ThrowOnNull (name, "name");
			OptionalType = optionalType;
			Visibility = vis;
		}

		public CSEnum (CSVisibility vis, string name, CSType optionalType)
			: this (vis, new CSIdentifier (name), optionalType)
		{
		}

		public List<CSBinding> Values { get; private set; }
		public CSVisibility Visibility { get; private set; }
		public CSType OptionalType { get; private set; }
		public CSIdentifier Name { get; private set; }

		public CSType ToCSType ()
		{
			return new CSSimpleType (Name.Name);
		}

		#region ICodeElem implementation

		public event EventHandler<WriteEventArgs> Begin = (s, e) => { };

		public event EventHandler<WriteEventArgs> End = (s, e) => { };

		public virtual object BeginWrite (ICodeWriter writer)
		{
			OnBeginWrite (new WriteEventArgs (writer));
			return null;
		}

		protected virtual void OnBeginWrite (WriteEventArgs args)
		{
			Begin (this, args);
		}

		public virtual void Write (ICodeWriter writer, object o)
		{
		}

		public virtual void EndWrite (ICodeWriter writer, object o)
		{
			OnEndWrite (new WriteEventArgs (writer));
		}

		protected virtual void OnEndWrite (WriteEventArgs args)
		{
			End.FireInReverse (this, args);
		}

		#endregion

		#region ICodeElemSet implementation

		public IEnumerable<ICodeElement> Elements {
			get {
				var decl = new LineCodeElementCollection<ICodeElement> (true, false, true);
				if (Visibility != CSVisibility.None)
					decl.Add (new SimpleElement (CSMethod.VisibilityToString (Visibility) + " "));
				decl.Add (new CSIdentifier ("enum" + " "));
				decl.Add (Name);

				if (OptionalType != null) {
					decl.Add (new SimpleElement (" : ", true));
					decl.Add (OptionalType);
				}
				yield return decl;

				var contents = new DecoratedCodeElementCollection<ICodeElement> ("{", "}",
					true, true, true);

				var bindings = new CommaListElementCollection<CSBinding> ();
				bindings.AddRange (Values);
				if (bindings.Count > 0 && !bindings [0].OnOwnLine) {
					bindings [0] = new CSBinding (bindings [0].Name, bindings [0].Value, true);
				}
				contents.Add (bindings);

				yield return contents;

			}
		}

		#endregion

	}
}

