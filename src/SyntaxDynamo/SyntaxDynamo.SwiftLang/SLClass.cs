// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SyntaxDynamo.SwiftLang {
	public class SLClass : ICodeElementSet {
		public SLClass (Visibility vis, SLIdentifier name, IEnumerable<SLFunc> methods = null,
			bool isStatic = false, bool isSealed = false, NamedType namedType = NamedType.Class,
			bool isFinal = false)
		{
			// swift hates when you put public on an extension on a public type
			Visibility = vis == Visibility.Public && namedType == NamedType.Extension ? Visibility.None : vis;
			IsStatic = isStatic;
			IsSealed = isSealed;
			IsFinal = isFinal;
			NamedType = namedType;
			Name = Exceptions.ThrowOnNull (name, "name");
			Inheritance = new SLInheritance ();
			Fields = new List<ICodeElement> ();
			Constructors = new List<SLFunc> ();
			Methods = new List<SLFunc> ();
			Properties = new List<SLProperty> ();
			InnerClasses = new SLClasses ();
			Subscripts = new List<SLSubscript> ();
			Generics = new SLGenericTypeDeclarationCollection ();

			if (methods != null)
				Methods.AddRange (methods);
		}

		public SLClass (Visibility vis, string name,
			IEnumerable<SLFunc> members = null, bool isStatic = false, bool isSealed = false, NamedType namedType = NamedType.Class,
			bool isFinal = false)
			: this (vis, new SLIdentifier (name), members, isStatic, isSealed, namedType, isFinal)
		{
		}

		public Visibility Visibility { get; private set; }
		public bool IsStatic { get; private set; }
		public bool IsSealed { get; private set; }
		public bool IsFinal { get; private set; }
		public NamedType NamedType { get; private set; }
		public SLIdentifier Name { get; private set; }
		public SLInheritance Inheritance { get; private set; }
		public List<ICodeElement> Fields { get; private set; }
		public List<SLFunc> Constructors { get; private set; }
		public List<SLFunc> Methods { get; private set; }
		public List<SLProperty> Properties { get; private set; }
		public SLClasses InnerClasses { get; private set; }
		public List<SLSubscript> Subscripts { get; private set; }
		public SLGenericTypeDeclarationCollection Generics { get; private set; }


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

		static SLIdentifier IdentifierForNamedType (NamedType nt)
		{
			switch (nt) {
			case NamedType.Class: return new SLIdentifier ("class");
			case NamedType.Struct: return new SLIdentifier ("struct");
			case NamedType.Extension: return new SLIdentifier ("extension");
			case NamedType.Actor: return new SLIdentifier ("actor");
			default:
				throw new ArgumentOutOfRangeException ($"Unknown named type {nt}", nameof (nt));
			}
		}

		#region ICodeElemSet implementation

		public IEnumerable<ICodeElement> Elements {
			get {
				var decl = new LineCodeElementCollection<ICodeElement> (true, false, true);
				if (Visibility != Visibility.None)
					decl.Add (new SimpleElement (SLFunc.ToVisibilityString (Visibility) + " "));
				if (IsStatic)
					decl.Add (new SimpleElement ("static ", true));
				if (IsSealed)
					decl.Add (new SimpleElement ("sealed ", true));
				if (IsFinal)
					decl.Add (new SimpleElement ("final ", true));
				decl.Add (IdentifierForNamedType (NamedType));
				decl.Add (SimpleElement.Spacer);
				decl.Add (Name);
				if (Generics.Count > 0) {
					decl.Add (Generics);
				}
				if (Inheritance.Count > 0) {
					decl.Add (new SimpleElement (" : ", true));
					decl.Add (Inheritance);
				}
				yield return decl;

				foreach (var constraint in Generics.ConstraintElements) {
					yield return constraint;
				}

				var contents = new DecoratedCodeElementCollection<ICodeElement> ("{", "}",
					true, true, true);

				contents.AddRange (Fields);
				contents.AddRange (Constructors);
				contents.AddRange (Methods);
				contents.AddRange (Properties);
				contents.AddRange (Subscripts);
				contents.Add (InnerClasses);

				yield return contents;
			}
		}

		#endregion
	}

	public class SLClasses : CodeElementCollection<SLClass> {
		public SLClasses (IEnumerable<SLClass> classes = null)
			: base ()
		{
			if (classes != null)
				AddRange (classes);
		}

		public SLClasses And (SLClass use)
		{
			Add (use);
			return this;
		}
	}
}

