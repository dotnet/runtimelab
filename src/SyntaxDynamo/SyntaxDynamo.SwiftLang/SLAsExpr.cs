// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SyntaxDynamo.SwiftLang {
	public class SLAsExpr : SLBaseExpr {
		public SLAsExpr (SLType asType)
		{
			AsType = Exceptions.ThrowOnNull (asType, nameof (asType));
		}

		public SLType AsType { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.Write ("as ", true);
			AsType.WriteAll (writer);
		}
	}
}
