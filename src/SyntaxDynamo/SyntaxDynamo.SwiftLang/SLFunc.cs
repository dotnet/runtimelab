// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SyntaxDynamo.SwiftLang {
	public class SLFunc : ICodeElementSet {
		public SLFunc (Visibility vis, SLType type, SLIdentifier name, SLParameterList parms, SLCodeBlock body)
			: this (vis, type, name, parms, body, false)
		{
		}

		public SLFunc (Visibility vis, SLType type, SLIdentifier name, SLParameterList parms, SLCodeBlock body, bool throws)
			: this (vis, (throws ? FunctionKind.Throws : FunctionKind.None), type, name, parms, body)
		{
		}

		public SLFunc (Visibility vis, FunctionKind funcKind, SLType type, SLIdentifier name, SLParameterList parms, SLCodeBlock body, bool isOptional = false)
		{
			GenericParams = new SLGenericTypeDeclarationCollection ();
			Visibility = vis;
			ReturnType = type;
			bool isConstructor = (funcKind & FunctionKind.Constructor) != 0;
			Name = isConstructor ? new SLIdentifier (isOptional ? "init?" : "init") : Exceptions.ThrowOnNull (name, nameof (name));
			Parameters = parms ?? new SLParameterList ();
			Body = Exceptions.ThrowOnNull (body, nameof (body));
			FuncKind = funcKind;
			IsConstructor = isConstructor;
			IsOptional = isOptional;
		}

		public bool IsConstructor { get; private set; }
		public FunctionKind FuncKind { get; private set; }
		public Visibility Visibility { get; private set; }
		public SLType ReturnType { get; private set; }
		public SLIdentifier Name { get; private set; }
		public SLParameterList Parameters { get; private set; }
		public SLCodeBlock Body { get; private set; }
		public SLGenericTypeDeclarationCollection GenericParams { get; private set; }
		public bool IsOptional { get; private set; }


		public static string ToVisibilityString (Visibility vis)
		{
			switch (vis) {
			case Visibility.Private:
				return "private";
			case Visibility.Public:
				return "public";
			case Visibility.None:
				return "";
			case Visibility.Internal:
				return "internal";
			case Visibility.Open:
				return "open";
			case Visibility.FilePrivate:
				return "fileprivate";
			default:
				throw new ArgumentOutOfRangeException (nameof (vis));
			}
		}

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
			writer.BeginNewLine (true);
		}

		public virtual void EndWrite (ICodeWriter writer, object o)
		{
			OnEndWrite (new WriteEventArgs (writer));
		}

		protected virtual void OnEndWrite (WriteEventArgs args)
		{
			End.FireInReverse (this, args);
		}

		public IEnumerable<ICodeElement> Elements {
			get {
				if (Visibility != Visibility.None) {
					yield return new SimpleElement (ToVisibilityString (Visibility), false);
					yield return SimpleElement.Spacer;
				}
				if ((FuncKind & FunctionKind.Final) != 0) {
					yield return new SimpleElement ("final");
					yield return SimpleElement.Spacer;
				}
				if ((FuncKind & FunctionKind.Override) != 0) {
					yield return new SimpleElement ("override");
					yield return SimpleElement.Spacer;
				}
				if ((FuncKind & FunctionKind.Required) != 0) {
					yield return new SimpleElement ("required");
					yield return SimpleElement.Spacer;
				}
				if ((FuncKind & FunctionKind.Static) != 0) {
					yield return new SimpleElement ("static");
					yield return SimpleElement.Spacer;
				}
				if ((FuncKind & FunctionKind.Class) != 0) {
					yield return new SimpleElement ("class");
					yield return SimpleElement.Spacer;
				}

				if (!IsConstructor) {
					yield return new SimpleElement ("func");
					yield return SimpleElement.Spacer;
				}


				yield return Name;
				yield return GenericParams;
				yield return Parameters;

				if ((FuncKind & FunctionKind.Async) != 0) {
					yield return SimpleElement.Spacer;
					yield return new SimpleElement ("async");
					yield return SimpleElement.Spacer;
				}
				if ((FuncKind & FunctionKind.Throws) != 0) {
					yield return SimpleElement.Spacer;
					yield return new SimpleElement ("throws");
					yield return SimpleElement.Spacer;
				}
				if ((FuncKind & FunctionKind.Rethrows) != 0) {
					yield return SimpleElement.Spacer;
					yield return new SimpleElement ("rethrows");
					yield return SimpleElement.Spacer;
				}
				if (!IsConstructor && ReturnType != null) {
					yield return SimpleElement.Spacer;
					yield return new SimpleElement ("->");
					yield return SimpleElement.Spacer;
					yield return ReturnType;
				}
				foreach (ICodeElement constr in GenericParams.ConstraintElements) {
					yield return constr;
				}

				yield return Body;
			}
		}
	}
}

