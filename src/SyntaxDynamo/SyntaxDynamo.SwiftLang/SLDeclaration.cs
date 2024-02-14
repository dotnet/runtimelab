// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.SwiftLang {
	public class SLDeclaration : LineCodeElementCollection<ICodeElement>, ISLExpr, ISLLineable {
		public SLDeclaration (bool isLet, SLBinding binding, Visibility vis = Visibility.Private, bool isStatic = false)
			: base (null, false, true)
		{
			IsLet = isLet;
			IsStatic = isStatic;
			And (new SimpleElement (SLFunc.ToVisibilityString (vis))).And (SimpleElement.Spacer);
			if (IsStatic)
				Add (new SimpleElement ("static ", true));
			And (new SimpleElement (isLet ? "let" : "var")).And (SimpleElement.Spacer);
			Binding = binding;
			Add (Binding);
		}

		public SLDeclaration (bool isLet, string name, SLType typeAnnotation = null, ISLExpr value = null,
			Visibility vis = Visibility.Private, bool isStatic = false)
			: this (isLet, new SLIdentifier (Exceptions.ThrowOnNull (name, nameof(name))), typeAnnotation, value, vis, isStatic)
		{
		}

		public SLDeclaration (bool isLet, SLIdentifier name, SLType typeAnnotation = null, ISLExpr value = null,
			Visibility vis = Visibility.Private, bool isStatic = false)
			: this (isLet, new SLBinding (name, value, typeAnnotation), vis, isStatic)
		{
		}

		public bool IsLet { get; private set; }
		public Visibility Visibilty { get; private set; }
		public SLBinding Binding { get; private set; }
		public bool IsStatic { get; private set; }


		public static SLLine LetLine (SLIdentifier name, SLType typeAnnotation, ISLExpr value = null,
			Visibility vis = Visibility.Private, bool isStatic = false)
		{
			return new SLLine (new SLDeclaration (true, name, typeAnnotation, value, vis, isStatic));
		}

		public static SLLine LetLine (string name, SLType typeAnnotation, ISLExpr value = null,
			Visibility vis = Visibility.Private, bool isStatic = false)
		{
			return new SLLine (new SLDeclaration (true, name, typeAnnotation, value, vis, isStatic));
		}

		public static SLLine VarLine (SLIdentifier name, SLType typeAnnotation, ISLExpr value = null,
			Visibility vis = Visibility.Private, bool isStatic = false)
		{
			return new SLLine (new SLDeclaration (false, name, typeAnnotation, value, vis, isStatic));
		}

		public static SLLine VarLine (string name, SLType typeAnnotation, ISLExpr value = null,
			Visibility vis = Visibility.Private, bool isStatic = false)
		{
			return new SLLine (new SLDeclaration (false, name, typeAnnotation, value, vis, isStatic));
		}

	}
}

