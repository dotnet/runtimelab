// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SyntaxDynamo.CSLang {
	public class CSInterface : ICodeElementSet, ICSTopLevelDeclaration {
		public CSInterface (CSVisibility vis, CSIdentifier name,
			IEnumerable<CSMethod> methods = null)
		{
			Visibility = vis;
			Name = Exceptions.ThrowOnNull (name, "name");
			Inheritance = new CSInheritance ();
			Methods = new List<CSMethod> ();
			Properties = new List<CSProperty> ();
			GenericParams = new CSGenericTypeDeclarationCollection ();
			GenericConstraints = new CSGenericConstraintCollection ();
			if (methods != null)
				Methods.AddRange (methods);
		}

		public CSInterface (CSVisibility vis, string name, IEnumerable<CSMethod> methods = null)
			: this (vis, new CSIdentifier (name), methods)
		{
		}


		public CSType ToCSType (IEnumerable<CSType> genericReplacements)
		{
			var replacements = genericReplacements.ToList ();
			if (replacements.Count < GenericParams.Count) {
				replacements.AddRange (GenericParams.Skip (replacements.Count).Select (gen => new CSSimpleType (gen.Name.Name)));
			}
			return new CSSimpleType (Name.Name, false, replacements.ToArray ());
		}

		public CSType ToCSType ()
		{
			return ToCSType (GenericParams.Select (gen => new CSSimpleType (gen.Name.Name)));
		}

		public CSVisibility Visibility { get; private set; }
		public CSIdentifier Name { get; private set; }
		public CSInheritance Inheritance { get; private set; }
		public List<CSMethod> Methods { get; private set; }
		public List<CSProperty> Properties { get; private set; }
		public CSGenericTypeDeclarationCollection GenericParams { get; private set; }
		public CSGenericConstraintCollection GenericConstraints { get; private set; }

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
				decl.Add (new CSIdentifier ("interface "));
				decl.Add (Name);

				decl.Add (GenericParams);
				if (Inheritance.Count > 0) {
					decl.Add (new SimpleElement (" : ", true));
					decl.Add (Inheritance);
				}
				if (GenericConstraints.Count > 0) {
					decl.Add (SimpleElement.Spacer);
					decl.Add (GenericConstraints);
				}
				yield return decl;

				var contents = new DecoratedCodeElementCollection<ICodeElement> ("{", "}",
					true, true, true);

				contents.AddRange (Methods);
				contents.AddRange (Properties);

				yield return contents;

			}
		}

		#endregion
	}
}

