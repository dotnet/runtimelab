// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo.CSLang {
	public class CSDelegateTypeDecl : DelegatedSimpleElement, ICSStatement {
		public CSDelegateTypeDecl (CSVisibility vis, CSType type, CSIdentifier name, CSParameterList parms, bool isUnsafe = false)
		{
			Visibility = vis;
			Type = type != null ? type : CSSimpleType.Void;
			Name = Exceptions.ThrowOnNull (name, "name");
			Parameters = parms;
			IsUnsafe = isUnsafe;
		}

		public CSVisibility Visibility { get; private set; }
		public CSType Type { get; private set; }
		public CSIdentifier Name { get; private set; }
		public CSParameterList Parameters { get; private set; }
		public bool IsUnsafe { get; private set; }

		protected override void LLWrite (ICodeWriter writer, object o)
		{
			writer.BeginNewLine (true);
			writer.Write (CSMethod.VisibilityToString (Visibility), false);
			if (IsUnsafe)
				writer.Write (" unsafe", true);
			writer.Write (" delegate ", true);
			Type.WriteAll (writer);
			writer.Write (' ', true);
			Name.WriteAll (writer);
			writer.Write ('(', true);
			Parameters.WriteAll (writer);
			writer.Write (')', true);
			writer.Write (';', false);
			writer.EndLine ();
		}
	}
}

