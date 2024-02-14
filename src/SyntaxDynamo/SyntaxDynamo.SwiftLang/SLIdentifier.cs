// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.SwiftLang {
	public class SLIdentifier : SLBaseExpr {
		public SLIdentifier (string name)
		{
			Name = Exceptions.ThrowOnNull (name, nameof(name));
		}

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write (Name, false);
		}

		public string Name { get; private set; }

		public override string ToString ()
		{
			return Name;
		}

		static SLIdentifier anonymousIdentifier = new SLIdentifier ("_");
		public static SLIdentifier Anonymous { get { return anonymousIdentifier; } }
		static SLIdentifier superIdentifier = new SLIdentifier ("super");
		public static SLIdentifier Super { get { return superIdentifier; } }
	}
}

