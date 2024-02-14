// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.SwiftLang {
	public class SLBinding : SLBaseExpr, ISLLineable {
		public SLBinding (SLIdentifier id, ISLExpr value, SLType typeAnnotation = null)
		{
			Name = Exceptions.ThrowOnNull (id, nameof(id));
			Value = value;
			TypeAnnotation = typeAnnotation;
		}

		public SLBinding (string id, ISLExpr value, SLType typeAnnotation = null)
			: this (new SLIdentifier (id), value, typeAnnotation)
		{
		}

		public SLBinding (SLSubscriptExpr sub, ISLExpr value)
		{
			Name = null;
			Subscript = Exceptions.ThrowOnNull (sub, nameof(sub));
			Value = value;
			TypeAnnotation = null;
		}

		public SLIdentifier Name { get; private set; }
		public SLSubscriptExpr Subscript { get; private set; }
		public SLType TypeAnnotation { get; private set; }
		public ISLExpr Value { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.BeginNewLine (true);
			if (Name != null)
				Name.WriteAll (writer);
			else
				Subscript.WriteAll (writer);

			if (TypeAnnotation != null) {
				writer.Write (": ", true);
				TypeAnnotation.WriteAll (writer);
			}

			if (Value != null) {
				writer.Write (" = ", true);
				Value.WriteAll (writer);
			}
			writer.EndLine ();
		}


	}
}

