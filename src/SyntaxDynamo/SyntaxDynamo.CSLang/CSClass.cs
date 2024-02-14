// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using SyntaxDynamo;

namespace SyntaxDynamo.CSLang {
	public class CSClass : ICodeElementSet, ICSTopLevelDeclaration {
		public CSClass (CSVisibility vis, CSIdentifier name, IEnumerable<CSMethod> methods = null,
			bool isStatic = false, bool isSealed = false)
		{
			Visibility = vis;
			IsStatic = isStatic;
			IsSealed = isSealed;
			Name = Exceptions.ThrowOnNull (name, "name");
			Inheritance = new CSInheritance ();
			Delegates = new List<CSDelegateTypeDecl> ();
			Fields = new List<ICodeElement> ();
			Constructors = new List<CSMethod> ();
			Methods = new List<CSMethod> ();
			Properties = new List<CSProperty> ();
			InnerClasses = new CSClasses ();
			InnerEnums = new List<CSEnum> ();
			StaticConstructor = new CSCodeBlock ();
			GenericParams = new CSGenericTypeDeclarationCollection ();
			GenericConstraints = new CSGenericConstraintCollection ();

			if (methods != null)
				Methods.AddRange (methods);
		}

		public CSClass (CSVisibility vis, string name,
			IEnumerable<CSMethod> members = null, bool isStatic = false, bool isSealed = false)
			: this (vis, new CSIdentifier (name), members, isStatic, isSealed)
		{
		}

		protected virtual string EntityLabel { get { return "class"; } }

		public CSVisibility Visibility { get; private set; }
		public bool IsStatic { get; private set; }
		public bool IsSealed { get; private set; }
		public CSIdentifier Name { get; private set; }
		public CSInheritance Inheritance { get; private set; }
		public List<CSDelegateTypeDecl> Delegates { get; private set; }
		public List<ICodeElement> Fields { get; private set; }
		public List<CSMethod> Constructors { get; private set; }
		public List<CSMethod> Methods { get; private set; }
		public List<CSProperty> Properties { get; private set; }
		public CSClasses InnerClasses { get; private set; }
		public List<CSEnum> InnerEnums { get; private set; }
		public CSCodeBlock StaticConstructor { get; private set; }
		public CSGenericTypeDeclarationCollection GenericParams { get; private set; }
		public CSGenericConstraintCollection GenericConstraints { get; private set; }

		public CSType ToCSType (IEnumerable<CSType> genericReplacements)
		{
			List<CSType> replacements = genericReplacements.ToList ();
			if (replacements.Count < GenericParams.Count) {
				replacements.AddRange (GenericParams.Skip (replacements.Count).Select (gen => new CSSimpleType (gen.Name.Name)));
			}
			CSSimpleType t = new CSSimpleType (Name.Name, false, replacements.ToArray ());
			return t;
		}

		public CSType ToCSType ()
		{
			return ToCSType (GenericParams.Select (gen => new CSSimpleType (gen.Name.Name)));
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
				if (IsStatic)
					decl.Add (new SimpleElement ("static ", true));
				if (IsSealed)
					decl.Add (new SimpleElement ("sealed ", true));
				decl.Add (new CSIdentifier (EntityLabel + " "));
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

				contents.AddRange (Delegates);
				contents.AddRange (Fields);

				if (StaticConstructor.Count > 0) {
					var m = new CSMethod (CSVisibility.None, CSMethodKind.Static,
							    null, Name, new CSParameterList (), StaticConstructor);
					contents.Add (m);
				}
				contents.AddRange (Constructors);
				contents.AddRange (Methods);
				contents.AddRange (Properties);
				contents.Add (InnerClasses);
				contents.AddRange (InnerEnums);

				yield return contents;

			}
		}

		#endregion
	}

	public class CSClasses : CodeElementCollection<CSClass> {
		public CSClasses (IEnumerable<CSClass> classes = null)
			: base ()
		{
			if (classes != null)
				AddRange (classes);
		}

		public CSClasses And (CSClass use)
		{
			Add (use);
			return this;
		}
	}

	public class CSStruct : CSClass {
		public CSStruct (CSVisibility vis, CSIdentifier name, IEnumerable<CSMethod> methods = null,
			bool isStatic = false, bool isSealed = false)
			: base (vis, name, methods, isStatic, isSealed)
		{
		}

		public CSStruct (CSVisibility vis, string name,
			IEnumerable<CSMethod> members = null, bool isStatic = false, bool isSealed = false)
			: this (vis, new CSIdentifier (name), members, isStatic, isSealed)
		{
		}

		protected override string EntityLabel {
			get {
				return "struct";
			}
		}
	}

	public class CSStructs : CodeElementCollection<CSStruct> {
		public CSStructs (IEnumerable<CSStruct> structs = null)
			: base ()
		{
			if (structs != null)
				AddRange (structs);
		}

		public CSStructs And (CSStruct st)
		{
			Add (st);
			return this;
		}
	}
}

