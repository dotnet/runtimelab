// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo.SwiftLang {
	public class SLGenericConstraint : DelegatedSimpleElement {
		public SLGenericConstraint (bool isInheritance, SLType firstType, SLType secondType)
		{
			IsInheritance = isInheritance;
			FirstType = Exceptions.ThrowOnNull (firstType, nameof (firstType));
			SecondType = Exceptions.ThrowOnNull (secondType, nameof (secondType));
		}

		public bool IsInheritance { get; private set; }
		public SLType FirstType { get; private set; }
		public SLType SecondType { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			FirstType.Write (writer, o);
			writer.Write (String.Format (" {0} ", IsInheritance ? ":" : "=="), true);
			SecondType.Write (writer, o);
		}
	}
}
