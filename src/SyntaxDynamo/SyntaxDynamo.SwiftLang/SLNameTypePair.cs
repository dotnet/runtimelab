// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo.SwiftLang {
	public class SLNameTypePair : DelegatedSimpleElement {
		public SLNameTypePair (SLParameterKind kind, SLIdentifier id, SLType type)
			: base ()
		{
			ParameterKind = kind;
			Name = id;
			TypeAnnotation = type;
		}

		public SLNameTypePair (SLIdentifier id, SLType type)
			: this (SLParameterKind.None, id, type)
		{
		}

		public SLNameTypePair(SLParameterKind kind, string id, SLType type)
			: this (kind, id != null ? new SLIdentifier(id) : null, type)
		{			
		}

		public SLNameTypePair(string id, SLType type)
			: this (SLParameterKind.None, id, type)
		{			
		}

		public SLIdentifier Name { get; private set; }
		public SLType TypeAnnotation { get; private set; }
		public SLParameterKind ParameterKind { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			if (Name != null) {
				Name.WriteAll (writer);
			}
			if (TypeAnnotation != null) {
				if (Name != null)
					writer.Write (": ", true);
				if (ParameterKind != SLParameterKind.None) {
					writer.Write (ToParameterKindString (ParameterKind), false);
					writer.Write (' ', false);
				}
				TypeAnnotation.WriteAll (writer);
			}
		}

		internal static string ToParameterKindString (SLParameterKind kind)
		{
			switch (kind) {
			case SLParameterKind.None:
				return "";
			case SLParameterKind.Var:
				return "var";
			case SLParameterKind.InOut:
				return "inout";
			default:
				throw new ArgumentOutOfRangeException (nameof(kind), "unexpected value " + kind.ToString ());
			}
		}
	}
}

